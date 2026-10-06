using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Infrastructure.HealthChecks;
using EscolaSystemApi.Infrastructure.Repositories;
using EscolaSystemApi.Infrastructure.Security;
using EscolaSystemApi.Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

namespace EscolaSystemApi.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var commandTimeout = configuration.GetValue("Database:CommandTimeoutSeconds", 30);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"), npgsql => npgsql
                // Quedas curtas do banco (failover, restart) são repetidas em vez de virar 500
                .EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)
                .CommandTimeout(commandTimeout)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        services.AddHealthChecks()
            .AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);

        return services;
    }

    // Atrás de proxy reverso, o IP do cliente vem em X-Forwarded-For; só se confia em proxies configurados
    public static IServiceCollection AddForwardedHeadersSupport(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;

            foreach (var proxy in configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
                if (IPAddress.TryParse(proxy, out var ip))
                    options.KnownProxies.Add(ip);

            foreach (var network in configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
            {
                var parts = network.Split('/');
                if (parts.Length == 2 && IPAddress.TryParse(parts[0], out var prefix) && int.TryParse(parts[1], out var length))
                    options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, length));
            }
        });

        return services;
    }

    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<AuthCookies>();
        services.AddScoped<ICpfEncryptionService, CpfEncryptionService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddScoped<IUserService, UserService>();
        // Só lê configuração na construção: uma instância basta
        services.AddSingleton<IJwtService, JwtService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISchoolService, SchoolService>();
        services.AddScoped<IClassService, ClassService>();
        services.AddScoped<IStudentService, StudentService>();
        services.AddScoped<IGradeService, GradeService>();
        services.AddScoped<IAttendanceService, AttendanceService>();
        services.AddScoped<IDisciplinaryCallService, DisciplinaryCallService>();
        services.AddScoped<IPendingWorkService, PendingWorkService>();
        services.AddScoped<IDashboardService, DashboardService>();

        return services;
    }

    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = configuration["Jwt:Issuer"],
                    ValidAudience = configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    // O navegador manda o token no cookie httpOnly; ferramentas e integrações podem usar o cabeçalho Authorization
                    OnMessageReceived = ctx =>
                    {
                        if (string.IsNullOrEmpty(ctx.Token) && !ctx.Request.Headers.ContainsKey("Authorization"))
                            ctx.Token = ctx.Request.Cookies[AuthCookies.AccessCookie];
                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async ctx =>
                    {
                        // Toda requisição confere no banco se a sessão não foi encerrada (logout, senha nova, conta desativada)
                        var sid = ctx.Principal?.FindFirstValue(ClaimTypes.Sid) ?? ctx.Principal?.FindFirstValue(JwtService.SessionClaim);
                        var sub = ctx.Principal?.FindFirstValue(ClaimTypes.NameIdentifier)
                                  ?? ctx.Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub);
                        var role = ctx.Principal?.FindFirstValue(ClaimTypes.Role) ?? string.Empty;
                        var schoolClaim = ctx.Principal?.FindFirstValue("schoolId");
                        Guid? schoolId = Guid.TryParse(schoolClaim, out var parsedSchool) ? parsedSchool : null;

                        var sessionValidator = ctx.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                        if (!Guid.TryParse(sub, out var userId)
                            || !Guid.TryParse(sid, out var sessionId)
                            || !await sessionValidator.IsValidAsync(userId, sessionId, role, schoolId, ctx.HttpContext.RequestAborted))
                            ctx.Fail("Sessão não corresponde mais ao usuário.");
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services, IConfiguration configuration, IWebHostEnvironment env)
    {
        var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
            ?? ["http://localhost:3000", "http://localhost:5173"];

        if (!env.IsDevelopment())
            origins = origins.Where(o => o.StartsWith("https://", StringComparison.OrdinalIgnoreCase)).ToArray();

        services.AddCors(options =>
        {
            options.AddPolicy("DefaultPolicy", policy =>
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials());
        });

        return services;
    }

    public static IServiceCollection AddRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        // Limite por IP alto o bastante para uma escola inteira atrás de um único IP;
        // a proteção de senha fica no bloqueio por conta (AuthService)
        var authPermitLimit = configuration.GetValue("RateLimiting:AuthPermitLimit", 30);

        services.AddRateLimiter(options =>
        {
            // Auth routes: attempts per 15 minutes per IP
            // (particionado por IP; um limiter único bloquearia a escola inteira após 5 logins)
            options.AddPolicy("AuthPolicy", ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = authPermitLimit,
                        Window = TimeSpan.FromMinutes(15),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            // Global policy: 200 requests per minute per IP
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 200,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0
                    }));

            options.OnRejected = async (context, token) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsync(
                    """{"error":"Muitas requisições. Aguarde e tente novamente.","message":"Muitas requisições. Aguarde e tente novamente."}""", token);
            };
        });

        return services;
    }

    public static IServiceCollection AddOpenApiWithScalar(this IServiceCollection services)
    {
        services.AddOpenApi();
        return services;
    }
}

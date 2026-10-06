using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using EscolaSystemApi.Extensions;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Middleware;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .Enrich.FromLogContext()
              .WriteTo.Console();

        // Em container o log vai só para o console; arquivo local apenas quando configurado
        var logFile = ctx.Configuration["Logs:FilePath"];
        if (!string.IsNullOrWhiteSpace(logFile))
            config.WriteTo.File(logFile, rollingInterval: RollingInterval.Day);
    });

    builder.Services.AddControllers(options =>
    {
        // Reject any request body larger than 1 MB
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute(1 * 1024 * 1024));
    })
    .ConfigureApiBehaviorOptions(options =>
        options.InvalidModelStateResponseFactory = ApiErrors.InvalidModelState);

    builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiAuthorizationResultHandler>();

    builder.WebHost.ConfigureKestrel(k =>
        k.Limits.MaxRequestBodySize = 1 * 1024 * 1024); // 1 MB global cap

    builder.Services.AddInfrastructure(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddCorsPolicy(builder.Configuration, builder.Environment);
    builder.Services.AddForwardedHeadersSupport(builder.Configuration);
    builder.Services.AddRateLimiting(builder.Configuration);
    builder.Services.AddOpenApiWithScalar();

    builder.Services.AddHttpContextAccessor();

    var app = builder.Build();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // Com várias instâncias, desligue e aplique as migrations num passo de deploy separado
        if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
            await db.Database.MigrateAsync();
        await DbSeeder.SeedAsync(db);

        var cpfService = scope.ServiceProvider.GetRequiredService<ICpfEncryptionService>();
        var legacyUsers = await db.Users
            .Where(u => u.Cpf != null && u.CpfEncrypted == null)
            .ToListAsync();

        foreach (var u in legacyUsers)
        {
            u.CpfEncrypted = cpfService.Encrypt(u.Cpf!);
            u.CpfHash = cpfService.Hash(u.Cpf!);
            u.Cpf = null;
        }

        if (legacyUsers.Count > 0)
        {
            await db.SaveChangesAsync();
            Log.Information("CPF migration: {Count} registro(s) criptografado(s)", legacyUsers.Count);
        }
    }

    // Antes de tudo que usa o IP do cliente (rate limit, logs)
    if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
        app.UseForwardedHeaders();

    app.UseMiddleware<ExceptionHandlingMiddleware>();
    app.UseMiddleware<SecurityHeadersMiddleware>();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "EscolaSystem API";
            options.Theme = ScalarTheme.Default;
        });
    }

    app.UseCors("DefaultPolicy");
    if (!app.Environment.IsDevelopment())
        app.UseHttpsRedirection();
    app.UseRateLimiter();
    // Depois do CORS (o preflight passa) e antes da autenticação
    app.UseMiddleware<CsrfMiddleware>();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    // Vivo: o processo responde. Pronto: o banco também está acessível.
    app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false }).DisableRateLimiting();
    app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") }).DisableRateLimiting();

    await app.RunAsync();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

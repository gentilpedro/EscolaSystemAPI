using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Infrastructure.Security;
using EscolaSystemApi.Middleware;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class SessionSecurityTests
{
    private static AuthService CreateAuthService(AppDbContext context)
        => new(new UnitOfWork(context), JwtServiceMock.Create(), context);

    private static async Task<(AuthService Service, User Admin, AuthSession Session)> LoginAsync(AppDbContext context)
    {
        var admin = DbContextHelper.CreateAdminUser(context);
        var service = CreateAuthService(context);
        var login = await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        login.IsSuccess.Should().BeTrue();
        return (service, admin, login.Data!);
    }

    // ---------- Rotação do refresh token ----------

    [Fact]
    public async Task Refresh_ValidToken_RotatesAndMarksOldAsUsed()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, _, login) = await LoginAsync(context);

        var result = await service.RefreshAsync(login.RefreshToken);

        result.IsSuccess.Should().BeTrue();
        result.Data!.RefreshToken.Should().NotBe(login.RefreshToken);
        context.RefreshTokens.Single(r => r.TokenHash == UserSessions.Hash(login.RefreshToken)).UsedAt.Should().NotBeNull();
        context.RefreshTokens.Single(r => r.TokenHash == UserSessions.Hash(result.Data.RefreshToken)).UsedAt.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_ReusedTokenAfterGrace_RevokesAllSessionsOfUser()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, admin, login) = await LoginAsync(context);
        var otherDevice = await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        await service.RefreshAsync(login.RefreshToken);

        // Simula o uso da cópia roubada depois da janela de tolerância
        var used = context.RefreshTokens.Single(r => r.TokenHash == UserSessions.Hash(login.RefreshToken));
        used.UsedAt = DateTime.UtcNow - AuthService.RefreshReuseGrace - TimeSpan.FromSeconds(1);
        context.SaveChanges();

        var result = await service.RefreshAsync(login.RefreshToken);

        result.StatusCode.Should().Be(401);
        context.UserSessions.Where(s => s.UserId == admin.Id).Should().OnlyContain(s => s.RevokedAt != null);
        (await service.RefreshAsync(otherDevice.Data!.RefreshToken)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Refresh_ReusedTokenWithinGrace_ReturnsConflictWithoutRevoking()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, _, login) = await LoginAsync(context);
        await service.RefreshAsync(login.RefreshToken);

        // Outra aba chegou um instante depois com o mesmo token
        var result = await service.RefreshAsync(login.RefreshToken);

        result.StatusCode.Should().Be(409);
        context.UserSessions.Should().OnlyContain(s => s.RevokedAt == null);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("token-que-nao-existe")]
    public async Task Refresh_MissingOrUnknownToken_ReturnsUnauthorized(string? token)
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, _, _) = await LoginAsync(context);

        (await service.RefreshAsync(token)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Refresh_DeactivatedUser_ReturnsUnauthorized()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, admin, login) = await LoginAsync(context);
        admin.IsActive = false;
        context.SaveChanges();

        (await service.RefreshAsync(login.RefreshToken)).StatusCode.Should().Be(401);
    }

    // ---------- Revogação ----------

    [Fact]
    public async Task Logout_BySession_RevokesAccessAndRefresh()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, admin, login) = await LoginAsync(context);
        var sessionId = context.UserSessions.Single().Id;

        await service.LogoutAsync(sessionId, null);

        (await new SessionValidator(context).IsValidAsync(admin.Id, sessionId, "Admin", null)).Should().BeFalse();
        (await service.RefreshAsync(login.RefreshToken)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Logout_WithExpiredAccess_FindsSessionByRefreshToken()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, _, login) = await LoginAsync(context);

        await service.LogoutAsync(null, login.RefreshToken);

        context.UserSessions.Single().RevokedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeactivateUser_RevokesSessions()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var service = CreateAuthService(context);
        var login = await service.LoginAsync(new LoginRequestDto(teacher.Email, "Admin@123"));
        var users = new UserService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), CpfEncryptionHelper.Create());

        await users.DeleteAsync(teacher.Id);

        context.UserSessions.Single(s => s.UserId == teacher.Id).RevokedAt.Should().NotBeNull();
        (await service.RefreshAsync(login.Data!.RefreshToken)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task ResetOwnPassword_KeepsCurrentSessionAndRevokesOthers()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var (service, admin, _) = await LoginAsync(context);
        await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        var sessions = context.UserSessions.OrderBy(s => s.CreatedAt).ToList();
        var current = sessions[0].Id;

        await service.ResetPasswordAsync(new ResetPasswordDto(admin.Email, "Nova@1234"), admin.Id, current);

        context.UserSessions.Single(s => s.Id == current).RevokedAt.Should().BeNull();
        context.UserSessions.Where(s => s.Id != current).Should().OnlyContain(s => s.RevokedAt != null);
    }

    // ---------- CSRF ----------

    private static async Task<int> RunCsrf(string method, string path, string? accessCookie, string? csrfCookie, string? csrfHeader, bool bearer = false)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        var cookies = new List<string>();
        if (accessCookie is not null) cookies.Add($"{AuthCookies.AccessCookie}={accessCookie}");
        if (csrfCookie is not null) cookies.Add($"{AuthCookies.CsrfCookie}={csrfCookie}");
        if (cookies.Count > 0) context.Request.Headers.Cookie = string.Join("; ", cookies);
        if (csrfHeader is not null) context.Request.Headers[AuthCookies.CsrfHeader] = csrfHeader;
        if (bearer) context.Request.Headers.Authorization = "Bearer x";
        context.Response.Body = new MemoryStream();

        var nextCalled = false;
        await new CsrfMiddleware(_ => { nextCalled = true; return Task.CompletedTask; }).InvokeAsync(context);
        return nextCalled ? 200 : context.Response.StatusCode;
    }

    [Fact]
    public async Task Csrf_UnsafeRequestWithCookieAndNoHeader_Returns403()
        => (await RunCsrf("POST", "/api/grades", "jwt", "abc", null)).Should().Be(403);

    [Fact]
    public async Task Csrf_HeaderDifferentFromCookie_Returns403()
        => (await RunCsrf("DELETE", "/api/grades/1", "jwt", "abc", "xyz")).Should().Be(403);

    [Fact]
    public async Task Csrf_MatchingHeader_PassesThrough()
        => (await RunCsrf("PUT", "/api/grades/1", "jwt", "abc", "abc")).Should().Be(200);

    [Theory]
    [InlineData("GET", "/api/grades")]       // leitura não altera nada
    [InlineData("POST", "/api/auth/login")]  // é o login que emite o token
    public async Task Csrf_SafeOrLogin_PassesThrough(string method, string path)
        => (await RunCsrf(method, path, "jwt", null, null)).Should().Be(200);

    [Fact]
    public async Task Csrf_BearerHeaderWithoutCookies_PassesThrough()
        => (await RunCsrf("POST", "/api/grades", null, null, null, bearer: true)).Should().Be(200);

    // ---------- Cookies ----------

    private sealed class TestEnvironment(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "EscolaSystemApi";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    [Fact]
    public void Cookies_TokensAreHttpOnlyStrictAndSecureOutsideDevelopment()
    {
        var cookies = new AuthCookies(new ConfigurationBuilder().Build(), new TestEnvironment("Production"));
        var context = new DefaultHttpContext();
        var session = new AuthSession("access", DateTime.UtcNow.AddMinutes(15), "refresh", DateTime.UtcNow.AddDays(7),
            new UserDto(Guid.NewGuid(), "Admin", "a@test.com", "Admin", null, DateTime.UtcNow));

        cookies.Write(context.Request, context.Response, session, newCsrfToken: true);

        var setCookies = context.Response.Headers.SetCookie.Select(c => c!.ToLowerInvariant()).ToList();
        var access = setCookies.Single(c => c.StartsWith(AuthCookies.AccessCookie));
        var refresh = setCookies.Single(c => c.StartsWith(AuthCookies.RefreshCookie));
        var csrf = setCookies.Single(c => c.StartsWith(AuthCookies.CsrfCookie));

        access.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict");
        refresh.Should().Contain("httponly").And.Contain("path=/api/auth");
        // O front precisa ler o token de CSRF para repeti-lo no cabeçalho
        csrf.Should().NotContain("httponly");
    }
}

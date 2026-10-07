using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class OpenSessionsTests
{
    private const string ChromeWindows = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36";
    private const string SafariIphone = "Mozilla/5.0 (iPhone; CPU iPhone OS 17_5 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/17.5 Mobile/15E148 Safari/604.1";

    private static AuthService Auth(AppDbContext context) => new(new UnitOfWork(context), JwtServiceMock.Create(), context);

    private static Guid SessionOf(AppDbContext context, AuthSession login) =>
        context.RefreshTokens.Single(r => r.TokenHash == UserSessions.Hash(login.RefreshToken)).SessionId;

    [Theory]
    [InlineData(ChromeWindows, "Chrome no Windows")]
    [InlineData(SafariIphone, "Safari no iPhone")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Safari/537.36 Edg/129.0.0.0", "Edge no Windows")]
    [InlineData("Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/129.0.0.0 Mobile Safari/537.36", "Chrome no Android")]
    [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 14.5; rv:130.0) Gecko/20100101 Firefox/130.0", "Firefox no Mac")]
    [InlineData("curl/8.4.0", "Aparelho desconhecido")]
    [InlineData(null, "Aparelho desconhecido")]
    public void DeviceDescription_SummarizesUserAgent(string? userAgent, string expected) =>
        DeviceDescription.From(userAgent).Should().Be(expected);

    [Fact]
    public async Task Sessions_ListsOpenDevicesWithCurrentFirst()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var phone = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), SafariIphone)).Data!;
        var desktop = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), ChromeWindows)).Data!;
        var current = SessionOf(context, desktop);

        var sessions = (await Auth(context).GetSessionsAsync(admin.Id, current)).Data!;

        sessions.Should().HaveCount(2);
        sessions[0].Should().Match<SessionDto>(s => s.IsCurrent && s.Device == "Chrome no Windows");
        sessions[1].Should().Match<SessionDto>(s => !s.IsCurrent && s.Device == "Safari no iPhone" && s.Id == SessionOf(context, phone));
    }

    [Fact]
    public async Task RevokeSession_EndsThatDeviceOnly_AndRefusesCurrent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var phone = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), SafariIphone)).Data!;
        var desktop = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), ChromeWindows)).Data!;
        var current = SessionOf(context, desktop);

        (await Auth(context).RevokeSessionAsync(admin.Id, current, current)).StatusCode.Should().Be(400);
        (await Auth(context).RevokeSessionAsync(admin.Id, SessionOf(context, phone), current)).StatusCode.Should().Be(204);

        (await Auth(context).RefreshAsync(phone.RefreshToken)).StatusCode.Should().Be(401);
        (await Auth(context).RefreshAsync(desktop.RefreshToken)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeSession_OfAnotherPerson_IsNotFound()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var directorLogin = (await Auth(context).LoginAsync(new LoginRequestDto(director.Email, "Admin@123"))).Data!;

        var result = await Auth(context).RevokeSessionAsync(admin.Id, SessionOf(context, directorLogin), null);

        result.StatusCode.Should().Be(404);
        (await Auth(context).RefreshAsync(directorLogin.RefreshToken)).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeOtherSessions_KeepsOnlyCurrent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var phone = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), SafariIphone)).Data!;
        var desktop = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"), ChromeWindows)).Data!;
        var current = SessionOf(context, desktop);

        await Auth(context).RevokeOtherSessionsAsync(admin.Id, current);

        (await Auth(context).GetSessionsAsync(admin.Id, current)).Data!.Should().ContainSingle(s => s.IsCurrent);
        (await Auth(context).RefreshAsync(phone.RefreshToken)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Refresh_UpdatesLastUsedAt()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var login = (await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"))).Data!;
        var session = context.UserSessions.Single();
        session.LastUsedAt = DateTime.UtcNow.AddHours(-2);
        context.SaveChanges();

        await Auth(context).RefreshAsync(login.RefreshToken);

        context.UserSessions.Single().LastUsedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task AdminDisconnectsDirector_FromAllDevices_WithSamePassword()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var directorLogin = (await Auth(context).LoginAsync(new LoginRequestDto(director.Email, "Admin@123"))).Data!;
        var users = new UserService(new UnitOfWork(context), context, new CurrentUserServiceMock(admin.Id, "Admin"), CpfEncryptionHelper.Create());

        (await users.RevokeSessionsAsync(director.Id)).StatusCode.Should().Be(204);
        (await users.RevokeSessionsAsync(teacher.Id)).StatusCode.Should().Be(403);
        (await users.RevokeSessionsAsync(admin.Id)).StatusCode.Should().Be(400);

        (await Auth(context).RefreshAsync(directorLogin.RefreshToken)).StatusCode.Should().Be(401);
        (await Auth(context).LoginAsync(new LoginRequestDto(director.Email, "Admin@123"))).IsSuccess.Should().BeTrue();
    }
}

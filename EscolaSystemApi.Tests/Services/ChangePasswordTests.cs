using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Application.Validators.Auth;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class ChangePasswordTests
{
    private static (AppDbContext Context, AuthService Service, User Admin) Setup()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        return (context, new AuthService(new UnitOfWork(context), JwtServiceMock.Create(), context), admin);
    }

    [Fact]
    public async Task WithCorrectCurrentPassword_ChangesAndLogsInWithNewOne()
    {
        var (_, service, admin) = Setup();

        (await service.ChangePasswordAsync(admin.Id, new ChangePasswordDto("Admin@123", "Nova@1234"))).IsSuccess.Should().BeTrue();

        (await service.LoginAsync(new LoginRequestDto(admin.Email, "Nova@1234"))).IsSuccess.Should().BeTrue();
        (await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"))).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task WithWrongCurrentPassword_Returns400AndKeepsPassword()
    {
        var (_, service, admin) = Setup();

        var result = await service.ChangePasswordAsync(admin.Id, new ChangePasswordDto("Errada@123", "Nova@1234"));

        // 400, não 401: o front não pode tratar como sessão expirada
        result.StatusCode.Should().Be(400);
        (await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task RepeatedWrongCurrentPassword_LocksAccountLikeLogin()
    {
        var (_, service, admin) = Setup();
        for (var i = 0; i < AuthService.MaxFailedLoginAttempts; i++)
            await service.ChangePasswordAsync(admin.Id, new ChangePasswordDto("Errada@123", "Nova@1234"));

        (await service.ChangePasswordAsync(admin.Id, new ChangePasswordDto("Admin@123", "Nova@1234"))).StatusCode.Should().Be(429);
    }

    [Fact]
    public async Task SamePasswordAsCurrent_IsRefused()
    {
        var (_, service, admin) = Setup();

        var result = await service.ChangePasswordAsync(admin.Id, new ChangePasswordDto("Admin@123", "Admin@123"));

        result.StatusCode.Should().Be(400);
        result.Error.Should().Contain("diferente");
    }

    [Fact]
    public async Task ResetOfOwnPassword_IsRefused()
    {
        var (_, service, admin) = Setup();

        var result = await service.ResetPasswordAsync(new ResetPasswordDto(admin.Email, "Nova@1234"), admin.Id);

        result.StatusCode.Should().Be(400);
        result.Error.Should().Contain("change-password");
    }

    [Fact]
    public void Validator_RequiresCurrentAndStrongNewPassword()
    {
        var validator = new ChangePasswordDtoValidator();

        validator.Validate(new ChangePasswordDto("", "Nova@1234")).IsValid.Should().BeFalse();
        validator.Validate(new ChangePasswordDto("Admin@123", "fraca")).IsValid.Should().BeFalse();
        validator.Validate(new ChangePasswordDto("Admin@123", "Nova@1234")).IsValid.Should().BeTrue();
    }
}

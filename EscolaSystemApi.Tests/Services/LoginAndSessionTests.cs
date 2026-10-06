using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class LoginAndSessionTests
{
    private static AuthService CreateAuthService(AppDbContext context)
    {
        return new AuthService(new UnitOfWork(context), JwtServiceMock.Create(), context);
    }

    private static async Task FailLoginsAsync(AuthService service, string email, int times)
    {
        for (var i = 0; i < times; i++)
            await service.LoginAsync(new LoginRequestDto(email, "Errada@123"));
    }

    // ---------- Bloqueio por conta ----------

    [Fact]
    public async Task Login_FiveWrongPasswords_LocksAccount()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var service = CreateAuthService(context);

        await FailLoginsAsync(service, admin.Email, AuthService.MaxFailedLoginAttempts);
        var result = await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));

        result.StatusCode.Should().Be(429);
        result.Error.Should().Contain("bloqueada");
    }

    [Fact]
    public async Task Login_FewerThanLimit_StillLogsIn()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var service = CreateAuthService(context);

        await FailLoginsAsync(service, admin.Email, AuthService.MaxFailedLoginAttempts - 1);
        var result = await service.LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));

        result.IsSuccess.Should().BeTrue();
        context.Users.Single(u => u.Id == admin.Id).FailedLoginAttempts.Should().Be(0);
    }

    [Fact]
    public async Task Login_AfterLockoutExpires_LogsIn()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        admin.LockoutEndsAt = DateTime.UtcNow.AddMinutes(-1);
        context.SaveChanges();

        var result = await CreateAuthService(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));

        result.IsSuccess.Should().BeTrue();
        context.Users.Single(u => u.Id == admin.Id).LockoutEndsAt.Should().BeNull();
    }

    [Fact]
    public async Task Login_LockoutIsPerAccount()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var service = CreateAuthService(context);

        await FailLoginsAsync(service, admin.Email, AuthService.MaxFailedLoginAttempts);
        var result = await service.LoginAsync(new LoginRequestDto(teacher.Email, "Admin@123"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_UnlocksAccount()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        var service = CreateAuthService(context);
        await FailLoginsAsync(service, admin.Email, AuthService.MaxFailedLoginAttempts);

        await service.ResetPasswordAsync(new ResetPasswordDto(admin.Email, "Nova@1234"), admin.Id);
        var result = await service.LoginAsync(new LoginRequestDto(admin.Email, "Nova@1234"));

        result.IsSuccess.Should().BeTrue();
    }

    // ---------- Sessão ----------

    [Fact]
    public async Task Session_ActiveUserWithSameRoleAndSchool_IsValid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);

        var valid = await new SessionValidator(context).IsValidAsync(teacher.Id, JwtServiceMock.CreateSession(context, teacher.Id).Id, "Teacher", school.Id);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task Session_AdminWithoutSchool_IsValid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);

        var valid = await new SessionValidator(context).IsValidAsync(admin.Id, JwtServiceMock.CreateSession(context, admin.Id).Id, "Admin", null);

        valid.Should().BeTrue();
    }

    [Fact]
    public async Task Session_DeactivatedUser_IsInvalid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        teacher.IsActive = false;
        context.SaveChanges();

        var valid = await new SessionValidator(context).IsValidAsync(teacher.Id, JwtServiceMock.CreateSession(context, teacher.Id).Id, "Teacher", school.Id);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task Session_RoleChanged_IsInvalid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);

        var valid = await new SessionValidator(context).IsValidAsync(teacher.Id, JwtServiceMock.CreateSession(context, teacher.Id).Id, "Director", school.Id);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task Session_SchoolChanged_IsInvalid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var other = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);

        var valid = await new SessionValidator(context).IsValidAsync(teacher.Id, JwtServiceMock.CreateSession(context, teacher.Id).Id, "Teacher", other.Id);

        valid.Should().BeFalse();
    }

    [Fact]
    public async Task Session_SchoolDeactivated_IsInvalid()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        school.IsActive = false;
        context.SaveChanges();

        var valid = await new SessionValidator(context).IsValidAsync(teacher.Id, JwtServiceMock.CreateSession(context, teacher.Id).Id, "Teacher", school.Id);

        valid.Should().BeFalse();
    }
}

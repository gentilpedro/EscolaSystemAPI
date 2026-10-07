using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class LockoutVisibilityTests
{
    private static AuthService Auth(AppDbContext context) => new(new UnitOfWork(context), JwtServiceMock.Create(), context);

    private static UserService Users(AppDbContext context, Guid actorId, string role, Guid? schoolId = null) =>
        new(new UnitOfWork(context), context, new CurrentUserServiceMock(actorId, role, schoolId), CpfEncryptionHelper.Create());

    private static async Task LockAsync(AppDbContext context, string email)
    {
        for (var i = 0; i < AuthService.MaxFailedLoginAttempts; i++)
            await Auth(context).LoginAsync(new LoginRequestDto(email, "Errada@123"));
    }

    [Fact]
    public async Task LockedDirector_ShowsInListWithLockedUntilAndInFilter()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        await LockAsync(context, director.Email);
        var service = Users(context, admin.Id, "Admin");

        var all = await service.GetAllAsync(new PagedQuery());
        var locked = await service.GetAllAsync(new PagedQuery(), locked: true);
        var unlocked = await service.GetAllAsync(new PagedQuery(), locked: false);

        all.Data!.Items.Single(u => u.Id == director.Id).LockedUntil.Should().BeAfter(DateTime.UtcNow);
        all.Data.Items.Single(u => u.Id == admin.Id).LockedUntil.Should().BeNull();
        locked.Data!.Items.Select(u => u.Id).Should().BeEquivalentTo([director.Id]);
        unlocked.Data!.Items.Select(u => u.Id).Should().BeEquivalentTo([admin.Id]);
    }

    [Fact]
    public async Task ExpiredLockout_IsNotShownAsLocked()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        director.LockoutEndsAt = DateTime.UtcNow.AddMinutes(-1);
        context.SaveChanges();

        var result = await Users(context, admin.Id, "Admin").GetAllAsync(new PagedQuery(), locked: true);

        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task AdminUnlocks_DirectorLogsInWithSamePassword()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        await LockAsync(context, director.Email);
        (await Auth(context).LoginAsync(new LoginRequestDto(director.Email, "Admin@123"))).StatusCode.Should().Be(429);

        var result = await Users(context, admin.Id, "Admin").UnlockAsync(director.Id);

        result.StatusCode.Should().Be(204);
        (await Auth(context).LoginAsync(new LoginRequestDto(director.Email, "Admin@123"))).IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Unlock_OnlyAdminAndOnlyForAdminsAndDirectors()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        await LockAsync(context, teacher.Email);

        (await Users(context, admin.Id, "Admin").UnlockAsync(teacher.Id)).StatusCode.Should().Be(403);
        (await Users(context, director.Id, "Director", school.Id).UnlockAsync(teacher.Id)).StatusCode.Should().Be(403);
        (await Users(context, admin.Id, "Admin").UnlockAsync(Guid.NewGuid())).StatusCode.Should().Be(404);
        context.Users.Single(u => u.Id == teacher.Id).LockoutEndsAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AdminStats_CountsLockedAdminsAndDirectorsOnly()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        await LockAsync(context, director.Email);
        await LockAsync(context, teacher.Email);

        var stats = await new DashboardService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Admin")).GetAdminStatsAsync();

        stats.Data!.LockedAccounts.Should().Be(1);
    }
}

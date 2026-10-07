using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class LastAccessTests
{
    private static AuthService Auth(AppDbContext context) => new(new UnitOfWork(context), JwtServiceMock.Create(), context);

    private static UserService Users(AppDbContext context, Guid actorId, string role, Guid? schoolId = null) =>
        new(new UnitOfWork(context), context, new CurrentUserServiceMock(actorId, role, schoolId), CpfEncryptionHelper.Create());

    [Fact]
    public async Task Admin_SeesLastAccess_NullWhenNeverLoggedIn()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));

        var items = (await Users(context, admin.Id, "Admin").GetAllAsync(new PagedQuery())).Data!.Items.ToList();

        items.Single(u => u.Id == admin.Id).LastAccessAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        items.Single(u => u.Id == director.Id).LastAccessAt.Should().BeNull();
        (await Users(context, admin.Id, "Admin").GetByIdAsync(admin.Id)).Data!.LastAccessAt.Should().NotBeNull();
    }

    [Fact]
    public async Task LastAccess_FollowsTheMostRecentSessionUse()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var admin = DbContextHelper.CreateAdminUser(context);
        await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        var sessions = context.UserSessions.OrderBy(s => s.CreatedAt).ToList();
        sessions[0].LastUsedAt = DateTime.UtcNow.AddDays(-10);
        sessions[1].LastUsedAt = DateTime.UtcNow.AddDays(-2);
        context.SaveChanges();

        var dto = (await Users(context, admin.Id, "Admin").GetByIdAsync(admin.Id)).Data!;

        dto.LastAccessAt.Should().BeCloseTo(DateTime.UtcNow.AddDays(-2), TimeSpan.FromMinutes(1));
    }

    [Fact]
    public async Task SortByLastAccess_PutsNeverFirstThenOldest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = DbContextHelper.CreateAdminUser(context);
        var never = DbContextHelper.CreateDirectorUser(context, school.Id);
        var otherSchool = DbContextHelper.CreateSchool(context);
        var old = DbContextHelper.CreateDirectorUser(context, otherSchool.Id);
        old.Email = "antigo@test.com";
        context.SaveChanges();
        await Auth(context).LoginAsync(new LoginRequestDto(admin.Email, "Admin@123"));
        await Auth(context).LoginAsync(new LoginRequestDto(old.Email, "Admin@123"));
        context.UserSessions.Single(s => s.UserId == old.Id).LastUsedAt = DateTime.UtcNow.AddDays(-30);
        context.SaveChanges();

        var ordered = (await Users(context, admin.Id, "Admin").GetAllAsync(new PagedQuery(), sort: "lastAccess")).Data!.Items
            .Select(u => u.Id).ToList();

        ordered.Should().Equal(never.Id, old.Id, admin.Id);
    }

    [Fact]
    public async Task Director_DoesNotReceiveLastAccess()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        await Auth(context).LoginAsync(new LoginRequestDto(teacher.Email, "Admin@123"));

        var items = (await Users(context, director.Id, "Director", school.Id).GetAllAsync(new PagedQuery())).Data!.Items;

        items.Should().OnlyContain(u => u.LastAccessAt == null);
    }
}

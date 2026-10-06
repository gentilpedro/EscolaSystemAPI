using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class LastAdminTests
{
    // Dois admins: o "outro" age sobre o "alvo"
    private static (AppDbContext Context, UserService Service, User Target, User Other) Setup()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var target = DbContextHelper.CreateAdminUser(context);
        var other = new User { Name = "Outro Admin", Email = "outro.admin@test.com", PasswordHash = "x", RoleId = RoleIds.Admin, IsActive = true };
        context.Users.Add(other);
        context.SaveChanges();
        var service = new UserService(new UnitOfWork(context), context, new CurrentUserServiceMock(other.Id, "Admin"), CpfEncryptionHelper.Create());
        return (context, service, target, other);
    }

    private static UpdateUserDto Update(User u, int roleId, bool isActive, Guid? schoolId = null) => new(u.Name, u.Email, roleId, schoolId, isActive);

    [Fact]
    public async Task DeactivatingAnAdmin_WhileAnotherRemains_IsAllowed()
    {
        var (_, service, target, _) = Setup();

        (await service.DeleteAsync(target.Id)).StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task DeactivatingTheLastActiveAdmin_IsRefused()
    {
        var (context, service, target, other) = Setup();
        // O admin que age é o único outro; desativado, sobra só o alvo
        other.IsActive = false;
        context.SaveChanges();

        (await service.DeleteAsync(target.Id)).StatusCode.Should().Be(409);
        (await service.UpdateAsync(target.Id, Update(target, RoleIds.Admin, isActive: false))).StatusCode.Should().Be(409);
        context.Users.Single(u => u.Id == target.Id).IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task DemotingTheLastActiveAdminToDirector_IsRefused()
    {
        var (context, service, target, other) = Setup();
        other.IsActive = false;
        context.SaveChanges();
        var school = DbContextHelper.CreateSchool(context);

        var result = await service.UpdateAsync(target.Id, Update(target, RoleIds.Director, isActive: true, school.Id));

        result.StatusCode.Should().Be(409);
        result.Error.Should().Contain("pelo menos um administrador");
    }

    [Fact]
    public async Task EditingTheLastAdminKeepingRoleAndActive_IsAllowed()
    {
        var (context, service, target, other) = Setup();
        other.IsActive = false;
        context.SaveChanges();

        (await service.UpdateAsync(target.Id, Update(target, RoleIds.Admin, isActive: true) with { Name = "Novo Nome" })).IsSuccess.Should().BeTrue();
    }
}

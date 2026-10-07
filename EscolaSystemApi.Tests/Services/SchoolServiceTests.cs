using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class SchoolServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllSchools()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateSchool(context);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlyOwnSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateSchool(context);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new SchoolService(uow, currentUser, context);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
        result.Data!.Items.First().Id.Should().Be(school.Id);
    }

    [Fact]
    public async Task CreateAsync_Admin_CreatesSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        var dto = new CreateSchoolDto("Escola Nova", "Rua X", "11999999999", "nova@escola.com");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Name.Should().Be("Escola Nova");
    }

    [Fact]
    public async Task CreateAsync_DuplicateEmail_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        var dto = new CreateSchoolDto("Escola", "Rua X", "11999999999", "escola@test.com");
        await service.CreateAsync(dto);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task GetByIdAsync_Director_CannotAccessOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new SchoolService(uow, currentUser, context);

        var result = await service.GetByIdAsync(school2.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }
    [Fact]
    public async Task UpdateAsync_Admin_UpdatesSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        var dto = new UpdateSchoolDto("Nome Atualizado", "Rua Nova", "11888888888", "novo@email.com", true);
        var result = await service.UpdateAsync(school.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Nome Atualizado");
    }

    [Fact]
    public async Task UpdateAsync_Director_CannotUpdateOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new SchoolService(uow, currentUser, context);

        var dto = new UpdateSchoolDto("Nome", "Rua", "11999999999", "email@test.com", true);
        var result = await service.UpdateAsync(school2.Id, dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeleteAsync_Admin_DeletesSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        var result = await service.DeleteAsync(school.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new SchoolService(uow, currentUser, context);

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task GetByIdAsync_Admin_GetsActiveUserCountButDirectorDoesNot()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateDirectorUser(context, school.Id);
        DbContextHelper.CreateTeacherUser(context, school.Id);
        var inactive = DbContextHelper.CreateParentUser(context, school.Id);
        inactive.IsActive = false;
        context.SaveChanges();

        var asAdmin = await new SchoolService(new UnitOfWork(context), new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), context)
            .GetByIdAsync(school.Id);
        var asDirector = await new SchoolService(new UnitOfWork(context), new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id), context)
            .GetByIdAsync(school.Id);

        asAdmin.Data!.ActiveUsers.Should().Be(2);
        asDirector.Data!.ActiveUsers.Should().BeNull();
    }
}

using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Classes;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class ClassServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllClasses()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateClass(context, school.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new ClassService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlyOwnSchoolClasses()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateClass(context, school1.Id);
        DbContextHelper.CreateClass(context, school2.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new ClassService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_Director_CannotCreateInOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new ClassService(uow, context, currentUser);

        var dto = new CreateClassDto("Turma X", 2025, school2.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeleteAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new ClassService(uow, context, currentUser);

        var result = await service.DeleteAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateAsync_Admin_UpdatesClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new ClassService(uow, context, currentUser);

        var dto = new UpdateClassDto("Turma Atualizada", 2026, school.Id, true);
        var result = await service.UpdateAsync(cls.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Turma Atualizada");
    }

    [Fact]
    public async Task UpdateAsync_Director_CannotUpdateClassFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new ClassService(uow, context, currentUser);

        var dto = new UpdateClassDto("Turma", 2025, school2.Id, true);
        var result = await service.UpdateAsync(cls.Id, dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }
}
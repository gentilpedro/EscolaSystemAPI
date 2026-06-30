using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.PendingWorks;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class PendingWorkServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllWorks()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreatePendingWork(context, student.Id, cls.Id);
        DbContextHelper.CreatePendingWork(context, student.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new PendingWorkService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ValidData_CreatesPendingWork()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new PendingWorkService(uow, context, currentUser);

        var dto = new CreatePendingWorkDto(student.Id, cls.Id, "Trabalho de Matemática", "Resolver exercícios", DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Title.Should().Be("Trabalho de Matemática");
    }

    [Fact]
    public async Task MarkAsDeliveredAsync_ValidWork_MarksAsDelivered()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var work = DbContextHelper.CreatePendingWork(context, student.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new PendingWorkService(uow, context, currentUser);

        var result = await service.MarkAsDeliveredAsync(work.Id);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsDelivered.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsDeliveredAsync_AlreadyDelivered_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var work = DbContextHelper.CreatePendingWork(context, student.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new PendingWorkService(uow, context, currentUser);

        await service.MarkAsDeliveredAsync(work.Id);
        var result = await service.MarkAsDeliveredAsync(work.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_Teacher_CannotCreateInOtherClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls2.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls1.Id);

        var currentUser = new CurrentUserServiceMock(teacher.Id, "Teacher", school.Id);
        var service = new PendingWorkService(uow, context, currentUser);

        var dto = new CreatePendingWorkDto(student.Id, cls2.Id, "Trabalho", "Descrição", DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task CreateAsync_StudentNotInClass_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls1.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new PendingWorkService(uow, context, currentUser);

        var dto = new CreatePendingWorkDto(student.Id, cls2.Id, "Trabalho", "Desc", DateOnly.FromDateTime(DateTime.Today.AddDays(7)));
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAllAsync_Student_ReturnsOnlyOwnWorks()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreatePendingWork(context, student1.Id, cls.Id);
        DbContextHelper.CreatePendingWork(context, student2.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Student", studentId: student1.Id);
        var service = new PendingWorkService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }
}
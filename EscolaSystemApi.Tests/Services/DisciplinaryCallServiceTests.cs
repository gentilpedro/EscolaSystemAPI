using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class DisciplinaryCallServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllCalls()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        DbContextHelper.CreateDisciplinaryCall(context, student.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAsync_ValidData_CreatesCall()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new CreateDisciplinaryCallDto(student.Id, "Comportamento inadequado em sala");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Description.Should().Be("Comportamento inadequado em sala");
    }

    [Fact]
    public async Task ApproveAsync_PendingCall_ApprovesCall()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);

        var currentUser = new CurrentUserServiceMock(director.Id, "Director", school.Id);
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new ResolveCallDto("Chamado aprovado após análise");
        var result = await service.ApproveAsync(call.Id, director.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(DisciplinaryCallStatus.Approved);
    }

    [Fact]
    public async Task RejectAsync_PendingCall_RejectsCall()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);

        var currentUser = new CurrentUserServiceMock(director.Id, "Director", school.Id);
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new ResolveCallDto("Chamado rejeitado por falta de evidências");
        var result = await service.RejectAsync(call.Id, director.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Status.Should().Be(DisciplinaryCallStatus.Rejected);
    }

    [Fact]
    public async Task ApproveAsync_AlreadyResolved_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);

        var currentUser = new CurrentUserServiceMock(director.Id, "Director", school.Id);
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new ResolveCallDto("Resolução");
        await service.ApproveAsync(call.Id, director.Id, dto);
        var result = await service.ApproveAsync(call.Id, director.Id, dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }
    [Fact]
    public async Task UpdateAsync_PendingCall_UpdatesDescription()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new UpdateDisciplinaryCallDto("Descrição atualizada");
        var result = await service.UpdateAsync(call.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Description.Should().Be("Descrição atualizada");
    }

    [Fact]
    public async Task UpdateAsync_ResolvedCall_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        var currentUser = new CurrentUserServiceMock(director.Id, "Director", school.Id);
        var service = new DisciplinaryCallService(uow, context, currentUser);

        await service.ApproveAsync(call.Id, director.Id, new ResolveCallDto("Aprovado"));
        var result = await service.UpdateAsync(call.Id, new UpdateDisciplinaryCallDto("Tentativa de edição"));

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task CreateAsync_Teacher_NotOfStudentClass_ReturnsForbidden()
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
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var dto = new CreateDisciplinaryCallDto(student.Id, "Comportamento inadequado");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetAllAsync_Student_ReturnsOnlyOwnCalls()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateDisciplinaryCall(context, student1.Id);
        DbContextHelper.CreateDisciplinaryCall(context, student2.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Student", studentId: student1.Id);
        var service = new DisciplinaryCallService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

}
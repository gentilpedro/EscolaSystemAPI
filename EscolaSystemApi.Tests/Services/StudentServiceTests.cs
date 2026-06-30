using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Students;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class StudentServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_ReturnsAllStudents()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateStudent(context, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlySchoolStudents()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school1.Id);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        DbContextHelper.CreateStudent(context, cls1.Id);
        DbContextHelper.CreateStudent(context, cls2.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new StudentService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_ValidData_CreatesStudent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var dto = new CreateStudentDto("Aluno Novo", "aluno@test.com", "MAT001", new DateOnly(2005, 1, 1), cls.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Name.Should().Be("Aluno Novo");
    }

    [Fact]
    public async Task CreateAsync_DuplicateRegistration_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var dto = new CreateStudentDto("Aluno", "aluno@test.com", "MAT001", new DateOnly(2005, 1, 1), cls.Id);
        await service.CreateAsync(dto);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateAsync_Director_CannotCreateInOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new StudentService(uow, context, currentUser);

        var dto = new CreateStudentDto("Aluno", "aluno@test.com", "MAT001", new DateOnly(2005, 1, 1), cls2.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateAsync_Admin_UpdatesStudent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var dto = new UpdateStudentDto("Nome Atualizado", "novo@email.com", student.Registration, student.BirthDate, cls.Id, true);
        var result = await service.UpdateAsync(student.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Name.Should().Be("Nome Atualizado");
    }

    [Fact]
    public async Task DeleteAsync_Admin_DeletesStudent()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var result = await service.DeleteAsync(student.Id);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(204);
    }

    [Fact]
    public async Task DeleteAsync_Director_CannotDeleteStudentFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var student = DbContextHelper.CreateStudent(context, cls2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new StudentService(uow, context, currentUser);

        var result = await service.DeleteAsync(student.Id);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new StudentService(uow, context, currentUser);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task CreateAsync_Director_CannotCreateStudentInClassFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new StudentService(uow, context, currentUser);

        var dto = new CreateStudentDto("Aluno", "aluno@test.com", "MAT999", new DateOnly(2005, 1, 1), cls2.Id);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }
}
using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Grades;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class GradeServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_SeesNothing()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new GradeService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlySchoolGrades()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school1.Id);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls1.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls2.Id);
        DbContextHelper.CreateGrade(context, student1.Id, cls1.Id);
        DbContextHelper.CreateGrade(context, student2.Id, cls2.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new GradeService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetAllAsync_Teacher_ReturnsOnlyOwnClassGrades()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls1.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls2.Id);
        DbContextHelper.CreateGrade(context, student1.Id, cls1.Id);
        DbContextHelper.CreateGrade(context, student2.Id, cls2.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls1.Id);

        var currentUser = new CurrentUserServiceMock(teacher.Id, "Teacher", school.Id);
        var service = new GradeService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_ValidData_CreatesGrade()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new GradeService(uow, context, currentUser);

        var dto = new CreateGradeDto(student.Id, cls.Id, "Matemática", 9.5m, "1° Trimestre");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.Subject.Should().Be("Matemática");
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
        var service = new GradeService(uow, context, currentUser);

        var dto = new CreateGradeDto(student.Id, cls2.Id, "Português", 8.0m, "1° Trimestre");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_Returns404()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new GradeService(uow, context, currentUser);

        var result = await service.GetByIdAsync(Guid.NewGuid());

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task UpdateAsync_Director_UpdatesGrade()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var grade = DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new GradeService(uow, context, currentUser);

        var dto = new UpdateGradeDto("Português", 7.5m, "2° Trimestre");
        var result = await service.UpdateAsync(grade.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.Subject.Should().Be("Português");
    }

    [Fact]
    public async Task UpdateAsync_Teacher_CannotUpdateGradeFromOtherClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls2.Id);
        var grade = DbContextHelper.CreateGrade(context, student.Id, cls2.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls1.Id);
        var currentUser = new CurrentUserServiceMock(teacher.Id, "Teacher", school.Id);
        var service = new GradeService(uow, context, currentUser);

        var dto = new UpdateGradeDto("Português", 7.5m, "2° Trimestre");
        var result = await service.UpdateAsync(grade.Id, dto);

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
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new GradeService(uow, context, currentUser);

        var dto = new CreateGradeDto(student.Id, cls2.Id, "Matemática", 9.0m, "1° Trimestre");
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAllAsync_Student_ReturnsOnlyOwnGrades()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student1.Id, cls.Id);
        DbContextHelper.CreateGrade(context, student2.Id, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Student", studentId: student1.Id);
        var service = new GradeService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

}
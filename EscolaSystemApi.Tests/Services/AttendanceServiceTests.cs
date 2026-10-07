using EscolaSystemApi.Common;
using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class AttendanceServiceTests
{
    [Fact]
    public async Task GetAllAsync_Admin_SeesNothing()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateAttendance(context, student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));
        DbContextHelper.CreateAttendance(context, student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today.AddDays(-1)));

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Admin");
        var service = new AttendanceService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_Director_ReturnsOnlySchoolAttendances()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school1.Id);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls1.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls2.Id);
        DbContextHelper.CreateAttendance(context, student1.Id, cls1.Id, DateOnly.FromDateTime(DateTime.Today));
        DbContextHelper.CreateAttendance(context, student2.Id, cls2.Id, DateOnly.FromDateTime(DateTime.Today));

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

    [Fact]
    public async Task CreateAsync_ValidData_CreatesAttendance()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var dto = new CreateAttendanceDto(student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today), true, null);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeTrue();
        result.StatusCode.Should().Be(201);
        result.Data!.IsPresent.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_DuplicateAttendance_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var date = DateOnly.FromDateTime(DateTime.Today);

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var dto = new CreateAttendanceDto(student.Id, cls.Id, date, true, null);
        await service.CreateAsync(dto);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(409);
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
        var service = new AttendanceService(uow, context, currentUser);

        var dto = new CreateAttendanceDto(student.Id, cls2.Id, DateOnly.FromDateTime(DateTime.Today), true, null);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateAsync_Director_UpdatesAttendance()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var attendance = DbContextHelper.CreateAttendance(context, student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));
        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var dto = new UpdateAttendanceDto(false, "Faltou sem justificativa");
        var result = await service.UpdateAsync(attendance.Id, dto);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsPresent.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_Teacher_CannotUpdateAttendanceFromOtherClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls2.Id);
        var attendance = DbContextHelper.CreateAttendance(context, student.Id, cls2.Id, DateOnly.FromDateTime(DateTime.Today));
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls1.Id);
        var currentUser = new CurrentUserServiceMock(teacher.Id, "Teacher", school.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var dto = new UpdateAttendanceDto(false, "Faltou");
        var result = await service.UpdateAsync(attendance.Id, dto);

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
        var service = new AttendanceService(uow, context, currentUser);

        // Aluno está na cls1 mas tentando registrar chamada na cls2
        var dto = new CreateAttendanceDto(student.Id, cls2.Id, DateOnly.FromDateTime(DateTime.Today), true, null);
        var result = await service.CreateAsync(dto);

        result.IsSuccess.Should().BeFalse();
        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task GetAllAsync_Student_ReturnsOnlyOwnAttendances()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var uow = new UnitOfWork(context);
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student1 = DbContextHelper.CreateStudent(context, cls.Id);
        var student2 = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateAttendance(context, student1.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));
        DbContextHelper.CreateAttendance(context, student2.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today));

        var currentUser = new CurrentUserServiceMock(Guid.NewGuid(), "Student", studentId: student1.Id);
        var service = new AttendanceService(uow, context, currentUser);

        var result = await service.GetAllAsync(new PagedQuery());

        result.IsSuccess.Should().BeTrue();
        result.Data!.Items.Should().HaveCount(1);
    }

}
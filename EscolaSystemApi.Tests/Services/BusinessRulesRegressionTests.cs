using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.DTOs.Classes;
using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Application.DTOs.Students;
using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

// Regressões das falhas de permissão e regra de negócio encontradas na validação da API
public class BusinessRulesRegressionTests
{
    private static User CreateUser(AppDbContext context, int roleId, Guid? schoolId, Guid? studentId = null)
    {
        var user = new User
        {
            Name = $"Usuário {roleId}",
            Email = $"user_{Guid.NewGuid()}@test.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Senha@123"),
            RoleId = roleId,
            SchoolId = schoolId,
            StudentId = studentId,
            IsActive = true
        };
        context.Users.Add(user);
        context.SaveChanges();
        return user;
    }

    private static AuthService CreateAuthService(AppDbContext context)
    {
        return new AuthService(new UnitOfWork(context), JwtServiceMock.Create(), context);
    }

    private static UserService CreateUserService(AppDbContext context, ICurrentUserService currentUser)
        => new(new UnitOfWork(context), context, currentUser, CpfEncryptionHelper.Create());

    // ---------- Auth ----------

    [Fact]
    public async Task Register_NonAdminRole_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();

        var result = await CreateAuthService(context)
            .RegisterAsync(new RegisterRequestDto("Diretor", "d@test.com", "Senha@123", RoleIds.Director));

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ResetPassword_TeacherCannotResetAdminPassword()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = CreateUser(context, RoleIds.Admin, null);
        var teacher = CreateUser(context, RoleIds.Teacher, school.Id);

        var result = await CreateAuthService(context)
            .ResetPasswordAsync(new ResetPasswordDto(admin.Email, "Nova@1234"), teacher.Id);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ResetPassword_AdminResetsOnlyAdminsAndDirectors()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var admin = CreateUser(context, RoleIds.Admin, null);
        var director = CreateUser(context, RoleIds.Director, school.Id);
        var teacher = CreateUser(context, RoleIds.Teacher, school.Id);
        var service = CreateAuthService(context);

        (await service.ResetPasswordAsync(new ResetPasswordDto(director.Email, "Nova@1234"), admin.Id)).IsSuccess.Should().BeTrue();
        // As pessoas da escola ficam com a direção
        (await service.ResetPasswordAsync(new ResetPasswordDto(teacher.Email, "Nova@1234"), admin.Id)).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ResetPassword_DirectorCannotResetUserFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var director = CreateUser(context, RoleIds.Director, school1.Id);
        var teacher = CreateUser(context, RoleIds.Teacher, school2.Id);

        var result = await CreateAuthService(context)
            .ResetPasswordAsync(new ResetPasswordDto(teacher.Email, "Nova@1234"), director.Id);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ResetPassword_DirectorCanResetTeacherOfOwnSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var director = CreateUser(context, RoleIds.Director, school.Id);
        var teacher = CreateUser(context, RoleIds.Teacher, school.Id);

        var result = await CreateAuthService(context)
            .ResetPasswordAsync(new ResetPasswordDto(teacher.Email, "Nova@1234"), director.Id);

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task ResetPassword_DirectorCannotResetAnotherDirector()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var director = CreateUser(context, RoleIds.Director, school.Id);
        var otherDirector = CreateUser(context, RoleIds.Director, school.Id);

        var result = await CreateAuthService(context)
            .ResetPasswordAsync(new ResetPasswordDto(otherDirector.Email, "Nova@1234"), director.Id);

        result.StatusCode.Should().Be(403);
    }

    // ---------- Usuários ----------

    [Fact]
    public async Task UpdateUser_DirectorCannotMoveUserToOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var teacher = CreateUser(context, RoleIds.Teacher, school1.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.UpdateAsync(teacher.Id,
            new UpdateUserDto(teacher.Name, teacher.Email, RoleIds.Teacher, school2.Id, true));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateUser_DirectorCannotPromoteToDirector()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = CreateUser(context, RoleIds.Teacher, school.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.UpdateAsync(teacher.Id,
            new UpdateUserDto(teacher.Name, teacher.Email, RoleIds.Director, school.Id, true));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UpdateUser_DuplicateEmail_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher1 = CreateUser(context, RoleIds.Teacher, school.Id);
        var teacher2 = CreateUser(context, RoleIds.Teacher, school.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.UpdateAsync(teacher2.Id,
            new UpdateUserDto(teacher2.Name, teacher1.Email, RoleIds.Teacher, school.Id, true));

        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateUser_CpfIsEncryptedAndReturnedDecrypted()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.CreateAsync(
            new CreateUserDto("Prof", "prof@test.com", "Senha@123", RoleIds.Teacher, school.Id, Cpf: "123.456.789-00"));

        result.Data!.Cpf.Should().Be("123.456.789-00");
        var stored = context.Users.Single(u => u.Email == "prof@test.com");
        stored.CpfEncrypted.Should().NotBeNullOrEmpty().And.NotContain("123");
    }

    [Fact]
    public async Task AssignStudent_ParentAndStudentFromDifferentSchools_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var parent = CreateUser(context, RoleIds.Parent, school1.Id);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Admin"));

        var result = await service.AssignStudentAsync(parent.Id, student.Id);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task AssignStudent_DirectorCannotLinkParentFromOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var parent = CreateUser(context, RoleIds.Parent, school2.Id);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.AssignStudentAsync(parent.Id, student.Id);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task UnassignClass_DirectorCannotChangeOtherSchoolLink()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var teacher = CreateUser(context, RoleIds.Teacher, school2.Id);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.UnassignClassAsync(teacher.Id, cls.Id);

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AssignClass_ClassFromOtherSchool_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var teacher = CreateUser(context, RoleIds.Teacher, school1.Id);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        var service = CreateUserService(context, new CurrentUserServiceMock(Guid.NewGuid(), "Admin"));

        var result = await service.AssignClassAsync(teacher.Id, cls.Id);

        result.StatusCode.Should().Be(400);
    }

    // ---------- Turmas, alunos e escolas ----------

    [Fact]
    public async Task UpdateClass_DirectorCannotMoveClassToOtherSchool()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school1.Id);
        var service = new ClassService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.UpdateAsync(cls.Id, new UpdateClassDto(cls.Name, cls.Year, school2.Id, true));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DeleteClass_WithStudents_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateStudent(context, cls.Id);
        var service = new ClassService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.DeleteAsync(cls.Id);

        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task GetClasses_StudentSeesOwnClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var service = new ClassService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Student", school.Id, student.Id));

        var result = await service.GetAllAsync(new PagedQuery());

        result.Data!.Items.Should().ContainSingle(c => c.Id == cls.Id);
    }

    [Fact]
    public async Task GetClasses_ParentSeesChildClass()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var parent = CreateUser(context, RoleIds.Parent, school.Id);
        DbContextHelper.AssignParentToStudent(context, parent.Id, student.Id);
        var service = new ClassService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(parent.Id, "Parent", school.Id));

        var result = await service.GetAllAsync(new PagedQuery());

        result.Data!.Items.Should().ContainSingle(c => c.Id == cls.Id);
    }

    [Fact]
    public async Task UpdateStudent_TransferToOtherSchoolClass_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school1.Id);
        var cls2 = DbContextHelper.CreateClass(context, school2.Id);
        var student = DbContextHelper.CreateStudent(context, cls1.Id);
        var service = new StudentService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.UpdateAsync(student.Id,
            new UpdateStudentDto(student.Name, student.Email, student.Registration, student.BirthDate, cls2.Id, true));

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task DeleteStudent_WithGrades_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        var service = new StudentService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.DeleteAsync(student.Id);

        result.StatusCode.Should().Be(409);
        context.Grades.Should().HaveCount(1);
    }

    [Fact]
    public async Task DeleteSchool_WithClasses_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        DbContextHelper.CreateClass(context, school.Id);
        var service = new SchoolService(new UnitOfWork(context), new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), context);

        var result = await service.DeleteAsync(school.Id);

        result.StatusCode.Should().Be(409);
    }

    // ---------- Trabalhos pendentes ----------

    [Fact]
    public async Task MarkAsDelivered_StudentCannotDeliverAnotherStudentsWork()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var owner = DbContextHelper.CreateStudent(context, cls.Id);
        var other = DbContextHelper.CreateStudent(context, cls.Id);
        var work = DbContextHelper.CreatePendingWork(context, owner.Id, cls.Id);
        var service = new PendingWorkService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Student", school.Id, other.Id));

        var result = await service.MarkAsDeliveredAsync(work.Id);

        result.StatusCode.Should().Be(404);
        context.PendingWorks.Single().IsDelivered.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsDelivered_StudentDeliversOwnWork()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var work = DbContextHelper.CreatePendingWork(context, student.Id, cls.Id);
        var service = new PendingWorkService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Student", school.Id, student.Id));

        var result = await service.MarkAsDeliveredAsync(work.Id);

        result.IsSuccess.Should().BeTrue();
        result.Data!.IsDelivered.Should().BeTrue();
    }

    // ---------- Chamada em lote ----------

    [Fact]
    public async Task BulkAttendance_StudentNotInClass_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls1 = DbContextHelper.CreateClass(context, school.Id);
        var cls2 = DbContextHelper.CreateClass(context, school.Id);
        var outsider = DbContextHelper.CreateStudent(context, cls2.Id);
        var service = new AttendanceService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = await service.BulkCreateAsync([new CreateAttendanceDto(outsider.Id, cls1.Id, today, true, null)]);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BulkAttendance_MixedDates_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var s1 = DbContextHelper.CreateStudent(context, cls.Id);
        var s2 = DbContextHelper.CreateStudent(context, cls.Id);
        var service = new AttendanceService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));
        var today = DateOnly.FromDateTime(DateTime.Today);

        var result = await service.BulkCreateAsync(
        [
            new CreateAttendanceDto(s1.Id, cls.Id, today, true, null),
            new CreateAttendanceDto(s2.Id, cls.Id, today.AddDays(-1), true, null)
        ]);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task BulkAttendance_DirectorOfOtherSchool_ReturnsForbidden()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school1 = DbContextHelper.CreateSchool(context);
        var school2 = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school2.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var service = new AttendanceService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Director", school1.Id));

        var result = await service.BulkCreateAsync(
            [new CreateAttendanceDto(student.Id, cls.Id, DateOnly.FromDateTime(DateTime.Today), true, null)]);

        result.StatusCode.Should().Be(403);
    }

    // ---------- Ocorrências ----------

    [Fact]
    public async Task DisciplinaryCall_OrientadorOfClass_CreatesAndRecordsAuthor()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var orientador = CreateUser(context, RoleIds.Orientador, school.Id);
        context.OrientadorClasses.Add(new OrientadorClass { OrientadorId = orientador.Id, ClassId = cls.Id });
        context.SaveChanges();
        var service = new DisciplinaryCallService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(orientador.Id, "Orientador", school.Id));

        var result = await service.CreateAsync(new CreateDisciplinaryCallDto(student.Id, "Briga no recreio"));

        result.StatusCode.Should().Be(201);
        result.Data!.CreatedById.Should().Be(orientador.Id);
        result.Data.ClassId.Should().Be(cls.Id);
    }

    [Fact]
    public async Task DisciplinaryCall_OrientadorOfOtherClassCannotResolve()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var call = DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        var orientador = CreateUser(context, RoleIds.Orientador, school.Id);
        var service = new DisciplinaryCallService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(orientador.Id, "Orientador", school.Id));

        var result = await service.ApproveAsync(call.Id, orientador.Id, new ResolveCallDto("ok"));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task DisciplinaryCall_FilterByStatus()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        var resolved = DbContextHelper.CreateDisciplinaryCall(context, student.Id);
        resolved.Status = DisciplinaryCallStatus.Approved;
        context.SaveChanges();
        var service = new DisciplinaryCallService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.GetAllAsync(new PagedQuery(), new DisciplinaryCallFilter(Status: DisciplinaryCallStatus.Pending));

        result.Data!.Items.Should().ContainSingle();
    }

    // ---------- Paginação ----------

    [Theory]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-3, 10000, 1, 500)]
    [InlineData(2, 50, 2, 50)]
    public void PagedQuery_ClampsInvalidValues(int page, int pageSize, int expectedPage, int expectedPageSize)
    {
        var query = new PagedQuery(page, pageSize);

        query.Page.Should().Be(expectedPage);
        query.PageSize.Should().Be(expectedPageSize);
    }
}

using EscolaSystemApi.Application.DTOs.Grades;
using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Application.Validators.Grades;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class BusinessRulesIssue10Tests
{
    // ---------- Edição de perfil pelo admin ----------

    [Fact]
    public async Task AdminUpdate_ChangingDirectorToTeacher_ReturnsForbidden()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);
        var service = new UserService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), CpfEncryptionHelper.Create());

        var result = await service.UpdateAsync(director.Id,
            new UpdateUserDto(director.Name, director.Email, RoleIds.Teacher, school.Id, true));

        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AdminUpdate_Teacher_IsForbidden()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var service = new UserService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), CpfEncryptionHelper.Create());

        var result = await service.UpdateAsync(teacher.Id,
            new UpdateUserDto(teacher.Name, teacher.Email, RoleIds.Teacher, school.Id, false));

        // As pessoas da escola são da direção: o administrador nem desativa
        result.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task AdminUpdate_PromotingTeacherToDirector_IsForbidden()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        var service = new UserService(new UnitOfWork(context), context,
            new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), CpfEncryptionHelper.Create());

        var result = await service.UpdateAsync(teacher.Id,
            new UpdateUserDto(teacher.Name, teacher.Email, RoleIds.Director, school.Id, true));

        result.StatusCode.Should().Be(403);
    }

    // ---------- Nota duplicada e período ----------

    [Fact]
    public async Task CreateGrade_SameSubjectAndPeriod_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id); // Matemática, 1° Trimestre
        var service = new GradeService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        // Mesma matéria com outra caixa e o bimestre com "º" em vez de "°"
        var result = await service.CreateAsync(new CreateGradeDto(student.Id, cls.Id, " matemática ", 9, "1º Trimestre"));

        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreateGrade_OtherPeriod_IsAllowed()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        var service = new GradeService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.CreateAsync(new CreateGradeDto(student.Id, cls.Id, "Matemática", 7, "2º Trimestre"));

        result.StatusCode.Should().Be(201);
    }

    [Fact]
    public async Task UpdateGrade_IntoExistingSubjectAndPeriod_ReturnsConflict()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        var service = new GradeService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));
        var other = await service.CreateAsync(new CreateGradeDto(student.Id, cls.Id, "Matemática", 7, "2º Trimestre"));

        var result = await service.UpdateAsync(other.Data!.Id, new UpdateGradeDto("Matemática", 7, "1º Trimestre"));

        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task UpdateGrade_SameRecordSamePeriod_IsAllowed()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var student = DbContextHelper.CreateStudent(context, cls.Id);
        var grade = DbContextHelper.CreateGrade(context, student.Id, cls.Id);
        var service = new GradeService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var result = await service.UpdateAsync(grade.Id, new UpdateGradeDto("Matemática", 9.5m, "1º Trimestre"));

        result.IsSuccess.Should().BeTrue();
        result.Data!.Value.Should().Be(9.5m);
    }

    [Theory]
    [InlineData("1º Trimestre", true)]
    [InlineData("1° Trimestre", true)]
    [InlineData("3º Trimestre", true)]
    [InlineData("Recuperação", true)]
    [InlineData("Final", true)]
    [InlineData("4º Trimestre", false)]
    [InlineData("Semestre 1", false)]
    public void GradeValidator_AcceptsOnlySchoolPeriods(string period, bool valid)
    {
        var result = new CreateGradeValidator().Validate(new CreateGradeDto(Guid.NewGuid(), Guid.NewGuid(), "Matemática", 8, period));

        result.IsValid.Should().Be(valid);
    }

    // ---------- Filtro de alunos ativos ----------

    [Fact]
    public async Task GetStudents_IsActiveFilter_ExcludesInactive()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        DbContextHelper.CreateStudent(context, cls.Id);
        var inactive = DbContextHelper.CreateStudent(context, cls.Id);
        inactive.IsActive = false;
        context.SaveChanges();
        var service = new StudentService(new UnitOfWork(context), context, new CurrentUserServiceMock(Guid.NewGuid(), "Director", school.Id));

        var active = await service.GetAllAsync(new PagedQuery(), cls.Id, isActive: true);
        var all = await service.GetAllAsync(new PagedQuery(), cls.Id);

        active.Data!.Items.Should().ContainSingle();
        all.Data!.Items.Should().HaveCount(2);
    }
}

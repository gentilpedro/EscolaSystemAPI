using EscolaSystemApi.Application.DTOs.PendingWorks;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class ClassAssignmentTests
{
    private static readonly DateOnly DueDate = DateOnly.FromDateTime(DateTime.Today.AddDays(7));

    // Turma com dois alunos ativos e um desativado, e o professor dela
    private static (AppDbContext Context, Class Class, User Teacher, List<Student> Active) Setup()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var active = new List<Student> { DbContextHelper.CreateStudent(context, cls.Id), DbContextHelper.CreateStudent(context, cls.Id) };
        var inactive = DbContextHelper.CreateStudent(context, cls.Id);
        inactive.IsActive = false;
        context.SaveChanges();
        var teacher = DbContextHelper.CreateTeacherUser(context, school.Id);
        DbContextHelper.AssignTeacherToClass(context, teacher.Id, cls.Id);
        return (context, cls, teacher, active);
    }

    private static PendingWorkService ServiceFor(AppDbContext context, User user, string role)
        => new(new UnitOfWork(context), context, new CurrentUserServiceMock(user.Id, role, user.SchoolId));

    private static CreateClassAssignmentDto NewAssignment(Guid classId) => new(classId, "Resumo do capítulo 4", "Uma página.", DueDate);

    [Fact]
    public async Task CreateForClass_Teacher_CreatesOneWorkPerActiveStudentWithSameAssignment()
    {
        var (context, cls, teacher, active) = Setup();

        var result = await ServiceFor(context, teacher, "Teacher").CreateForClassAsync(NewAssignment(cls.Id));

        result.StatusCode.Should().Be(201);
        result.Data!.StudentCount.Should().Be(2);
        context.PendingWorks.Should().HaveCount(2)
            .And.OnlyContain(w => w.AssignmentId == result.Data.AssignmentId)
            .And.Contain(w => w.StudentId == active[0].Id)
            .And.Contain(w => w.StudentId == active[1].Id);
    }

    [Fact]
    public async Task CreateForClass_TeacherOfAnotherClass_IsForbidden()
    {
        var (context, cls, _, _) = Setup();
        var other = DbContextHelper.CreateTeacherUser(context, cls.SchoolId);

        var result = await ServiceFor(context, other, "Teacher").CreateForClassAsync(NewAssignment(cls.Id));

        result.StatusCode.Should().Be(403);
        context.PendingWorks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateForClass_WithoutActiveStudents_ReturnsBadRequest()
    {
        var context = DbContextHelper.CreateInMemoryContext();
        var school = DbContextHelper.CreateSchool(context);
        var cls = DbContextHelper.CreateClass(context, school.Id);
        var director = DbContextHelper.CreateDirectorUser(context, school.Id);

        var result = await ServiceFor(context, director, "Director").CreateForClassAsync(NewAssignment(cls.Id));

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task UpdateAssignment_ChangesAllStudentsAndKeepsDeliveries()
    {
        var (context, cls, teacher, active) = Setup();
        var service = ServiceFor(context, teacher, "Teacher");
        var created = (await service.CreateForClassAsync(NewAssignment(cls.Id))).Data!;
        var delivered = context.PendingWorks.First(w => w.StudentId == active[0].Id);
        delivered.IsDelivered = true;
        delivered.DeliveredAt = DateTime.UtcNow;
        context.SaveChanges();

        var newDue = DueDate.AddDays(3);
        var result = await service.UpdateAssignmentAsync(created.AssignmentId, new UpdateAssignmentDto("Resumo corrigido", "Duas páginas.", newDue));

        result.IsSuccess.Should().BeTrue();
        result.Data!.DeliveredCount.Should().Be(1);
        context.PendingWorks.Should().OnlyContain(w => w.Title == "Resumo corrigido" && w.Description == "Duas páginas." && w.DueDate == newDue);
        context.PendingWorks.Single(w => w.StudentId == active[0].Id).IsDelivered.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAssignment_RemovesOnlyThatAssignment()
    {
        var (context, cls, teacher, _) = Setup();
        var service = ServiceFor(context, teacher, "Teacher");
        var first = (await service.CreateForClassAsync(NewAssignment(cls.Id))).Data!;
        // Mesmo título, descrição e prazo: antes viravam um trabalho só na tela
        var second = (await service.CreateForClassAsync(NewAssignment(cls.Id))).Data!;

        var result = await service.DeleteAssignmentAsync(first.AssignmentId);

        result.StatusCode.Should().Be(204);
        context.PendingWorks.Should().HaveCount(2).And.OnlyContain(w => w.AssignmentId == second.AssignmentId);
    }

    [Fact]
    public async Task UpdateAndDelete_DirectorOfAnotherSchool_AreForbidden()
    {
        var (context, cls, teacher, _) = Setup();
        var created = (await ServiceFor(context, teacher, "Teacher").CreateForClassAsync(NewAssignment(cls.Id))).Data!;
        var otherSchool = DbContextHelper.CreateSchool(context);
        var outsider = ServiceFor(context, DbContextHelper.CreateDirectorUser(context, otherSchool.Id), "Director");

        (await outsider.UpdateAssignmentAsync(created.AssignmentId, new UpdateAssignmentDto("x", "y", DueDate))).StatusCode.Should().Be(403);
        (await outsider.DeleteAssignmentAsync(created.AssignmentId)).StatusCode.Should().Be(403);
        context.PendingWorks.Should().HaveCount(2).And.OnlyContain(w => w.Title == "Resumo do capítulo 4");
    }

    [Fact]
    public async Task UpdateAssignment_Unknown_ReturnsNotFound()
    {
        var (context, _, teacher, _) = Setup();

        var result = await ServiceFor(context, teacher, "Teacher").UpdateAssignmentAsync(Guid.NewGuid(), new UpdateAssignmentDto("x", "y", DueDate));

        result.StatusCode.Should().Be(404);
    }

    [Fact]
    public async Task SingleCreate_GetsItsOwnAssignment()
    {
        var (context, cls, teacher, active) = Setup();
        var service = ServiceFor(context, teacher, "Teacher");

        var a = await service.CreateAsync(new CreatePendingWorkDto(active[0].Id, cls.Id, "T", "D", DueDate));
        var b = await service.CreateAsync(new CreatePendingWorkDto(active[1].Id, cls.Id, "T", "D", DueDate));

        a.Data!.AssignmentId.Should().NotBe(b.Data!.AssignmentId);
    }
}

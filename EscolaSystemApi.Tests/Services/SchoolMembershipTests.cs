using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class SchoolMembershipTests
{
    /// <summary>
    /// Escola 1 (turma A, aluno A) com o diretor 1 e um professor vinculado à turma A;
    /// escola 2 (turma B, aluno B) com o diretor 2. Os vínculos com a escola são criados como no cadastro.
    /// </summary>
    private sealed class World
    {
        public AppDbContext Context { get; } = DbContextHelper.CreateInMemoryContext();
        public School School1 { get; }
        public School School2 { get; }
        public Class ClassA { get; }
        public Class ClassB { get; }
        public Student StudentA { get; }
        public Student StudentB { get; }
        public User Director1 { get; }
        public User Director2 { get; }
        public User Teacher { get; }

        public World()
        {
            School1 = DbContextHelper.CreateSchool(Context);
            School2 = DbContextHelper.CreateSchool(Context);
            ClassA = DbContextHelper.CreateClass(Context, School1.Id);
            ClassB = DbContextHelper.CreateClass(Context, School2.Id);
            StudentA = DbContextHelper.CreateStudent(Context, ClassA.Id);
            StudentB = DbContextHelper.CreateStudent(Context, ClassB.Id);
            Director1 = DbContextHelper.CreateDirectorUser(Context, School1.Id);
            Director2 = DbContextHelper.CreateDirectorUser(Context, School2.Id);
            Director2.Email = "diretor2@test.com";
            Teacher = DbContextHelper.CreateTeacherUser(Context, School1.Id);
            foreach (var u in new[] { Director1, Director2, Teacher })
                Context.SchoolMemberships.Add(new SchoolMembership { UserId = u.Id, SchoolId = u.SchoolId!.Value });
            DbContextHelper.AssignTeacherToClass(Context, Teacher.Id, ClassA.Id);
            Context.SaveChanges();
        }

        public UserService Users(User actor, string role) =>
            new(new UnitOfWork(Context), Context, new CurrentUserServiceMock(actor.Id, role, actor.SchoolId), CpfEncryptionHelper.Create());

        public UserService Admin() =>
            new(new UnitOfWork(Context), Context, new CurrentUserServiceMock(Guid.NewGuid(), "Admin"), CpfEncryptionHelper.Create());

        public AccessScope TeacherScope() => new(Context, new CurrentUserServiceMock(Teacher.Id, "Teacher", Teacher.SchoolId));

        public UpdateUserDto TeacherUpdate(Guid? schoolId, int roleId = RoleIds.Teacher) =>
            new(Teacher.Name, Teacher.Email, roleId, schoolId, true);
    }

    // ---------- O vazamento da issue ----------

    [Fact]
    public async Task ChangingTeacherSchoolByEdit_IsRefusedAndAccessStaysTheSame()
    {
        var w = new World();

        var result = await w.Admin().UpdateAsync(w.Teacher.Id, w.TeacherUpdate(w.School2.Id));

        result.StatusCode.Should().Be(400);
        w.TeacherScope().Classes().Select(c => c.Id).Should().BeEquivalentTo([w.ClassA.Id]);
    }

    // ---------- Professor em duas escolas ----------

    [Fact]
    public async Task TeacherAddedToSecondSchool_SeesClassesOfBothSchools()
    {
        var w = new World();
        var director2 = w.Users(w.Director2, "Director");

        (await director2.AddMemberAsync(w.School2.Id, w.Teacher.Email)).IsSuccess.Should().BeTrue();
        (await director2.AssignClassAsync(w.Teacher.Id, w.ClassB.Id)).StatusCode.Should().Be(204);

        w.TeacherScope().Classes().Select(c => c.Id).Should().BeEquivalentTo([w.ClassA.Id, w.ClassB.Id]);
        var listed = await director2.GetAllAsync(new PagedQuery());
        listed.Data!.Items.Should().Contain(u => u.Id == w.Teacher.Id)
            .Which.Schools.Should().HaveCount(2);
    }

    [Fact]
    public async Task AssigningClassOfSchoolWithoutMembership_IsRefused()
    {
        var w = new World();

        var result = await w.Admin().AssignClassAsync(w.Teacher.Id, w.ClassB.Id);

        result.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task RemovedFromSecondSchool_KeepsFirstSchoolAndHistory()
    {
        var w = new World();
        var director2 = w.Users(w.Director2, "Director");
        await director2.AddMemberAsync(w.School2.Id, w.Teacher.Email);
        await director2.AssignClassAsync(w.Teacher.Id, w.ClassB.Id);

        // "Desativar" pelo diretor da escola 2 tira o professor só da escola 2
        (await director2.DeleteAsync(w.Teacher.Id)).StatusCode.Should().Be(204);

        w.Context.Users.Single(u => u.Id == w.Teacher.Id).IsActive.Should().BeTrue();
        w.TeacherScope().Classes().Select(c => c.Id).Should().BeEquivalentTo([w.ClassA.Id]);
        // Nada é apagado: o vínculo com a turma B fica encerrado, como histórico
        w.Context.TeacherClasses.IgnoreQueryFilters().Single(tc => tc.ClassId == w.ClassB.Id).EndedAt.Should().NotBeNull();
        w.Context.SchoolMemberships.Single(m => m.UserId == w.Teacher.Id && m.SchoolId == w.School2.Id).EndedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task RemovedFromOnlySchool_DeactivatesAccountAndRevokesSessions()
    {
        var w = new World();
        var auth = new AuthService(new UnitOfWork(w.Context), JwtServiceMock.Create(), w.Context);
        var login = await auth.LoginAsync(new LoginRequestDto(w.Teacher.Email, "Admin@123"));

        (await w.Users(w.Director1, "Director").RemoveMemberAsync(w.School1.Id, w.Teacher.Id)).StatusCode.Should().Be(204);

        w.Context.Users.Single(u => u.Id == w.Teacher.Id).IsActive.Should().BeFalse();
        w.TeacherScope().Classes().Should().BeEmpty();
        (await auth.RefreshAsync(login.Data!.RefreshToken)).StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task DirectorOfAnotherSchool_CannotRemoveOrAddInSchoolThatIsNotHis()
    {
        var w = new World();
        var director2 = w.Users(w.Director2, "Director");

        (await director2.RemoveMemberAsync(w.School1.Id, w.Teacher.Id)).StatusCode.Should().Be(403);
        (await director2.AddMemberAsync(w.School1.Id, w.Teacher.Email)).StatusCode.Should().Be(403);
        (await director2.DeleteAsync(w.Teacher.Id)).StatusCode.Should().Be(403);
    }

    // ---------- Escola principal desativada ----------

    private static async Task TeacherInBothSchoolsWithFirstDeactivated(World w)
    {
        var director2 = w.Users(w.Director2, "Director");
        await director2.AddMemberAsync(w.School2.Id, w.Teacher.Email);
        await director2.AssignClassAsync(w.Teacher.Id, w.ClassB.Id);
        w.School1.IsActive = false;
        w.Context.SaveChanges();
    }

    [Fact]
    public async Task PrimarySchoolDeactivated_TeacherLogsInThroughTheOtherSchool()
    {
        var w = new World();
        await TeacherInBothSchoolsWithFirstDeactivated(w);

        var login = await new AuthService(new UnitOfWork(w.Context), JwtServiceMock.Create(), w.Context)
            .LoginAsync(new LoginRequestDto(w.Teacher.Email, "Admin@123"));

        login.IsSuccess.Should().BeTrue();
        login.Data!.User.SchoolId.Should().Be(w.School2.Id);
        w.Context.Users.Single(u => u.Id == w.Teacher.Id).SchoolId.Should().Be(w.School2.Id);
        // A turma da escola desativada some, mesmo com o vínculo ativo
        new AccessScope(w.Context, new CurrentUserServiceMock(w.Teacher.Id, "Teacher", w.School2.Id))
            .Classes().Select(c => c.Id).Should().BeEquivalentTo([w.ClassB.Id]);
    }

    [Fact]
    public async Task OnlySchoolDeactivated_LoginIsRefused()
    {
        var w = new World();
        w.School1.IsActive = false;
        w.Context.SaveChanges();

        var login = await new AuthService(new UnitOfWork(w.Context), JwtServiceMock.Create(), w.Context)
            .LoginAsync(new LoginRequestDto(w.Teacher.Email, "Admin@123"));

        login.StatusCode.Should().Be(401);
        login.Error.Should().Contain("Escola desativada");
    }

    [Fact]
    public async Task PrimarySchoolDeactivatedDuringSession_RefreshMovesToTheOtherSchool()
    {
        var w = new World();
        var auth = new AuthService(new UnitOfWork(w.Context), JwtServiceMock.Create(), w.Context);
        var login = await auth.LoginAsync(new LoginRequestDto(w.Teacher.Email, "Admin@123"));
        await TeacherInBothSchoolsWithFirstDeactivated(w);
        var validator = new SessionValidator(w.Context);
        var sessionId = w.Context.UserSessions.Single(s => s.UserId == w.Teacher.Id).Id;

        // O token antigo (escola 1) deixa de valer; a renovação troca para a escola 2
        (await validator.IsValidAsync(w.Teacher.Id, sessionId, "Teacher", w.School1.Id)).Should().BeFalse();
        var refreshed = await auth.RefreshAsync(login.Data!.RefreshToken);

        refreshed.IsSuccess.Should().BeTrue();
        refreshed.Data!.User.SchoolId.Should().Be(w.School2.Id);
        (await validator.IsValidAsync(w.Teacher.Id, sessionId, "Teacher", w.School2.Id)).Should().BeTrue();
    }

    // ---------- Vínculos com turma encerram, não apagam ----------

    [Fact]
    public async Task UnassignAndAssignAgain_ReusesTheSameLink()
    {
        var w = new World();
        var director1 = w.Users(w.Director1, "Director");

        (await director1.UnassignClassAsync(w.Teacher.Id, w.ClassA.Id)).StatusCode.Should().Be(204);
        w.TeacherScope().Classes().Should().BeEmpty();
        w.Context.TeacherClasses.IgnoreQueryFilters().Should().ContainSingle(tc => tc.EndedAt != null);

        (await director1.AssignClassAsync(w.Teacher.Id, w.ClassA.Id)).StatusCode.Should().Be(204);
        w.Context.TeacherClasses.IgnoreQueryFilters().Should().ContainSingle(tc => tc.EndedAt == null);
    }

    // ---------- Responsável com filhos em escolas diferentes ----------

    [Fact]
    public async Task ParentInTwoSchools_RemovalEndsOnlyThatSchoolsChildren()
    {
        var w = new World();
        var parent = DbContextHelper.CreateParentUser(w.Context, w.School1.Id);
        w.Context.SchoolMemberships.Add(new SchoolMembership { UserId = parent.Id, SchoolId = w.School1.Id });
        w.Context.SaveChanges();
        var director1 = w.Users(w.Director1, "Director");
        var director2 = w.Users(w.Director2, "Director");
        await director1.AssignStudentAsync(parent.Id, w.StudentA.Id);

        // Na escola 2, o responsável precisa entrar na escola antes de ser vinculado ao aluno dela
        (await director2.AssignStudentAsync(parent.Id, w.StudentB.Id)).StatusCode.Should().Be(403);
        await director2.AddMemberAsync(w.School2.Id, parent.Email);
        (await director2.AssignStudentAsync(parent.Id, w.StudentB.Id)).StatusCode.Should().Be(204);

        await director2.RemoveMemberAsync(w.School2.Id, parent.Id);

        w.Context.ParentStudents.Select(ps => ps.StudentId).Should().BeEquivalentTo([w.StudentA.Id]);
        w.Context.Users.Single(u => u.Id == parent.Id).IsActive.Should().BeTrue();
    }

    // ---------- Regras de entrada ----------

    [Fact]
    public async Task AddMember_RulesForRoleStatusAndDuplicate()
    {
        var w = new World();
        var director2 = w.Users(w.Director2, "Director");

        (await director2.AddMemberAsync(w.School2.Id, "ninguem@test.com")).StatusCode.Should().Be(404);
        (await director2.AddMemberAsync(w.School2.Id, w.Director1.Email)).StatusCode.Should().Be(400);

        await director2.AddMemberAsync(w.School2.Id, w.Teacher.Email);
        (await director2.AddMemberAsync(w.School2.Id, w.Teacher.Email)).StatusCode.Should().Be(409);

        w.Teacher.IsActive = false;
        w.Context.SaveChanges();
        (await w.Users(w.Director2, "Director").AddMemberAsync(w.School2.Id, w.Teacher.Email)).StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task PromotingTeacherInTwoSchoolsToDirector_IsRefused()
    {
        var w = new World();
        await w.Users(w.Director2, "Director").AddMemberAsync(w.School2.Id, w.Teacher.Email);

        var result = await w.Admin().UpdateAsync(w.Teacher.Id, w.TeacherUpdate(w.School1.Id, RoleIds.Director));

        result.StatusCode.Should().Be(409);
    }

    [Fact]
    public async Task CreatingSchoolUser_CreatesMembership()
    {
        var w = new World();

        var created = await w.Users(w.Director1, "Director").CreateAsync(
            new CreateUserDto("Nova Professora", "nova@test.com", "Senha@123", RoleIds.Teacher, w.School1.Id));

        w.Context.SchoolMemberships.Should().Contain(m => m.UserId == created.Data!.Id && m.SchoolId == w.School1.Id && m.EndedAt == null);
    }
}

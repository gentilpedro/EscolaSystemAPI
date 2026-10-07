using EscolaSystemApi.Application.Services;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using EscolaSystemApi.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace EscolaSystemApi.Tests.Services;

public class AccessScopeTests
{
    /// <summary>
    /// Duas escolas. Na escola 1, turmas A e B com um aluno cada; na escola 2, a turma C.
    /// Professor e orientador vinculados só à turma A; responsável e conta de aluno ligados ao aluno da turma A.
    /// Cada aluno tem uma nota, uma chamada, um trabalho e um chamado.
    /// </summary>
    private sealed class World
    {
        public AppDbContext Context { get; } = DbContextHelper.CreateInMemoryContext();
        public School School1 { get; }
        public Class ClassA { get; }
        public Class ClassB { get; }
        public Class ClassC { get; }
        public Student StudentA { get; }
        public Student StudentB { get; }
        public Student StudentC { get; }
        public User Director { get; }
        public User Teacher { get; }
        public User Orientador { get; }
        public User Parent { get; }

        public World()
        {
            School1 = DbContextHelper.CreateSchool(Context);
            var school2 = DbContextHelper.CreateSchool(Context);
            ClassA = DbContextHelper.CreateClass(Context, School1.Id);
            ClassB = DbContextHelper.CreateClass(Context, School1.Id);
            ClassC = DbContextHelper.CreateClass(Context, school2.Id);
            StudentA = DbContextHelper.CreateStudent(Context, ClassA.Id);
            StudentB = DbContextHelper.CreateStudent(Context, ClassB.Id);
            StudentC = DbContextHelper.CreateStudent(Context, ClassC.Id);

            Director = DbContextHelper.CreateDirectorUser(Context, School1.Id);
            Teacher = DbContextHelper.CreateTeacherUser(Context, School1.Id);
            DbContextHelper.AssignTeacherToClass(Context, Teacher.Id, ClassA.Id);
            Orientador = DbContextHelper.CreateTeacherUser(Context, School1.Id);
            Context.OrientadorClasses.Add(new OrientadorClass { OrientadorId = Orientador.Id, ClassId = ClassA.Id });
            Parent = DbContextHelper.CreateParentUser(Context, School1.Id);
            DbContextHelper.AssignParentToStudent(Context, Parent.Id, StudentA.Id);
            Context.SaveChanges();

            foreach (var s in new[] { StudentA, StudentB, StudentC })
            {
                DbContextHelper.CreateGrade(Context, s.Id, s.ClassId);
                DbContextHelper.CreateAttendance(Context, s.Id, s.ClassId, new DateOnly(2026, 3, 2));
                DbContextHelper.CreatePendingWork(Context, s.Id, s.ClassId);
                DbContextHelper.CreateDisciplinaryCall(Context, s.Id);
            }
        }

        public AccessScope ScopeFor(string role) => role switch
        {
            "Admin" => new(Context, new CurrentUserServiceMock(Guid.NewGuid(), "Admin")),
            "Director" => new(Context, new CurrentUserServiceMock(Director.Id, "Director", School1.Id)),
            "Teacher" => new(Context, new CurrentUserServiceMock(Teacher.Id, "Teacher", School1.Id)),
            "Orientador" => new(Context, new CurrentUserServiceMock(Orientador.Id, "Orientador", School1.Id)),
            "Student" => new(Context, new CurrentUserServiceMock(Guid.NewGuid(), "Student", School1.Id, StudentA.Id)),
            "Parent" => new(Context, new CurrentUserServiceMock(Parent.Id, "Parent", School1.Id)),
            _ => new(Context, new CurrentUserServiceMock(Guid.NewGuid(), role))
        };

        // Alunos que cada perfil deve enxergar, pela regra do sistema. O administrador cuida do sistema e não vê nada
        public Guid[] ExpectedStudents(string role) => role switch
        {
            "Director" => [StudentA.Id, StudentB.Id],
            "Teacher" or "Orientador" or "Student" or "Parent" => [StudentA.Id],
            _ => []
        };

        public Guid[] ExpectedClasses(string role) => role switch
        {
            "Director" => [ClassA.Id, ClassB.Id],
            "Teacher" or "Orientador" or "Student" or "Parent" => [ClassA.Id],
            _ => []
        };
    }

    public static TheoryData<string> Roles => new() { "Admin", "Director", "Teacher", "Orientador", "Student", "Parent", "Desconhecido" };

    [Theory]
    [MemberData(nameof(Roles))]
    public void Classes_FollowRole(string role)
    {
        var world = new World();
        world.ScopeFor(role).Classes().Select(c => c.Id).Should().BeEquivalentTo(world.ExpectedClasses(role));
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void Students_FollowRole(string role)
    {
        var world = new World();
        world.ScopeFor(role).Students().Select(s => s.Id).Should().BeEquivalentTo(world.ExpectedStudents(role));
    }

    [Theory]
    [MemberData(nameof(Roles))]
    public void SchoolRecords_FollowRole(string role)
    {
        var world = new World();
        var scope = world.ScopeFor(role);
        var expected = world.ExpectedStudents(role);

        scope.Grades().Select(g => g.StudentId).Should().BeEquivalentTo(expected);
        scope.Attendances().Select(a => a.StudentId).Should().BeEquivalentTo(expected);
        scope.PendingWorks().Select(p => p.StudentId).Should().BeEquivalentTo(expected);
        scope.DisciplinaryCalls().Select(d => d.StudentId).Should().BeEquivalentTo(expected);
    }

    [Fact]
    public void Records_FollowClassWhereTheyWereRecorded_NotCurrentClassOfStudent()
    {
        var world = new World();
        // O aluno da turma A muda para a turma B: a nota lançada na turma A continua com o professor da turma A
        world.StudentA.ClassId = world.ClassB.Id;
        world.Context.SaveChanges();

        var teacher = world.ScopeFor("Teacher");

        teacher.Grades().Select(g => g.StudentId).Should().Contain(world.StudentA.Id);
        teacher.Students().Should().BeEmpty();
    }
}

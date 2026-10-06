using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;

namespace EscolaSystemApi.Application.Services;

/// <summary>
/// Quem enxerga o quê, num lugar só. Cada método devolve só os registros que o usuário logado pode ver:
/// Admin tudo; Diretor a própria escola; Professor e Orientador as turmas a que estão vinculados;
/// Aluno o próprio registro; Responsável os alunos vinculados a ele. Perfil desconhecido não vê nada.
/// Os services partem daqui e acrescentam Include, filtros e paginação.
/// </summary>
public sealed class AccessScope(AppDbContext context, ICurrentUserService currentUser)
{
    // As propriedades de currentUser ficam dentro das expressões: só são lidas para o perfil que as usa

    public IQueryable<Class> Classes() => currentUser.Role switch
    {
        "Admin" => context.Classes,
        "Director" => context.Classes.Where(c => c.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Classes.Where(c => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == c.Id)),
        "Orientador" => context.Classes.Where(c => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == c.Id)),
        "Student" => context.Classes.Where(c => context.Students
            .Any(s => s.Id == currentUser.StudentId && s.ClassId == c.Id)),
        "Parent" => context.Classes.Where(c => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.Student.ClassId == c.Id)),
        _ => context.Classes.Where(_ => false)
    };

    public IQueryable<Student> Students() => currentUser.Role switch
    {
        "Admin" => context.Students,
        "Director" => context.Students.Where(s => s.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Students.Where(s => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
        "Orientador" => context.Students.Where(s => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == s.ClassId)),
        "Student" => context.Students.Where(s => s.Id == currentUser.StudentId),
        "Parent" => context.Students.Where(s => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == s.Id)),
        _ => context.Students.Where(_ => false)
    };

    // Notas, chamadas e trabalhos seguem a turma em que o registro foi lançado (não a turma atual do aluno)

    public IQueryable<Grade> Grades() => currentUser.Role switch
    {
        "Admin" => context.Grades,
        "Director" => context.Grades.Where(g => g.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Grades.Where(g => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == g.ClassId)),
        "Orientador" => context.Grades.Where(g => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == g.ClassId)),
        "Student" => context.Grades.Where(g => g.StudentId == currentUser.StudentId),
        "Parent" => context.Grades.Where(g => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == g.StudentId)),
        _ => context.Grades.Where(_ => false)
    };

    public IQueryable<Attendance> Attendances() => currentUser.Role switch
    {
        "Admin" => context.Attendances,
        "Director" => context.Attendances.Where(a => a.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Attendances.Where(a => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == a.ClassId)),
        "Orientador" => context.Attendances.Where(a => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == a.ClassId)),
        "Student" => context.Attendances.Where(a => a.StudentId == currentUser.StudentId),
        "Parent" => context.Attendances.Where(a => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == a.StudentId)),
        _ => context.Attendances.Where(_ => false)
    };

    public IQueryable<PendingWork> PendingWorks() => currentUser.Role switch
    {
        "Admin" => context.PendingWorks,
        "Director" => context.PendingWorks.Where(p => p.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.PendingWorks.Where(p => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == p.ClassId)),
        "Orientador" => context.PendingWorks.Where(p => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == p.ClassId)),
        "Student" => context.PendingWorks.Where(p => p.StudentId == currentUser.StudentId),
        "Parent" => context.PendingWorks.Where(p => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == p.StudentId)),
        _ => context.PendingWorks.Where(_ => false)
    };

    // Chamados seguem a turma atual do aluno
    public IQueryable<DisciplinaryCall> DisciplinaryCalls() => currentUser.Role switch
    {
        "Admin" => context.DisciplinaryCalls,
        "Director" => context.DisciplinaryCalls.Where(d => d.Student.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.DisciplinaryCalls.Where(d => context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == d.Student.ClassId)),
        "Orientador" => context.DisciplinaryCalls.Where(d => context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == d.Student.ClassId)),
        "Student" => context.DisciplinaryCalls.Where(d => d.StudentId == currentUser.StudentId),
        "Parent" => context.DisciplinaryCalls.Where(d => context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == d.StudentId)),
        _ => context.DisciplinaryCalls.Where(_ => false)
    };
}

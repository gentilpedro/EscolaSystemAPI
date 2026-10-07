using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;

namespace EscolaSystemApi.Application.Services;

/// <summary>
/// Quem enxerga o quê, num lugar só. Cada método devolve só os registros que o usuário logado pode ver:
/// Diretor a própria escola; Professor e Orientador as turmas a que estão vinculados;
/// Aluno o próprio registro; Responsável os alunos vinculados a ele. O administrador cuida do sistema e não vê
/// dados das escolas; ele e qualquer perfil desconhecido não veem nada.
/// Professor, orientador e responsável não veem nada de escola desativada, mesmo com o vínculo ativo.
/// Os services partem daqui e acrescentam Include, filtros e paginação.
/// </summary>
public sealed class AccessScope(AppDbContext context, ICurrentUserService currentUser)
{
    // As propriedades de currentUser ficam dentro das expressões: só são lidas para o perfil que as usa

    public IQueryable<Class> Classes() => currentUser.Role switch
    {
        "Director" => context.Classes.Where(c => c.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Classes.Where(c => c.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == c.Id)),
        "Orientador" => context.Classes.Where(c => c.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == c.Id)),
        "Student" => context.Classes.Where(c => context.Students
            .Any(s => s.Id == currentUser.StudentId && s.ClassId == c.Id)),
        "Parent" => context.Classes.Where(c => c.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.Student.ClassId == c.Id)),
        _ => context.Classes.Where(_ => false)
    };

    public IQueryable<Student> Students() => currentUser.Role switch
    {
        "Director" => context.Students.Where(s => s.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Students.Where(s => s.Class.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == s.ClassId)),
        "Orientador" => context.Students.Where(s => s.Class.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == s.ClassId)),
        "Student" => context.Students.Where(s => s.Id == currentUser.StudentId),
        "Parent" => context.Students.Where(s => s.Class.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == s.Id)),
        _ => context.Students.Where(_ => false)
    };

    // Notas, chamadas e trabalhos seguem a turma em que o registro foi lançado (não a turma atual do aluno)

    public IQueryable<Grade> Grades() => currentUser.Role switch
    {
        "Director" => context.Grades.Where(g => g.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Grades.Where(g => g.Class.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == g.ClassId)),
        "Orientador" => context.Grades.Where(g => g.Class.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == g.ClassId)),
        "Student" => context.Grades.Where(g => g.StudentId == currentUser.StudentId),
        "Parent" => context.Grades.Where(g => g.Class.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == g.StudentId)),
        _ => context.Grades.Where(_ => false)
    };

    public IQueryable<Attendance> Attendances() => currentUser.Role switch
    {
        "Director" => context.Attendances.Where(a => a.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.Attendances.Where(a => a.Class.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == a.ClassId)),
        "Orientador" => context.Attendances.Where(a => a.Class.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == a.ClassId)),
        "Student" => context.Attendances.Where(a => a.StudentId == currentUser.StudentId),
        "Parent" => context.Attendances.Where(a => a.Class.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == a.StudentId)),
        _ => context.Attendances.Where(_ => false)
    };

    public IQueryable<PendingWork> PendingWorks() => currentUser.Role switch
    {
        "Director" => context.PendingWorks.Where(p => p.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.PendingWorks.Where(p => p.Class.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == p.ClassId)),
        "Orientador" => context.PendingWorks.Where(p => p.Class.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == p.ClassId)),
        "Student" => context.PendingWorks.Where(p => p.StudentId == currentUser.StudentId),
        "Parent" => context.PendingWorks.Where(p => p.Class.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == p.StudentId)),
        _ => context.PendingWorks.Where(_ => false)
    };

    // Chamados seguem a turma atual do aluno
    public IQueryable<DisciplinaryCall> DisciplinaryCalls() => currentUser.Role switch
    {
        "Director" => context.DisciplinaryCalls.Where(d => d.Student.Class.SchoolId == currentUser.SchoolId),
        "Teacher" => context.DisciplinaryCalls.Where(d => d.Student.Class.School.IsActive && context.TeacherClasses
            .Any(tc => tc.TeacherId == currentUser.UserId && tc.ClassId == d.Student.ClassId)),
        "Orientador" => context.DisciplinaryCalls.Where(d => d.Student.Class.School.IsActive && context.OrientadorClasses
            .Any(oc => oc.OrientadorId == currentUser.UserId && oc.ClassId == d.Student.ClassId)),
        "Student" => context.DisciplinaryCalls.Where(d => d.StudentId == currentUser.StudentId),
        "Parent" => context.DisciplinaryCalls.Where(d => d.Student.Class.School.IsActive && context.ParentStudents
            .Any(ps => ps.ParentId == currentUser.UserId && ps.StudentId == d.StudentId)),
        _ => context.DisciplinaryCalls.Where(_ => false)
    };
}

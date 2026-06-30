namespace EscolaSystemApi.Domain.Entities;

public class Class : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public int Year { get; set; }
    public Guid SchoolId { get; set; }
    public School School { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public ICollection<Student> Students { get; set; } = [];
    public ICollection<Grade> Grades { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<PendingWork> PendingWorks { get; set; } = [];
    public ICollection<TeacherClass> TeacherClasses { get; set; } = [];
    public ICollection<OrientadorClass> OrientadorClasses { get; set; } = [];
}

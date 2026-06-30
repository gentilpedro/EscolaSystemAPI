namespace EscolaSystemApi.Domain.Entities;

public class Student : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Registration { get; set; } = string.Empty;
    public DateOnly BirthDate { get; set; }
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public ICollection<Grade> Grades { get; set; } = [];
    public ICollection<Attendance> Attendances { get; set; } = [];
    public ICollection<DisciplinaryCall> DisciplinaryCalls { get; set; } = [];
    public ICollection<PendingWork> PendingWorks { get; set; } = [];
    public ICollection<ParentStudent> ParentStudents { get; set; } = [];
    public User? UserAccount { get; set; }
}
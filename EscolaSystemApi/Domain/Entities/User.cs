using EscolaSystemApi.Domain.Entities;

public class User : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public int RoleId { get; set; }
    public Role Role { get; set; } = null!;
    public Guid? SchoolId { get; set; }
    public School? School { get; set; }
    public Guid? StudentId { get; set; }
    public Student? Student { get; set; }
    public string? Cpf { get; set; }
    public string? CpfHash { get; set; }
    public string? CpfEncrypted { get; set; }
    public string? Phone { get; set; }
    public bool IsActive { get; set; } = true;
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutEndsAt { get; set; }
    public ICollection<TeacherClass> TeacherClasses { get; set; } = [];
    public ICollection<ParentStudent> ParentStudents { get; set; } = [];
    public ICollection<OrientadorClass> OrientadorClasses { get; set; } = [];
}
namespace EscolaSystemApi.Domain.Entities;

public class TeacherClass
{
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
}
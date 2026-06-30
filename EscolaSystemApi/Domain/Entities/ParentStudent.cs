namespace EscolaSystemApi.Domain.Entities;

public class ParentStudent
{
    public Guid ParentId { get; set; }
    public User Parent { get; set; } = null!;
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
}
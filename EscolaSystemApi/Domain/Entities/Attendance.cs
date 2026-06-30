namespace EscolaSystemApi.Domain.Entities;

public class Attendance : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public DateOnly Date { get; set; }
    public bool IsPresent { get; set; }
    public string? Notes { get; set; }
}

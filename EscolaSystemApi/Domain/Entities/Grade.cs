namespace EscolaSystemApi.Domain.Entities;

public class Grade : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public string Subject { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Period { get; set; } = string.Empty;
}

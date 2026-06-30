using EscolaSystemApi.Domain.Enums;

namespace EscolaSystemApi.Domain.Entities;

public class DisciplinaryCall : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    public string Description { get; set; } = string.Empty;
    public DisciplinaryCallStatus Status { get; set; } = DisciplinaryCallStatus.Pending;
    public Guid? ResolvedById { get; set; }
    public User? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? Resolution { get; set; }
}

namespace EscolaSystemApi.Domain.Entities;

public class PendingWork : BaseEntity
{
    public Guid StudentId { get; set; }
    public Student Student { get; set; } = null!;
    // Mesmo valor em todos os alunos que receberam o trabalho juntos: é o "trabalho da turma"
    public Guid AssignmentId { get; set; } = Guid.NewGuid();
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateOnly DueDate { get; set; }
    public bool IsDelivered { get; set; }
    public DateTime? DeliveredAt { get; set; }
}

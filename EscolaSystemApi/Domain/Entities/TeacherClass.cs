namespace EscolaSystemApi.Domain.Entities;

public class TeacherClass : IEndableLink
{
    public Guid TeacherId { get; set; }
    public User Teacher { get; set; } = null!;
    public Guid ClassId { get; set; }
    public Class Class { get; set; } = null!;
    // Desvincular encerra em vez de apagar: o histórico de quem lecionou na turma fica
    public DateTime? EndedAt { get; set; }
}
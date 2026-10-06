namespace EscolaSystemApi.Domain.Entities;

// Vínculo da pessoa com uma escola. Professor, orientador e responsável podem estar em várias escolas;
// sair de uma escola encerra o vínculo (EndedAt) em vez de apagar, e o histórico fica.
// CreatedAt é a data de entrada.
public class SchoolMembership : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid SchoolId { get; set; }
    public School School { get; set; } = null!;
    public DateTime? EndedAt { get; set; }
}

// Vínculos que podem ser encerrados sem apagar o registro (turma, aluno, escola)
public interface IEndableLink
{
    DateTime? EndedAt { get; set; }
}

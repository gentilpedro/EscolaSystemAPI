namespace EscolaSystemApi.Domain.Entities;

// Só o hash do refresh token fica no banco; o valor existe apenas no cookie do navegador
public class RefreshToken : BaseEntity
{
    public Guid SessionId { get; set; }
    public UserSession Session { get; set; } = null!;
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    // Preenchido quando o token é trocado por um novo (rotação); reuso depois disso indica roubo
    public DateTime? UsedAt { get; set; }
}

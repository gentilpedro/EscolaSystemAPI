namespace EscolaSystemApi.Domain.Entities;

// Uma sessão por login: o token de acesso carrega o Id dela (claim "sid") e o refresh token gira dentro dela
public class UserSession : BaseEntity
{
    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    // Renovada a cada rotação do refresh token
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    // Navegador e sistema de quem entrou (User-Agent), para a pessoa reconhecer o aparelho
    public string? UserAgent { get; set; }
    // Último login ou renovação da sessão
    public DateTime? LastUsedAt { get; set; }
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

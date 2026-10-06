using EscolaSystemApi.Domain.Entities;

namespace EscolaSystemApi.Application.Interfaces;

public interface IJwtService
{
    // Token de acesso curto, ligado à sessão (claim "sid") que pode ser revogada
    (string Token, DateTime ExpiresAt) GenerateToken(User user, Guid sessionId);
    TimeSpan RefreshTokenLifetime { get; }
}

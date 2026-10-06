using System.Security.Cryptography;
using System.Text;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

// Operações de sessão usadas por login/renovação (AuthService) e por desativação de conta (UserService)
public static class UserSessions
{
    // 256 bits aleatórios: o valor vai só para o cookie; o banco guarda o hash
    public static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');

    public static string Hash(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

    // Encerra todas as sessões do usuário (desativação, troca de senha, reuso de refresh token).
    // Não salva: quem chama decide quando gravar.
    public static async Task RevokeAllAsync(AppDbContext context, Guid userId, Guid? exceptSessionId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var sessions = await context.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.Id != exceptSessionId)
            .ToListAsync(cancellationToken);

        foreach (var session in sessions)
            session.RevokedAt = now;
    }

    public static RefreshToken AddRefreshToken(AppDbContext context, UserSession session, string token, DateTime expiresAt)
    {
        var refresh = new RefreshToken { SessionId = session.Id, TokenHash = Hash(token), ExpiresAt = expiresAt };
        context.RefreshTokens.Add(refresh);
        return refresh;
    }
}

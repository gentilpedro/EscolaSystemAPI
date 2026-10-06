using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

// Perfil, escola e sessão ficam gravados no token: sem esta checagem, sair, desativar alguém ou trocar
// seu perfil só teria efeito quando o token expirasse
public class SessionValidator(AppDbContext context) : ISessionValidator
{
    public Task<bool> IsValidAsync(Guid userId, Guid sessionId, string role, Guid? schoolId, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return context.UserSessions.AsNoTracking().AnyAsync(s =>
                s.Id == sessionId
                && s.UserId == userId
                && s.RevokedAt == null
                && s.ExpiresAt > now
                && s.User.IsActive
                && s.User.Role.Name == role
                && s.User.SchoolId == schoolId
                && (s.User.School == null || s.User.School.IsActive),
            cancellationToken);
    }
}

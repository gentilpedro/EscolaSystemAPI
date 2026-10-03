using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

// Perfil e escola ficam gravados no token: sem esta checagem, desativar alguém ou trocar
// seu perfil só teria efeito quando o token expirasse
public class SessionValidator(AppDbContext context) : ISessionValidator
{
    public Task<bool> IsValidAsync(Guid userId, string role, Guid? schoolId, CancellationToken cancellationToken = default) =>
        context.Users.AsNoTracking().AnyAsync(u =>
                u.Id == userId
                && u.IsActive
                && u.Role.Name == role
                && u.SchoolId == schoolId
                && (u.School == null || u.School.IsActive),
            cancellationToken);
}

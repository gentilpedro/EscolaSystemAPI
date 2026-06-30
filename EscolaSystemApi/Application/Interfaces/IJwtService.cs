using EscolaSystemApi.Domain.Entities;

namespace EscolaSystemApi.Application.Interfaces;

public interface IJwtService
{
    (string Token, DateTime ExpiresAt) GenerateToken(User user);
    Guid? GetUserIdFromToken(string token);
}

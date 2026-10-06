namespace EscolaSystemApi.Application.Interfaces;

public interface ISessionValidator
{
    // Confere se a sessão do token segue ativa e se o token ainda representa o usuário como ele está no banco
    Task<bool> IsValidAsync(Guid userId, Guid sessionId, string role, Guid? schoolId, CancellationToken cancellationToken = default);
}

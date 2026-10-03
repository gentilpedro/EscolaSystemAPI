namespace EscolaSystemApi.Application.Interfaces;

public interface ISessionValidator
{
    // Confere se o token ainda representa o usuário como ele está no banco
    Task<bool> IsValidAsync(Guid userId, string role, Guid? schoolId, CancellationToken cancellationToken = default);
}

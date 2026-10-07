using EscolaSystemApi.Application.DTOs.Audit;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IAuditService
{
    // Registro de atividades para o administrador, mais recentes primeiro
    Task<Result<PagedResult<AuditLogDto>>> GetAsync(PagedQuery query, DateTime? from = null, DateTime? to = null,
        string? action = null, Guid? actorId = null, Guid? targetId = null, CancellationToken cancellationToken = default);
}

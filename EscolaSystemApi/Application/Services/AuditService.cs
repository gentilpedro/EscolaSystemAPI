using EscolaSystemApi.Application.DTOs.Audit;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

public class AuditService(AppDbContext context) : IAuditService
{
    // O administrador vê o que é do sistema: escolas, administradores e diretores.
    // O que a direção faz com professores, alunos e responsáveis fica gravado, mas fora desta consulta.
    public async Task<Result<PagedResult<AuditLogDto>>> GetAsync(PagedQuery query, DateTime? from = null, DateTime? to = null,
        string? action = null, Guid? actorId = null, Guid? targetId = null, CancellationToken cancellationToken = default)
    {
        var logs = context.AuditLogs.AsNoTracking()
            .Where(l => l.TargetType == "School" || l.TargetRoleId == RoleIds.Admin || l.TargetRoleId == RoleIds.Director);

        if (from.HasValue)
            logs = logs.Where(l => l.CreatedAt >= from.Value);
        if (to.HasValue)
            logs = logs.Where(l => l.CreatedAt < to.Value);
        if (!string.IsNullOrWhiteSpace(action))
            logs = logs.Where(l => l.Action == action);
        if (actorId.HasValue)
            logs = logs.Where(l => l.ActorId == actorId.Value);
        if (targetId.HasValue)
            logs = logs.Where(l => l.TargetId == targetId.Value);

        var totalCount = await logs.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var items = await logs
            .OrderByDescending(l => l.CreatedAt)
            .Skip(query.Skip).Take(query.Take)
            .Select(l => new AuditLogDto(l.Id, l.CreatedAt, l.ActorId, l.ActorName, l.Action, l.TargetType, l.TargetId, l.TargetName, l.Details))
            .ToListAsync(cancellationToken);

        return Result<PagedResult<AuditLogDto>>.Success(new PagedResult<AuditLogDto>(items, query.Page, query.PageSize, totalCount, totalPages));
    }
}

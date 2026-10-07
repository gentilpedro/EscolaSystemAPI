using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

// Grava o registro de atividades junto com a ação. Não salva: entra no mesmo SaveChanges de quem chama.
public static class AuditTrail
{
    public static async Task RecordAsync(AppDbContext context, Guid? actorId, string action, School school,
        string? details = null, CancellationToken cancellationToken = default)
    {
        context.AuditLogs.Add(new AuditLog
        {
            ActorId = NormalizeActor(actorId),
            ActorName = await ActorNameAsync(context, actorId, cancellationToken),
            Action = action,
            TargetType = "School",
            TargetId = school.Id,
            TargetName = school.Name,
            SchoolId = school.Id,
            Details = details
        });
    }

    public static async Task RecordAsync(AppDbContext context, Guid? actorId, string action, User user,
        string? details = null, Guid? schoolId = null, CancellationToken cancellationToken = default)
    {
        context.AuditLogs.Add(new AuditLog
        {
            ActorId = NormalizeActor(actorId),
            ActorName = await ActorNameAsync(context, actorId, cancellationToken),
            Action = action,
            TargetType = "User",
            TargetId = user.Id,
            TargetName = user.Name,
            TargetRoleId = user.RoleId,
            SchoolId = schoolId ?? user.SchoolId,
            Details = details
        });
    }

    // "Nome: A → B; Perfil: Diretor → Administrador" só com o que mudou; null quando nada mudou
    public static string? Changes(params (string Label, string? Before, string? After)[] fields)
    {
        var changed = fields
            .Where(f => !string.Equals(f.Before ?? string.Empty, f.After ?? string.Empty, StringComparison.Ordinal))
            .Select(f => $"{f.Label}: {Show(f.Before)} → {Show(f.After)}")
            .ToList();
        return changed.Count == 0 ? null : string.Join("; ", changed);
    }

    // Campo sensível ou sem nome legível: registra só que mudou ("CPF alterado"), nunca o valor
    public static string? Changed(string text, bool changed) => changed ? text : null;

    public static string? Join(params string?[] parts)
    {
        var present = parts.Where(p => !string.IsNullOrWhiteSpace(p)).ToList();
        return present.Count == 0 ? null : string.Join("; ", present);
    }

    public static string RoleLabel(int roleId) => roleId switch
    {
        RoleIds.Admin => "Administrador",
        RoleIds.Director => "Diretor",
        RoleIds.Teacher => "Professor",
        RoleIds.Student => "Aluno",
        RoleIds.Parent => "Responsável",
        RoleIds.Orientador => "Orientador",
        _ => "Desconhecido"
    };

    public static string ActiveLabel(bool active) => active ? "ativo" : "inativo";

    private static string Show(string? value) => string.IsNullOrWhiteSpace(value) ? "(vazio)" : value;

    private static Guid? NormalizeActor(Guid? actorId) => actorId is null || actorId == Guid.Empty ? null : actorId;

    private static async Task<string> ActorNameAsync(AppDbContext context, Guid? actorId, CancellationToken cancellationToken)
    {
        if (NormalizeActor(actorId) is not { } id)
            return "Sistema";

        var name = await context.Users.Where(u => u.Id == id).Select(u => u.Name).FirstOrDefaultAsync(cancellationToken);
        return name ?? "Usuário removido";
    }
}

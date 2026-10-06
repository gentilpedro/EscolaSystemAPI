using System.Linq.Expressions;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

// Vínculos das pessoas com as escolas. User.SchoolId é a escola principal (a do token);
// professor, orientador e responsável podem ter vínculo ativo em outras escolas também.
public static class SchoolMembers
{
    // Pertence à escola: é a escola principal ou há vínculo ativo com ela
    public static Expression<Func<User, bool>> BelongsTo(Guid? schoolId) =>
        u => u.SchoolId == schoolId || u.SchoolMemberships.Any(m => m.SchoolId == schoolId && m.EndedAt == null);

    public static Task<bool> BelongsToAsync(AppDbContext context, Guid userId, Guid? schoolId, CancellationToken cancellationToken) =>
        context.Users.Where(u => u.Id == userId).AnyAsync(BelongsTo(schoolId), cancellationToken);

    public static async Task<List<Guid>> ActiveSchoolIdsAsync(AppDbContext context, Guid userId, CancellationToken cancellationToken) =>
        await context.SchoolMemberships
            .Where(m => m.UserId == userId && m.EndedAt == null)
            .Select(m => m.SchoolId)
            .Distinct()
            .ToListAsync(cancellationToken);

    // Garante o vínculo ativo; reabre o encerrado se houver, para não duplicar o histórico. Não salva.
    public static async Task EnsureActiveAsync(AppDbContext context, Guid userId, Guid schoolId, CancellationToken cancellationToken)
    {
        var memberships = await context.SchoolMemberships
            .Where(m => m.UserId == userId && m.SchoolId == schoolId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        if (memberships.Any(m => m.EndedAt == null))
            return;

        // Volta para a escola: um vínculo novo, com nova data de entrada; o encerrado fica como histórico
        context.SchoolMemberships.Add(new SchoolMembership { UserId = userId, SchoolId = schoolId });
    }

    /// <summary>
    /// Tira a pessoa da escola sem apagar nada: encerra o vínculo com a escola e os vínculos com as turmas
    /// e os alunos dela. Devolve as escolas em que a pessoa continua. Não salva.
    /// </summary>
    public static async Task<List<Guid>> EndAsync(AppDbContext context, User user, Guid schoolId, CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;

        foreach (var m in await context.SchoolMemberships
                     .Where(m => m.UserId == user.Id && m.SchoolId == schoolId && m.EndedAt == null)
                     .ToListAsync(cancellationToken))
            m.EndedAt = now;

        foreach (var link in await context.TeacherClasses
                     .Where(tc => tc.TeacherId == user.Id && tc.Class.SchoolId == schoolId)
                     .ToListAsync(cancellationToken))
            link.EndedAt = now;

        foreach (var link in await context.OrientadorClasses
                     .Where(oc => oc.OrientadorId == user.Id && oc.Class.SchoolId == schoolId)
                     .ToListAsync(cancellationToken))
            link.EndedAt = now;

        foreach (var link in await context.ParentStudents
                     .Where(ps => ps.ParentId == user.Id && ps.Student.Class.SchoolId == schoolId)
                     .ToListAsync(cancellationToken))
            link.EndedAt = now;

        // Escolas que continuam: os vínculos ativos, menos esta (ainda não salvos)
        var remaining = (await ActiveSchoolIdsAsync(context, user.Id, cancellationToken)).Where(id => id != schoolId).ToList();

        // A escola principal acompanha: se era esta, passa para outra em que a pessoa continua
        if (user.SchoolId == schoolId && remaining.Count > 0)
            user.SchoolId = remaining[0];

        return remaining;
    }
}

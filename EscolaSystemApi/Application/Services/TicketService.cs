using EscolaSystemApi.Application.DTOs.Tickets;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Interfaces.Repositories;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using EscolaSystemApi.Domain.Enums;
using EscolaSystemApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EscolaSystemApi.Application.Services;

/// <summary>
/// Tickets da direção para a administração do sistema. A direção abre e acompanha os da própria escola;
/// a administração vê todos, responde e muda a situação.
/// </summary>
public class TicketService(IUnitOfWork unitOfWork, AppDbContext context, ICurrentUserService currentUser) : ITicketService
{
    private bool IsAdmin => currentUser.Role == "Admin";

    public async Task<Result<PagedResult<TicketListItemDto>>> GetAllAsync(PagedQuery query, TicketFilter filter, CancellationToken cancellationToken = default)
    {
        var tickets = Scoped();
        if (filter.Status.HasValue)
            tickets = tickets.Where(t => t.Status == filter.Status.Value);
        if (filter.Type.HasValue)
            tickets = tickets.Where(t => t.Type == filter.Type.Value);
        if (filter.SchoolId.HasValue)
            tickets = tickets.Where(t => t.SchoolId == filter.SchoolId.Value);

        var totalCount = await tickets.CountAsync(cancellationToken);
        var totalPages = (int)Math.Ceiling(totalCount / (double)query.PageSize);
        var rows = await tickets
            // Última atividade primeiro: ticket novo, resposta nova ou mudança de situação
            .OrderByDescending(t => t.UpdatedAt ?? t.CreatedAt)
            .Skip(query.Skip).Take(query.Take)
            .Select(t => new
            {
                t.Id, t.Title, t.Type, t.Status, t.SchoolId, SchoolName = t.School.Name, OpenedByName = t.OpenedBy.Name,
                t.CreatedAt, LastActivityAt = t.UpdatedAt ?? t.CreatedAt, MessageCount = t.Messages.Count
            })
            .ToListAsync(cancellationToken);

        var items = rows.Select(r => new TicketListItemDto(r.Id, r.Title, r.Type, TypeName(r.Type), r.Status, StatusName(r.Status),
            r.SchoolId, r.SchoolName, r.OpenedByName, r.CreatedAt, r.LastActivityAt, r.MessageCount)).ToList();
        return Result<PagedResult<TicketListItemDto>>.Success(new PagedResult<TicketListItemDto>(items, query.Page, query.PageSize, totalCount, totalPages));
    }

    public async Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await Scoped()
            .Include(t => t.School)
            .Include(t => t.OpenedBy)
            .Include(t => t.Messages).ThenInclude(m => m.Author)
            .AsSplitQuery()
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

        return ticket is null
            ? Result<TicketDto>.NotFound("Ticket não encontrado.")
            : Result<TicketDto>.Success(ToDto(ticket));
    }

    public async Task<Result<TicketDto>> CreateAsync(CreateTicketDto dto, CancellationToken cancellationToken = default)
    {
        if (currentUser.Role != "Director" || currentUser.SchoolId is not { } schoolId)
            return Result<TicketDto>.Forbidden("Só a direção da escola abre tickets.");

        var ticket = new Ticket
        {
            SchoolId = schoolId,
            OpenedById = currentUser.UserId,
            Type = dto.Type,
            Title = dto.Title.Trim(),
            Description = dto.Description.Trim()
        };
        context.Tickets.Add(ticket);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var created = await GetByIdAsync(ticket.Id, cancellationToken);
        return created.IsSuccess ? Result<TicketDto>.Created(created.Data!) : created;
    }

    public async Task<Result<TicketDto>> AddMessageAsync(Guid id, CreateTicketMessageDto dto, CancellationToken cancellationToken = default)
    {
        var ticket = await Scoped().FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
            return Result<TicketDto>.NotFound("Ticket não encontrado.");

        if (ticket.Status == TicketStatus.Closed)
            return Result<TicketDto>.BadRequest("Este ticket está fechado. Abra um novo ticket.");

        context.TicketMessages.Add(new TicketMessage { TicketId = ticket.Id, AuthorId = currentUser.UserId, Body = dto.Body.Trim() });

        // A primeira resposta da administração mostra que alguém pegou o ticket;
        // a direção respondendo um ticket resolvido diz que o problema voltou
        if (IsAdmin && ticket.Status == TicketStatus.Open)
            ticket.Status = TicketStatus.InProgress;
        else if (!IsAdmin && ticket.Status == TicketStatus.Resolved)
            ticket.Status = TicketStatus.Open;

        ticket.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(ticket.Id, cancellationToken);
    }

    public async Task<Result<TicketDto>> UpdateStatusAsync(Guid id, UpdateTicketStatusDto dto, CancellationToken cancellationToken = default)
    {
        if (!IsAdmin)
            return Result<TicketDto>.Forbidden("Só a administração muda a situação do ticket.");

        var ticket = await context.Tickets.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
            return Result<TicketDto>.NotFound("Ticket não encontrado.");

        ticket.Status = dto.Status;
        ticket.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(ticket.Id, cancellationToken);
    }

    public async Task<Result<TicketSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var pending = await context.Tickets
            .Where(t => t.Status == TicketStatus.Open || t.Status == TicketStatus.InProgress)
            .GroupBy(t => new { t.Type, t.Status })
            .Select(g => new { g.Key.Type, g.Key.Status, Count = g.Count() })
            .ToListAsync(cancellationToken);

        int ByType(TicketType type) => pending.Where(p => p.Type == type).Sum(p => p.Count);
        int ByStatus(TicketStatus status) => pending.Where(p => p.Status == status).Sum(p => p.Count);

        return Result<TicketSummaryDto>.Success(new TicketSummaryDto(
            ByType(TicketType.Bug), ByType(TicketType.Improvement), ByType(TicketType.Question), ByType(TicketType.Other),
            ByStatus(TicketStatus.Open), ByStatus(TicketStatus.InProgress)));
    }

    // Administração: todos; direção: os da própria escola; os demais perfis: nenhum
    private IQueryable<Ticket> Scoped() => currentUser.Role switch
    {
        "Admin" => context.Tickets,
        "Director" => context.Tickets.Where(t => t.SchoolId == currentUser.SchoolId),
        _ => context.Tickets.Where(_ => false)
    };

    private static TicketDto ToDto(Ticket t) => new(
        t.Id, t.Title, t.Description, t.Type, TypeName(t.Type), t.Status, StatusName(t.Status),
        t.SchoolId, t.School.Name, t.OpenedBy.Name, t.CreatedAt, t.UpdatedAt ?? t.CreatedAt,
        t.Messages.OrderBy(m => m.CreatedAt)
            .Select(m => new TicketMessageDto(m.Id, m.Author.Name, m.Author.RoleId == RoleIds.Admin, m.Body, m.CreatedAt))
            .ToList());

    public static string TypeName(TicketType type) => type switch
    {
        TicketType.Bug => "Bug",
        TicketType.Improvement => "Melhoria",
        TicketType.Question => "Dúvida",
        _ => "Outro"
    };

    public static string StatusName(TicketStatus status) => status switch
    {
        TicketStatus.Open => "Aberto",
        TicketStatus.InProgress => "Em andamento",
        TicketStatus.Resolved => "Resolvido",
        _ => "Fechado"
    };
}

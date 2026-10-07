using EscolaSystemApi.Domain.Enums;

namespace EscolaSystemApi.Application.DTOs.Tickets;

public sealed record CreateTicketDto(TicketType Type, string Title, string Description);

public sealed record CreateTicketMessageDto(string Body);

public sealed record UpdateTicketStatusDto(TicketStatus Status);

public sealed record TicketFilter(TicketStatus? Status = null, TicketType? Type = null, Guid? SchoolId = null);

// Linha da lista: sem a descrição nem as mensagens
public sealed record TicketListItemDto(
    Guid Id,
    string Title,
    TicketType Type,
    string TypeName,
    TicketStatus Status,
    string StatusName,
    Guid SchoolId,
    string SchoolName,
    string OpenedByName,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    int MessageCount);

public sealed record TicketMessageDto(Guid Id, string AuthorName, bool FromAdministration, string Body, DateTime CreatedAt);

public sealed record TicketDto(
    Guid Id,
    string Title,
    string Description,
    TicketType Type,
    string TypeName,
    TicketStatus Status,
    string StatusName,
    Guid SchoolId,
    string SchoolName,
    string OpenedByName,
    DateTime CreatedAt,
    DateTime LastActivityAt,
    IReadOnlyList<TicketMessageDto> Messages);

// Tickets que ainda pedem atenção (abertos ou em andamento), por tipo, para o painel do admin
public sealed record TicketSummaryDto(int Bugs, int Improvements, int Questions, int Others, int Open, int InProgress);

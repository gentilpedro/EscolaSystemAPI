using EscolaSystemApi.Application.DTOs.Tickets;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface ITicketService
{
    Task<Result<PagedResult<TicketListItemDto>>> GetAllAsync(PagedQuery query, TicketFilter filter, CancellationToken cancellationToken = default);
    Task<Result<TicketDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Result<TicketDto>> CreateAsync(CreateTicketDto dto, CancellationToken cancellationToken = default);
    Task<Result<TicketDto>> AddMessageAsync(Guid id, CreateTicketMessageDto dto, CancellationToken cancellationToken = default);
    Task<Result<TicketDto>> UpdateStatusAsync(Guid id, UpdateTicketStatusDto dto, CancellationToken cancellationToken = default);
    Task<Result<TicketSummaryDto>> GetSummaryAsync(CancellationToken cancellationToken = default);
}

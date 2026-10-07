using EscolaSystemApi.Application.DTOs.Tickets;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Tickets;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

// A direção abre e acompanha os tickets da escola; a administração responde e muda a situação
[Authorize(Roles = "Admin,Director")]
[Route("api/tickets")]
public class TicketsController(ITicketService ticketService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedQuery query, [FromQuery] TicketStatus? status, [FromQuery] TicketType? type, [FromQuery] Guid? schoolId,
        CancellationToken cancellationToken)
        => HandleResult(await ticketService.GetAllAsync(query, new TicketFilter(status, type, schoolId), cancellationToken));

    [HttpGet("summary")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Summary(CancellationToken cancellationToken)
        => HandleResult(await ticketService.GetSummaryAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await ticketService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Director")]
    public async Task<IActionResult> Create([FromBody] CreateTicketDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateTicketValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await ticketService.CreateAsync(dto, cancellationToken));
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] CreateTicketMessageDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateTicketMessageValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await ticketService.AddMessageAsync(id, dto, cancellationToken));
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateTicketStatusDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateTicketStatusValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await ticketService.UpdateStatusAsync(id, dto, cancellationToken));
    }
}

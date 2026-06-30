using EscolaSystemApi.Application.DTOs.PendingWorks;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.PendingWorks;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
[Route("api/pending-works")]
public class PendingWorksController(IPendingWorkService pendingWorkService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.GetAllAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreatePendingWorkDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreatePendingWorkValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await pendingWorkService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}/delivered")]
    [Authorize(Roles = "Admin,Teacher,Director,Student")]
    public async Task<IActionResult> MarkAsDelivered(Guid id, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.MarkAsDeliveredAsync(id, cancellationToken));
}
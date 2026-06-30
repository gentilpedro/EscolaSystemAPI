using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.DisciplinaryCalls;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
[Route("api/disciplinary-calls")]
public class DisciplinaryCallsController(IDisciplinaryCallService disciplinaryCallService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.GetAllAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreateDisciplinaryCallDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateDisciplinaryCallValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await disciplinaryCallService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDisciplinaryCallDto dto, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.UpdateAsync(id, dto, cancellationToken));

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin,Director")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ResolveCallDto dto, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.ApproveAsync(id, CurrentUserId, dto, cancellationToken));

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin,Director")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ResolveCallDto dto, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.RejectAsync(id, CurrentUserId, dto, cancellationToken));
}
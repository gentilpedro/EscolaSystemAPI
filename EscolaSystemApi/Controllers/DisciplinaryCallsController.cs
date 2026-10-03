using EscolaSystemApi.Application.DTOs.DisciplinaryCalls;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.DisciplinaryCalls;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
[Route("api/disciplinary-calls")]
public class DisciplinaryCallsController(IDisciplinaryCallService disciplinaryCallService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedQuery query,
        [FromQuery] Guid? schoolId,
        [FromQuery] Guid? studentId,
        [FromQuery] Guid? classId,
        [FromQuery] DisciplinaryCallStatus? status,
        CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.GetAllAsync(
            query, new DisciplinaryCallFilter(schoolId, studentId, classId, status), cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await disciplinaryCallService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Director,Orientador")]
    public async Task<IActionResult> Create([FromBody] CreateDisciplinaryCallDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateDisciplinaryCallValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await disciplinaryCallService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director,Orientador")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateDisciplinaryCallDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateDisciplinaryCallValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await disciplinaryCallService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Admin,Director,Orientador")]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ResolveCallDto dto, CancellationToken cancellationToken)
    {
        var validation = await new ResolveCallValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await disciplinaryCallService.ApproveAsync(id, CurrentUserId, dto, cancellationToken));
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Admin,Director,Orientador")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ResolveCallDto dto, CancellationToken cancellationToken)
    {
        var validation = await new ResolveCallValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await disciplinaryCallService.RejectAsync(id, CurrentUserId, dto, cancellationToken));
    }
}

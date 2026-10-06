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
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, [FromQuery] Guid? classId, [FromQuery] Guid? studentId, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.GetAllAsync(query, classId, studentId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreatePendingWorkDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreatePendingWorkValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await pendingWorkService.CreateAsync(dto, cancellationToken));
    }

    // Lança o trabalho para todos os alunos ativos da turma, numa única gravação
    [HttpPost("class")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> CreateForClass([FromBody] CreateClassAssignmentDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateClassAssignmentValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await pendingWorkService.CreateForClassAsync(dto, cancellationToken));
    }

    [HttpPut("assignments/{assignmentId:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> UpdateAssignment(Guid assignmentId, [FromBody] UpdateAssignmentDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateAssignmentValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await pendingWorkService.UpdateAssignmentAsync(assignmentId, dto, cancellationToken));
    }

    [HttpDelete("assignments/{assignmentId:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> DeleteAssignment(Guid assignmentId, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.DeleteAssignmentAsync(assignmentId, cancellationToken));

    [HttpPut("{id:guid}/delivered")]
    [Authorize(Roles = "Admin,Teacher,Director,Student")]
    public async Task<IActionResult> MarkAsDelivered(Guid id, CancellationToken cancellationToken)
        => HandleResult(await pendingWorkService.MarkAsDeliveredAsync(id, cancellationToken));
}
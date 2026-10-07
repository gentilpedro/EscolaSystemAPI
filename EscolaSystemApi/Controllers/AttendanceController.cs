using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Attendance;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize(Roles = RoleNames.School)]
[Route("api/attendance")]
public class AttendanceController(IAttendanceService attendanceService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, [FromQuery] Guid? classId, [FromQuery] Guid? studentId, [FromQuery] DateOnly? date, CancellationToken cancellationToken)
        => HandleResult(await attendanceService.GetAllAsync(query, classId, studentId, date, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await attendanceService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateAttendanceValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await attendanceService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Teacher,Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAttendanceDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateAttendanceValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await attendanceService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpPost("bulk")]
    [Authorize(Roles = "Teacher,Director")]
    public async Task<IActionResult> BulkCreate([FromBody] List<CreateAttendanceDto> dtos, CancellationToken cancellationToken)
    {
        var validator = new CreateAttendanceValidator();
        foreach (var dto in dtos)
        {
            var validation = await validator.ValidateAsync(dto, cancellationToken);
            if (!validation.IsValid)
                return ValidationFailed(validation);
        }

        return HandleResult(await attendanceService.BulkCreateAsync(dtos, cancellationToken));
    }
}
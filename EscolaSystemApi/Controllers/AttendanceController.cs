using EscolaSystemApi.Application.DTOs.Attendance;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
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
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreateAttendanceDto dto, CancellationToken cancellationToken)
        => HandleResult(await attendanceService.CreateAsync(dto, cancellationToken));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateAttendanceDto dto, CancellationToken cancellationToken)
        => HandleResult(await attendanceService.UpdateAsync(id, dto, cancellationToken));

    [HttpPost("bulk")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> BulkCreate([FromBody] List<CreateAttendanceDto> dtos, CancellationToken cancellationToken)
        => HandleResult(await attendanceService.BulkCreateAsync(dtos, cancellationToken));
}
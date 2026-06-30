using EscolaSystemApi.Application.DTOs.Grades;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Grades;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
public class GradesController(IGradeService gradeService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, [FromQuery] Guid? classId, [FromQuery] Guid? studentId, CancellationToken cancellationToken)
        => HandleResult(await gradeService.GetAllAsync(query, classId, studentId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await gradeService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Create([FromBody] CreateGradeDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateGradeValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await gradeService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateGradeDto dto, CancellationToken cancellationToken)
        => HandleResult(await gradeService.UpdateAsync(id, dto, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Teacher,Director")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => HandleResult(await gradeService.DeleteAsync(id, cancellationToken));
}
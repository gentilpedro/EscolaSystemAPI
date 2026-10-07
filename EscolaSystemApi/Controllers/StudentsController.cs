using EscolaSystemApi.Application.DTOs.Students;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Students;
using EscolaSystemApi.Common;
using EscolaSystemApi.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize(Roles = RoleNames.School)]
public class StudentsController(IStudentService studentService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, [FromQuery] Guid? classId, [FromQuery] Guid? schoolId, [FromQuery] bool? isActive, CancellationToken cancellationToken)
        => HandleResult(await studentService.GetAllAsync(query, classId, schoolId, isActive, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await studentService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Director")]
    public async Task<IActionResult> Create([FromBody] CreateStudentDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateStudentValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await studentService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStudentDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateStudentValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await studentService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Director")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => HandleResult(await studentService.DeleteAsync(id, cancellationToken));
}

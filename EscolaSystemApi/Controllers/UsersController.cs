using EscolaSystemApi.Application.DTOs.Users;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Users;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize(Roles = "Admin,Director")]
[Route("api/users")]
public class UsersController(IUserService userService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] PagedQuery query, [FromQuery] Guid? schoolId, [FromQuery] int? roleId,
        [FromQuery] string? search, [FromQuery] bool? isActive, CancellationToken cancellationToken)
        => HandleResult(await userService.GetAllAsync(query, schoolId, roleId, search, isActive, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await userService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateUserValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await userService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserDto dto, CancellationToken cancellationToken)
    {
        var validation = await new UpdateUserValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await userService.UpdateAsync(id, dto, cancellationToken));
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => HandleResult(await userService.DeleteAsync(id, cancellationToken));

    [HttpPost("{teacherId:guid}/assign-class/{classId:guid}")]
    public async Task<IActionResult> AssignClass(Guid teacherId, Guid classId, CancellationToken cancellationToken)
        => HandleResult(await userService.AssignClassAsync(teacherId, classId, cancellationToken));

    [HttpDelete("{teacherId:guid}/assign-class/{classId:guid}")]
    public async Task<IActionResult> UnassignClass(Guid teacherId, Guid classId, CancellationToken cancellationToken)
        => HandleResult(await userService.UnassignClassAsync(teacherId, classId, cancellationToken));

    [HttpPost("{parentId:guid}/assign-student/{studentId:guid}")]
    public async Task<IActionResult> AssignStudent(Guid parentId, Guid studentId, CancellationToken cancellationToken)
        => HandleResult(await userService.AssignStudentAsync(parentId, studentId, cancellationToken));

    [HttpDelete("{parentId:guid}/assign-student/{studentId:guid}")]
    public async Task<IActionResult> UnassignStudent(Guid parentId, Guid studentId, CancellationToken cancellationToken)
        => HandleResult(await userService.UnassignStudentAsync(parentId, studentId, cancellationToken));

    [HttpPost("{orientadorId:guid}/assign-orientador-class/{classId:guid}")]
    public async Task<IActionResult> AssignOrientadorClass(Guid orientadorId, Guid classId, CancellationToken cancellationToken)
        => HandleResult(await userService.AssignOrientadorClassAsync(orientadorId, classId, cancellationToken));

    [HttpDelete("{orientadorId:guid}/assign-orientador-class/{classId:guid}")]
    public async Task<IActionResult> UnassignOrientadorClass(Guid orientadorId, Guid classId, CancellationToken cancellationToken)
        => HandleResult(await userService.UnassignOrientadorClassAsync(orientadorId, classId, cancellationToken));
}
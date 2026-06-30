using EscolaSystemApi.Application.DTOs.Classes;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Classes;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
public class ClassesController(IClassService classService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => HandleResult(await classService.GetAllAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await classService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin,Director")]
    public async Task<IActionResult> Create([FromBody] CreateClassDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateClassValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await classService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin,Director")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateClassDto dto, CancellationToken cancellationToken)
        => HandleResult(await classService.UpdateAsync(id, dto, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,Director")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => HandleResult(await classService.DeleteAsync(id, cancellationToken));
}
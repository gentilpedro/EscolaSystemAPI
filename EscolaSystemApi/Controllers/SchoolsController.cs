using EscolaSystemApi.Application.DTOs.Schools;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Schools;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

[Authorize]
public class SchoolsController(ISchoolService schoolService) : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] PagedQuery query, CancellationToken cancellationToken)
        => HandleResult(await schoolService.GetAllAsync(query, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
        => HandleResult(await schoolService.GetByIdAsync(id, cancellationToken));

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Create([FromBody] CreateSchoolDto dto, CancellationToken cancellationToken)
    {
        var validation = await new CreateSchoolValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await schoolService.CreateAsync(dto, cancellationToken));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateSchoolDto dto, CancellationToken cancellationToken)
        => HandleResult(await schoolService.UpdateAsync(id, dto, cancellationToken));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
        => HandleResult(await schoolService.DeleteAsync(id, cancellationToken));
}

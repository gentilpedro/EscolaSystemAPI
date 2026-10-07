using EscolaSystemApi.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EscolaSystemApi.Controllers;

// Pessoas da escola: professor, orientador e responsável podem estar em várias escolas
[Authorize(Roles = "Director")]
[Route("api/schools/{schoolId:guid}/members")]
public class SchoolMembersController(IUserService userService) : BaseApiController
{
    public sealed record AddMemberRequest(string Email);

    // Adiciona à escola uma pessoa já cadastrada (pelo e-mail), sem criar outra conta
    [HttpPost]
    public async Task<IActionResult> Add(Guid schoolId, [FromBody] AddMemberRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest(new { error = "Informe o e-mail.", message = "Informe o e-mail." });

        return HandleResult(await userService.AddMemberAsync(schoolId, request.Email, cancellationToken));
    }

    // Tira a pessoa da escola: encerra os vínculos com ela, as turmas e os alunos dela; o histórico fica
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid schoolId, Guid userId, CancellationToken cancellationToken)
        => HandleResult(await userService.RemoveMemberAsync(schoolId, userId, cancellationToken));
}

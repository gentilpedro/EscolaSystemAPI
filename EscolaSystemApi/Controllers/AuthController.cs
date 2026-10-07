using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Auth;
using EscolaSystemApi.Common;
using EscolaSystemApi.Infrastructure.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaSystemApi.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService, AuthCookies cookies) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var validator = new LoginRequestValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        var result = await authService.LoginAsync(dto, Request.Headers.UserAgent.ToString(), cancellationToken);
        if (!result.IsSuccess)
            return HandleResult(result);

        cookies.Write(Request, Response, result.Data!, newCsrfToken: true);
        return Ok(result.Data!.ToResponse());
    }

    // Troca o refresh token (cookie) por um par novo. 401: sessão acabou, entrar de novo.
    // 409: outra aba renovou um instante antes; basta repetir a requisição original.
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(Request.Cookies[AuthCookies.RefreshCookie], cancellationToken);
        if (result.IsSuccess)
        {
            cookies.Write(Request, Response, result.Data!, newCsrfToken: false);
            return Ok(result.Data!.ToResponse());
        }

        if (result.StatusCode == StatusCodes.Status401Unauthorized)
            cookies.Clear(Response);
        return HandleResult(result);
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var validator = new RegisterRequestValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await authService.RegisterAsync(dto, CurrentUserId, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => HandleResult(await authService.GetMeAsync(CurrentUserId, cancellationToken));

    // Sem [Authorize]: com o token de acesso já expirado, a sessão é encontrada pelo refresh token
    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(CurrentSessionId, Request.Cookies[AuthCookies.RefreshCookie], cancellationToken);
        cookies.Clear(Response);
        return NoContent();
    }

    [HttpPost("change-password")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto, CancellationToken cancellationToken)
    {
        var validation = await new ChangePasswordDtoValidator().ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await authService.ChangePasswordAsync(CurrentUserId, dto, CurrentSessionId, cancellationToken));
    }

    // Aparelhos conectados: as sessões abertas da própria conta
    [HttpGet("sessions")]
    [Authorize]
    public async Task<IActionResult> Sessions(CancellationToken cancellationToken)
        => HandleResult(await authService.GetSessionsAsync(CurrentUserId, CurrentSessionId, cancellationToken));

    // Sai de um aparelho; o atual sai pelo logout
    [HttpDelete("sessions/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(Guid id, CancellationToken cancellationToken)
        => HandleResult(await authService.RevokeSessionAsync(CurrentUserId, id, CurrentSessionId, cancellationToken));

    // Sai de todos os outros aparelhos e mantém este
    [HttpDelete("sessions")]
    [Authorize]
    public async Task<IActionResult> RevokeOtherSessions(CancellationToken cancellationToken)
        => HandleResult(await authService.RevokeOtherSessionsAsync(CurrentUserId, CurrentSessionId, cancellationToken));

    // Senha de outra pessoa: admin (de administradores e diretores) ou diretor (da própria escola)
    [HttpPost("reset-password")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        var validator = new ResetPasswordDtoValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return ValidationFailed(validation);

        return HandleResult(await authService.ResetPasswordAsync(dto, CurrentUserId, cancellationToken));
    }
}

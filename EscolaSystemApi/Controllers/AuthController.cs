using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Application.Interfaces;
using EscolaSystemApi.Application.Validators.Auth;
using EscolaSystemApi.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace EscolaSystemApi.Controllers;

[Route("api/auth")]
public class AuthController(IAuthService authService, ITokenBlacklistService tokenBlacklist) : BaseApiController
{
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken cancellationToken)
    {
        var validator = new LoginRequestValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await authService.LoginAsync(dto, cancellationToken));
    }

    [HttpPost("register")]
    [Authorize(Roles = "Admin,Director")]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken cancellationToken)
    {
        var validator = new RegisterRequestValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await authService.RegisterAsync(dto, cancellationToken));
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
        => HandleResult(await authService.GetMeAsync(CurrentUserId, cancellationToken));

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        var jti = User.FindFirstValue(JwtRegisteredClaimNames.Jti);
        if (jti is not null)
            await tokenBlacklist.RevokeAsync(jti, TimeSpan.FromMinutes(60));

        return NoContent();
    }

    [HttpPost("reset-password")]
    [Authorize]
    [EnableRateLimiting("AuthPolicy")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto, CancellationToken cancellationToken)
    {
        var validator = new ResetPasswordDtoValidator();
        var validation = await validator.ValidateAsync(dto, cancellationToken);
        if (!validation.IsValid)
            return BadRequest(new { errors = validation.Errors.Select(e => e.ErrorMessage) });

        return HandleResult(await authService.ResetPasswordAsync(dto, CurrentUserId, cancellationToken));
    }
}
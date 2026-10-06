using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IAuthService
{
    Task<Result<AuthSession>> LoginAsync(LoginRequestDto dto, CancellationToken cancellationToken = default);
    // Troca o refresh token por um par novo (rotação); reuso de token já trocado encerra todas as sessões
    Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid? sessionId, string? refreshToken, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<Result<bool>> ResetPasswordAsync(ResetPasswordDto dto, Guid requestingUserId, Guid? currentSessionId = null, CancellationToken cancellationToken = default);
}

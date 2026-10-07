using EscolaSystemApi.Application.DTOs.Auth;
using EscolaSystemApi.Common;

namespace EscolaSystemApi.Application.Interfaces;

public interface IAuthService
{
    // userAgent: navegador de quem entra, para a lista de aparelhos conectados
    Task<Result<AuthSession>> LoginAsync(LoginRequestDto dto, string? userAgent = null, CancellationToken cancellationToken = default);
    // Troca o refresh token por um par novo (rotação); reuso de token já trocado encerra todas as sessões
    Task<Result<AuthSession>> RefreshAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid? sessionId, string? refreshToken, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> RegisterAsync(RegisterRequestDto dto, CancellationToken cancellationToken = default);
    Task<Result<UserDto>> GetMeAsync(Guid userId, CancellationToken cancellationToken = default);
    // Troca da própria senha: confere a atual, encerra as outras sessões e mantém a atual
    Task<Result<bool>> ChangePasswordAsync(Guid userId, ChangePasswordDto dto, Guid? currentSessionId = null, CancellationToken cancellationToken = default);
    // Redefinição da senha de outra pessoa (admin ou diretor)
    Task<Result<bool>> ResetPasswordAsync(ResetPasswordDto dto, Guid requestingUserId, CancellationToken cancellationToken = default);
    // Sessões abertas da própria conta (aparelhos conectados)
    Task<Result<IReadOnlyList<SessionDto>>> GetSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeSessionAsync(Guid userId, Guid sessionId, Guid? currentSessionId, CancellationToken cancellationToken = default);
    Task<Result<bool>> RevokeOtherSessionsAsync(Guid userId, Guid? currentSessionId, CancellationToken cancellationToken = default);
}

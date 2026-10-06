namespace EscolaSystemApi.Application.DTOs.Auth;

// Os tokens vão em cookies httpOnly; o corpo só diz até quando vale o acesso e quem entrou
public sealed record AuthResponseDto(DateTime ExpiresAt, UserDto User);

// Resultado interno de login/renovação: o controller grava os tokens em cookies e devolve só o AuthResponseDto
public sealed record AuthSession(
    string AccessToken,
    DateTime AccessExpiresAt,
    string RefreshToken,
    DateTime RefreshExpiresAt,
    UserDto User)
{
    public AuthResponseDto ToResponse() => new(AccessExpiresAt, User);
}

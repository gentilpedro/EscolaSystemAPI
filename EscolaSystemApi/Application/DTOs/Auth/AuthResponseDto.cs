namespace EscolaSystemApi.Application.DTOs.Auth;

public sealed record AuthResponseDto(string Token, string TokenType, DateTime ExpiresAt, UserDto User);

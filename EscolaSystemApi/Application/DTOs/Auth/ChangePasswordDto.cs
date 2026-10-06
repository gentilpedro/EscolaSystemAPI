namespace EscolaSystemApi.Application.DTOs.Auth;

// Troca da própria senha: só com a senha atual
public sealed record ChangePasswordDto(string CurrentPassword, string NewPassword);

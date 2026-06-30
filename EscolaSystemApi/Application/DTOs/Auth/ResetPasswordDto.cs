namespace EscolaSystemApi.Application.DTOs.Auth;

public sealed record ResetPasswordDto(string Email, string NewPassword);

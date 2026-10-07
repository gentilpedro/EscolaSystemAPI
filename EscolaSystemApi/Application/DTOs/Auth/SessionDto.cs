namespace EscolaSystemApi.Application.DTOs.Auth;

// Uma sessão aberta da própria conta: o aparelho resumido, quando entrou e quando foi usada pela última vez
public sealed record SessionDto(Guid Id, string Device, DateTime CreatedAt, DateTime LastUsedAt, bool IsCurrent);

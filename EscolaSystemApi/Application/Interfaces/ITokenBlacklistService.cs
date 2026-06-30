namespace EscolaSystemApi.Application.Interfaces;

public interface ITokenBlacklistService
{
    Task RevokeAsync(string jti, TimeSpan expiry);
    Task<bool> IsRevokedAsync(string jti);
}

using System.Collections.Concurrent;
using EscolaSystemApi.Application.Interfaces;

namespace EscolaSystemApi.Application.Services;

public class InMemoryTokenBlacklistService : ITokenBlacklistService
{
    private readonly ConcurrentDictionary<string, DateTime> _blacklist = new();

    public Task RevokeAsync(string jti, TimeSpan expiry)
    {
        _blacklist[jti] = DateTime.UtcNow.Add(expiry);
        PurgeExpired();
        return Task.CompletedTask;
    }

    public Task<bool> IsRevokedAsync(string jti)
    {
        if (_blacklist.TryGetValue(jti, out var expiresAt))
        {
            if (DateTime.UtcNow < expiresAt)
                return Task.FromResult(true);

            _blacklist.TryRemove(jti, out _);
        }

        return Task.FromResult(false);
    }

    private void PurgeExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var key in _blacklist.Keys.Where(k => _blacklist.TryGetValue(k, out var exp) && exp < now))
            _blacklist.TryRemove(key, out _);
    }
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using ElectroShop.Contracts.Responses;
namespace ElectroShop.Mvc.Services;

// Cookie chỉ mang session id; JWT nằm trên server và riêng cho từng phiên.
public sealed class TokenStore
{
    private readonly ConcurrentDictionary<string, AuthResponse> sessions = new();
    public string Add(AuthResponse auth)
    {
        foreach (var expired in sessions.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow))
            sessions.TryRemove(expired.Key, out _);
        var id = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        sessions[id] = auth;
        return id;
    }
    public AuthResponse? Get(string? id)
    {
        if (id is null || !sessions.TryGetValue(id, out var auth))
            return null;
        if (auth.ExpiresAt > DateTimeOffset.UtcNow)
            return auth;
        sessions.TryRemove(id, out _);
        return null;
    }
    public void Remove(string? id)
    {
        if (id is not null)
            sessions.TryRemove(id, out _);
    }
}

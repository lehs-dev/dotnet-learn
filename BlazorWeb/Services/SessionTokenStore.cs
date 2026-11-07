using System.Collections.Concurrent;
using ElectroShop.Contracts.Responses;

namespace ElectroShop.Blazor.Services;

// JWT chỉ tồn tại ở server; cookie trình duyệt chỉ chứa mã phiên ngẫu nhiên.
public sealed class SessionTokenStore
{
    private readonly ConcurrentDictionary<string, AuthResponse> sessions = new();
    public string Add(AuthResponse response)
    {
        foreach (var entry in sessions.Where(x => x.Value.ExpiresAt <= DateTimeOffset.UtcNow))
            sessions.TryRemove(entry.Key, out _);
        var id = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        sessions[id] = response;
        return id;
    }
    public AuthResponse? Get(string? id)
    {
        if (id is null || !sessions.TryGetValue(id, out var response))
            return null;
        if (response.ExpiresAt > DateTimeOffset.UtcNow)
            return response;
        sessions.TryRemove(id, out _);
        return null;
    }
    public void Remove(string? id)
    {
        if (id is not null)
            sessions.TryRemove(id, out _);
    }
}

using System.Collections.Concurrent;
using ElectroShop.Contracts.Responses;
namespace ElectroShop.RazorPages.Services;
// JWT nằm trên server; cookie chỉ mang mã phiên ngẫu nhiên.
public sealed class TokenStore
{
    private readonly ConcurrentDictionary<string, AuthResponse> sessions = new();
    public string Add(AuthResponse value)
    {
        var key = Guid.NewGuid().ToString("N");
        sessions[key] = value;
        return key;
    }
    public AuthResponse? Get(string? key)
    {
        if (key is null || !sessions.TryGetValue(key, out var value))
            return null;
        if (value.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            sessions.TryRemove(key, out _);
            return null;
        }
        return value;
    }
    public void Remove(string? key)
    {
        if (key is not null)
            sessions.TryRemove(key, out _);
    }
}

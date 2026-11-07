using ElectroShop.Contracts.DTOs;

namespace ElectroShop.Contracts.Responses;

public record PagedResult<T>(List<T> Items, int TotalCount, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
}

public record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User);

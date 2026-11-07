using Microsoft.AspNetCore.Identity;
using ElectroShop.Contracts.Enums;
namespace ElectroShop.Api.Entities;

public class Cart
{
    public int Id
    {
        get; set;
    }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<CartItem> Items { get; set; } = [];
}

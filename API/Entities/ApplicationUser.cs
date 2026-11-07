using Microsoft.AspNetCore.Identity;
using ElectroShop.Contracts.Enums;
namespace ElectroShop.Api.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = "";
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; } = true;
}

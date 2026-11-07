using ElectroShop.Contracts.Enums;
using Microsoft.AspNetCore.Identity;

namespace ElectroShop.Api.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Description { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public List<Product> Products { get; set; } = [];
}

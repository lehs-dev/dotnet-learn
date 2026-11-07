using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;

namespace ElectroShop.RazorPages.Models;

public record ProductEditor(int Id, ProductRequest Product, List<CategoryDto> Categories)
{
    public static ProductEditor From(ProductDto p, List<CategoryDto> categories) =>
        new(
            p.Id,
            new ProductRequest
            {
                CategoryId = p.CategoryId,
                Name = p.Name,
                Slug = p.Slug,
                SKU = p.SKU,
                Description = p.Description,
                Price = p.Price,
                StockQuantity = p.StockQuantity,
                ImageUrl = p.ImageUrl,
                IsActive = p.IsActive,
                RowVersion = p.RowVersion,
            },
            categories
        );
}

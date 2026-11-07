using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages.Products;

public sealed class DetailModel(ShopApiClient api) : ShopPageModel
{
    public ProductDto? Product { get; set; }
    public List<ProductDto> Related { get; set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug, CancellationToken ct)
    {
        try
        {
            Product = await api.GetAsync<ProductDto>(
                "api/products/" + Uri.EscapeDataString(slug),
                ct
            );
            var result = await api.GetAsync<PagedResult<ProductDto>>(
                $"api/products?categoryId={Product.CategoryId}&pageSize=5",
                ct
            );
            Related = result.Items.Where(x => x.Id != Product.Id).Take(4).ToList();
        }
        catch (ApiException ex)
        {
            if (ex.Status == 404)
                return NotFound();
            Error = ex.Message;
        }
        return Page();
    }
}

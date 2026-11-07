using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.Mvc.Controllers;

public sealed class ProductsController(ShopApiClient api) : Controller
{
    [HttpGet("/products/{slug}")]
    public async Task<IActionResult> Detail(string slug, CancellationToken ct)
    {
        try
        {
            var product = await api.GetAsync<ProductDto>(
                $"api/products/{Uri.EscapeDataString(slug)}",
                ct
            );
            var related = await api.GetAsync<PagedResult<ProductDto>>(
                $"api/products?categoryId={product.CategoryId}&pageSize=5",
                ct
            );
            return View(
                new ProductViewModel
                {
                    Product = product,
                    Related = related.Items.Where(p => p.Id != product.Id).Take(4).ToList(),
                }
            );
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect("/");
        }
    }
}

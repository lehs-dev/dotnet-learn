using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.Mvc.Controllers;

public sealed class HomeController(ShopApiClient api) : Controller
{
    [HttpGet("/")]
    public async Task<IActionResult> Index(string? q, int? categoryId, string? sort, int page = 1, CancellationToken ct = default)
    {
        var model = new CatalogViewModel { Q = q, CategoryId = categoryId, Sort = sort };
        try
        {
            model.Categories = await api.GetAsync<List<CategoryDto>>("api/categories", ct);
            model.Products = await api.GetAsync<PagedResult<ProductDto>>($"api/products?q={Uri.EscapeDataString(q ?? "")}&categoryId={categoryId}&sort={Uri.EscapeDataString(sort ?? "")}&page={Math.Max(1, page)}&pageSize=12", ct);
        }
        catch (ApiException ex) { model.Error = ex.Message; }
        return View(model);
    }
    [Route("/Home/Error")]
    public IActionResult Error()
    {
        Response.StatusCode = 500;
        return View();
    }
}

using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.Mvc.Controllers;

[Authorize, Route("orders")]
public sealed class OrdersController(ShopApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int page = 1, CancellationToken ct = default)
    {
        var model = new OrdersViewModel();
        try
        {
            model.Orders = await api.GetAsync<PagedResult<OrderDto>>($"api/orders?page={Math.Max(1, page)}&pageSize=10", ct);
        }
        catch (ApiException ex) { model.Error = ex.Message; }
        return View(model);
    }
}

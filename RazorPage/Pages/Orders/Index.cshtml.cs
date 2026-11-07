using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.RazorPages.Pages.Orders;

[Authorize]
public sealed class IndexModel(ShopApiClient api) : ShopPageModel
{
    [BindProperty(SupportsGet = true), FromQuery(Name = "page")] public int PageNumber { get; set; } = 1;
    public PagedResult<OrderDto>? Orders
    {
        get; set;
    }
    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Orders = await api.GetAsync<PagedResult<OrderDto>>($"api/orders?page={Math.Max(1, PageNumber)}&pageSize=10", ct);
        }
        catch (ApiException ex) { Error = ex.Message; }
    }
}

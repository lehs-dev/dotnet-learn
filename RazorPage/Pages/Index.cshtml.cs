using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages;

public sealed class IndexModel(ShopApiClient api) : ShopPageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public int? CategoryId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true), FromQuery(Name = "page")]
    public int PageNumber { get; set; } = 1;
    public List<CategoryDto> Categories { get; set; } = [];
    public PagedResult<ProductDto>? Products { get; set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Categories = await api.GetAsync<List<CategoryDto>>("api/categories", ct);
            Products = await api.GetAsync<PagedResult<ProductDto>>(
                $"api/products?q={Uri.EscapeDataString(Q ?? "")}&categoryId={CategoryId}&sort={Sort}&page={Math.Max(1, PageNumber)}&pageSize=12",
                ct
            );
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostAddAsync(
        int productId,
        int quantity,
        string? returnUrl,
        CancellationToken ct
    )
    {
        if (User.Identity?.IsAuthenticated != true)
            return Redirect(
                "/?login=1&ReturnUrl="
                    + Uri.EscapeDataString(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/")
            );
        var target = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        try
        {
            await api.PostAsync<CartDto>(
                "api/cart/items",
                new CartQuantityRequest { ProductId = productId, Quantity = quantity },
                ct
            );
            return MessageRedirect(target, "Đã thêm sản phẩm vào giỏ hàng.", true);
        }
        catch (ApiException ex)
        {
            return MessageRedirect(target, ex.Message);
        }
    }
}

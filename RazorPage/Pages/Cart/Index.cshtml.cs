using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.RazorPages.Pages.Cart;

[Authorize]
public sealed class IndexModel(ShopApiClient api) : ShopPageModel
{
    public CartDto? Cart
    {
        get; set;
    }
    public async Task OnGetAsync(CancellationToken ct)
    {
        try
        {
            Cart = await api.GetAsync<CartDto>("api/cart", ct);
        }
        catch (ApiException ex) { Error = ex.Message; }
    }
    public async Task<IActionResult> OnPostUpdateAsync(int productId, int quantity, CancellationToken ct)
    {
        try
        {
            await api.PutAsync<CartDto>($"api/cart/items/{productId}", new CartQuantityRequest { ProductId = productId, Quantity = quantity }, ct);
            return MessageRedirect("/cart", "Đã cập nhật giỏ hàng.", true);
        }
        catch (ApiException ex) { return MessageRedirect("/cart", ex.Message); }
    }
    public async Task<IActionResult> OnPostRemoveAsync(int productId, CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync($"api/cart/items/{productId}", ct);
            return MessageRedirect("/cart", "Đã xóa sản phẩm.", true);
        }
        catch (ApiException ex) { return MessageRedirect("/cart", ex.Message); }
    }
    public async Task<IActionResult> OnPostClearAsync(CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync("api/cart", ct);
            return MessageRedirect("/cart", "Đã xóa toàn bộ giỏ hàng.", true);
        }
        catch (ApiException ex) { return MessageRedirect("/cart", ex.Message); }
    }
}

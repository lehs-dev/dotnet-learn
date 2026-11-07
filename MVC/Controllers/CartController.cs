using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.Mvc.Controllers;

[Authorize, Route("cart")]
public sealed class CartController(ShopApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = new CartViewModel();
        try
        {
            model.Cart = await api.GetAsync<CartDto>("api/cart", ct);
        }
        catch (ApiException ex) { model.Error = ex.Message; }
        return View(model);
    }
    [HttpPost("add")]
    public async Task<IActionResult> Add(CartQuantityRequest input, string? returnUrl, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            TempData["Error"] = "Số lượng sản phẩm không hợp lệ.";
        else
            try
            {
                await api.PostAsync<object?>("api/cart/items", input, ct);
                TempData["Success"] = "Đã thêm sản phẩm vào giỏ hàng.";
            }
            catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return LocalRedirect(Url.IsLocalUrl(returnUrl) ? returnUrl! : "/cart");
    }
    [HttpPost("update")]
    public async Task<IActionResult> Update(CartQuantityRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            TempData["Error"] = "Số lượng sản phẩm không hợp lệ.";
        else
            try
            {
                await api.PutAsync<object?>($"api/cart/items/{input.ProductId}", input, ct);
                TempData["Success"] = "Đã cập nhật số lượng.";
            }
            catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("remove")]
    public async Task<IActionResult> Remove(int productId, CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync($"api/cart/items/{productId}", ct);
            TempData["Success"] = "Đã xóa sản phẩm khỏi giỏ.";
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
    [HttpPost("clear")]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync("api/cart", ct);
            TempData["Success"] = "Đã xóa giỏ hàng.";
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index));
    }
}

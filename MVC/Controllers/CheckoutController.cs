using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.Mvc.Controllers;

[Authorize, Route("checkout")]
public sealed class CheckoutController(ShopApiClient api) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(int? orderId, CancellationToken ct)
    {
        var model = new CheckoutViewModel();
        try
        {
            if (orderId.HasValue)
                model.Order = await api.GetAsync<OrderDto>($"api/orders/{orderId}", ct);
            else
            {
                model.Cart = await api.GetAsync<CartDto>("api/cart", ct);
                var user = await api.GetAsync<UserDto>("api/auth/me", ct);
                model.Input.ReceiverName = user.FullName;
                model.Input.ReceiverPhone = user.PhoneNumber;
            }
        }
        catch (ApiException ex)
        {
            model.Error = ex.Message;
        }
        return View(model);
    }

    [HttpPost("")]
    public async Task<IActionResult> Submit(
        [Bind(Prefix = "Input")] CheckoutRequest input,
        bool success,
        Guid attemptKey,
        CancellationToken ct
    )
    {
        var model = new CheckoutViewModel { Input = input, AttemptKey = attemptKey };
        try
        {
            model.Cart = await api.GetAsync<CartDto>("api/cart", ct);
            if (!ModelState.IsValid || input.CheckoutKey == Guid.Empty || attemptKey == Guid.Empty)
            {
                model.Error = "Vui lòng kiểm tra thông tin giao hàng.";
                return View("Index", model);
            }
            model.Order = await api.PostAsync<OrderDto>("api/orders", input, ct);
            model.Payment = await api.PostAsync<PaymentDto>(
                $"api/payments/{model.Order.Id}",
                new SandboxPaymentRequest { AttemptKey = attemptKey, Success = success },
                ct
            );
            TempData[
                model.Payment.Status == ElectroShop.Contracts.Enums.PaymentStatus.Success
                    ? "Success"
                    : "Error"
            ] =
                model.Payment.Status == ElectroShop.Contracts.Enums.PaymentStatus.Success
                    ? $"Thanh toán thành công đơn {model.Order.OrderCode}."
                    : "Thanh toán mô phỏng thất bại. Giỏ hàng và tồn kho được giữ nguyên; bạn có thể thử lại.";
            return RedirectToAction(nameof(Index), new { orderId = model.Order.Id });
        }
        catch (ApiException ex)
        {
            model.Error = ex.Message;
        }
        return View("Index", model);
    }

    [HttpPost("retry")]
    public async Task<IActionResult> Retry(
        int orderId,
        bool success,
        Guid attemptKey,
        CancellationToken ct
    )
    {
        try
        {
            var payment = await api.PostAsync<PaymentDto>(
                $"api/payments/{orderId}",
                new SandboxPaymentRequest { AttemptKey = attemptKey, Success = success },
                ct
            );
            TempData[
                payment.Status == ElectroShop.Contracts.Enums.PaymentStatus.Success
                    ? "Success"
                    : "Error"
            ] =
                payment.Status == ElectroShop.Contracts.Enums.PaymentStatus.Success
                    ? "Thanh toán thành công."
                    : "Thanh toán thất bại. Bạn có thể thử lại với cùng đơn hàng.";
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Index), new { orderId });
    }
}

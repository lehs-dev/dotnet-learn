using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages.Checkout;

[Authorize]
public sealed class IndexModel(ShopApiClient api) : ShopPageModel
{
    [BindProperty]
    public CheckoutRequest Input { get; set; } = new();
    public CartDto? Cart { get; set; }
    public OrderDto? Order { get; set; }
    public List<PaymentDto> Payments { get; set; } = [];

    [BindProperty]
    public Guid AttemptKey { get; set; } = Guid.NewGuid();

    public async Task OnGetAsync(int? orderId, CancellationToken ct)
    {
        try
        {
            if (orderId.HasValue)
            {
                Order = await api.GetAsync<OrderDto>($"api/orders/{orderId}", ct);
                Payments = await api.GetAsync<List<PaymentDto>>($"api/payments/{orderId}", ct);
            }
            else
            {
                Cart = await api.GetAsync<CartDto>("api/cart", ct);
                var user = await api.GetAsync<UserDto>("api/auth/me", ct);
                Input.ReceiverName = user.FullName;
                Input.ReceiverPhone = user.PhoneNumber;
            }
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
    }

    public async Task<IActionResult> OnPostCreateAsync(bool success, CancellationToken ct)
    {
        if (AttemptKey == Guid.Empty || Input.CheckoutKey == Guid.Empty)
            ModelState.AddModelError("", "Thiếu mã thao tác. Vui lòng tải lại trang.");
        if (!ModelState.IsValid)
        {
            try
            {
                Cart = await api.GetAsync<CartDto>("api/cart", ct);
            }
            catch (ApiException ex)
            {
                Error = ex.Message;
            }
            return Page();
        }
        try
        {
            Order = await api.PostAsync<OrderDto>("api/orders", Input, ct);
            var payment = await api.PostAsync<PaymentDto>(
                $"api/payments/{Order!.Id}",
                new SandboxPaymentRequest { AttemptKey = AttemptKey, Success = success },
                ct
            );
            return MessageRedirect(
                $"/checkout?orderId={Order.Id}",
                payment!.Status == PaymentStatus.Success
                    ? $"Thanh toán thành công đơn {Order.OrderCode}."
                    : "Thanh toán mô phỏng thất bại. Giỏ hàng và tồn kho được giữ nguyên; bạn có thể thử lại.",
                payment.Status == PaymentStatus.Success
            );
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
            try
            {
                Cart = await api.GetAsync<CartDto>("api/cart", ct);
            }
            catch (ApiException) { }
            return Page();
        }
    }

    public async Task<IActionResult> OnPostPayAsync(
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
            return MessageRedirect(
                $"/checkout?orderId={orderId}",
                payment!.Status == PaymentStatus.Success
                    ? "Thanh toán thành công. Đơn hàng của bạn đã được ghi nhận."
                    : "Thanh toán sandbox thất bại. Bạn có thể thử lại đơn hàng này.",
                payment.Status == PaymentStatus.Success
            );
        }
        catch (ApiException ex)
        {
            return MessageRedirect($"/checkout?orderId={orderId}", ex.Message);
        }
    }
}

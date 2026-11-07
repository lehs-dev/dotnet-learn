using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ElectroShop.RazorPages.Models;

public abstract class ShopPageModel : PageModel
{
    public string? Error { get; set; }

    protected IActionResult MessageRedirect(string url, string message, bool success = false)
    {
        TempData[success ? "Success" : "Error"] = message;
        return LocalRedirect(url);
    }

    public static string OrderState(ElectroShop.Contracts.Enums.OrderStatus value) =>
        value switch
        {
            ElectroShop.Contracts.Enums.OrderStatus.Pending => "Chờ xử lý",
            ElectroShop.Contracts.Enums.OrderStatus.Confirmed => "Đã xác nhận",
            ElectroShop.Contracts.Enums.OrderStatus.Shipping => "Đang giao",
            ElectroShop.Contracts.Enums.OrderStatus.Completed => "Hoàn thành",
            ElectroShop.Contracts.Enums.OrderStatus.Cancelled => "Đã hủy",
            _ => "Không xác định",
        };

    public static string PaymentState(ElectroShop.Contracts.Enums.PaymentStatus value) =>
        value switch
        {
            ElectroShop.Contracts.Enums.PaymentStatus.Pending => "Chờ thanh toán",
            ElectroShop.Contracts.Enums.PaymentStatus.Success => "Thành công",
            ElectroShop.Contracts.Enums.PaymentStatus.Failed => "Thất bại",
            _ => "Không xác định",
        };

    public static string Vnd(decimal value) =>
        value.ToString("N0", System.Globalization.CultureInfo.GetCultureInfo("vi-VN")) + " ₫";
}

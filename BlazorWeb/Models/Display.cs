using System.Globalization;
using ElectroShop.Contracts.Enums;

namespace ElectroShop.Blazor.Models;

public static class Display
{
    public static string Money(decimal value) =>
        value.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";

    public static string Order(OrderStatus status) =>
        status switch
        {
            OrderStatus.Pending => "Chờ xử lý",
            OrderStatus.Confirmed => "Đã xác nhận",
            OrderStatus.Shipping => "Đang giao",
            OrderStatus.Completed => "Hoàn thành",
            OrderStatus.Cancelled => "Đã hủy",
            _ => status.ToString(),
        };

    public static string Payment(PaymentStatus status) =>
        status switch
        {
            PaymentStatus.Success => "Thành công",
            PaymentStatus.Failed => "Thất bại",
            _ => "Chờ thanh toán",
        };

    public static string Date(DateTimeOffset value) =>
        TimeZoneInfo
            .ConvertTime(value, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))
            .ToString("dd/MM/yyyy HH:mm");
}

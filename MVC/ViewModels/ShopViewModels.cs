using System.Globalization;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;

namespace ElectroShop.Mvc.ViewModels;

public static class Display
{
    public static IEnumerable<OrderStatus> NextStatuses(OrderDto order)
    {
        yield return order.OrderStatus;
        if (
            order.OrderStatus == ElectroShop.Contracts.Enums.OrderStatus.Pending
            && order.PaymentStatus == ElectroShop.Contracts.Enums.PaymentStatus.Success
        )
            yield return ElectroShop.Contracts.Enums.OrderStatus.Confirmed;
        if (
            order.OrderStatus == ElectroShop.Contracts.Enums.OrderStatus.Pending
            && order.PaymentStatus != ElectroShop.Contracts.Enums.PaymentStatus.Success
        )
            yield return ElectroShop.Contracts.Enums.OrderStatus.Cancelled;
        if (order.OrderStatus == ElectroShop.Contracts.Enums.OrderStatus.Confirmed)
            yield return ElectroShop.Contracts.Enums.OrderStatus.Shipping;
        if (order.OrderStatus == ElectroShop.Contracts.Enums.OrderStatus.Shipping)
            yield return ElectroShop.Contracts.Enums.OrderStatus.Completed;
    }

    public static string Money(decimal amount) =>
        amount.ToString("N0", CultureInfo.GetCultureInfo("vi-VN")) + " ₫";

    public static string Date(DateTimeOffset value) =>
        value.ToOffset(TimeSpan.FromHours(7)).ToString("dd/MM/yyyy HH:mm");

    public static string OrderStatus(OrderStatus status) =>
        status switch
        {
            ElectroShop.Contracts.Enums.OrderStatus.Pending => "Chờ xử lý",
            ElectroShop.Contracts.Enums.OrderStatus.Confirmed => "Đã xác nhận",
            ElectroShop.Contracts.Enums.OrderStatus.Shipping => "Đang giao",
            ElectroShop.Contracts.Enums.OrderStatus.Completed => "Hoàn thành",
            _ => "Đã hủy",
        };

    public static string PaymentStatus(PaymentStatus status) =>
        status switch
        {
            ElectroShop.Contracts.Enums.PaymentStatus.Success => "Thành công",
            ElectroShop.Contracts.Enums.PaymentStatus.Failed => "Thất bại",
            _ => "Chờ thanh toán",
        };
}

public class CatalogViewModel
{
    public string? Q { get; set; }
    public int? CategoryId { get; set; }
    public string? Sort { get; set; }
    public PagedResult<ProductDto> Products { get; set; } = new([], 0, 1, 12);
    public List<CategoryDto> Categories { get; set; } = [];
    public string? Error { get; set; }
}

public class ProductViewModel
{
    public ProductDto Product { get; set; } = null!;
    public List<ProductDto> Related { get; set; } = [];
}

public class CartViewModel
{
    public CartDto Cart { get; set; } = new([]);
    public string? Error { get; set; }
}

public class CheckoutViewModel
{
    public CheckoutRequest Input { get; set; } = new();
    public CartDto Cart { get; set; } = new([]);
    public OrderDto? Order { get; set; }
    public PaymentDto? Payment { get; set; }
    public Guid AttemptKey { get; set; } = Guid.NewGuid();
    public string? Error { get; set; }
}

public class OrdersViewModel
{
    public PagedResult<OrderDto> Orders { get; set; } = new([], 0, 1, 10);
    public string? Error { get; set; }
}

public class AdminViewModel
{
    public string Tab { get; set; } = "dashboard";
    public string? Q { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public string? Error { get; set; }
    public DashboardDto? Dashboard { get; set; }
    public PagedResult<OrderDto> Orders { get; set; } = new([], 0, 1, 20);
    public PagedResult<ProductDto> Products { get; set; } = new([], 0, 1, 20);
    public List<CategoryDto> Categories { get; set; } = [];
    public PagedResult<CustomerDto> Customers { get; set; } = new([], 0, 1, 20);
    public PagedResult<CustomerDto> Accounts { get; set; } = new([], 0, 1, 20);
    public List<OrderDto>? CustomerOrders { get; set; }
    public string? SelectedCustomerId { get; set; }
    public int? EditProductId { get; set; }
    public ProductRequest Product { get; set; } = new();
    public int? EditCategoryId { get; set; }
    public CategoryRequest Category { get; set; } = new();
}

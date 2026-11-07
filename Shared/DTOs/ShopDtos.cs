using ElectroShop.Contracts.Enums;
namespace ElectroShop.Contracts.DTOs;

public record CategoryDto(int Id, string Name, string Slug, string Description, bool IsActive);
public record ProductDto(int Id, int CategoryId, string CategoryName, string Name, string Slug, string SKU, string Description, decimal Price, int StockQuantity, string ImageUrl, bool IsActive, string RowVersion);
public record UserDto(string Id, string FullName, string Email, string PhoneNumber, List<string> Roles, bool IsActive, DateTimeOffset CreatedAt);
public record CartItemDto(int ProductId, string Name, string Slug, string ImageUrl, decimal UnitPrice, int Quantity, int StockQuantity, bool IsActive)
{
    public decimal LineTotal => UnitPrice * Quantity;
}
public record CartDto(List<CartItemDto> Items)
{
    public decimal TotalAmount => Items.Sum(x => x.LineTotal);
}
public record OrderItemDto(int ProductId, string ProductName, string ProductSKU, decimal UnitPrice, int Quantity, decimal LineTotal);
public record OrderDto(int Id, string OrderCode, string UserId, string CustomerName, string ReceiverName, string ReceiverPhone, string ShippingAddress, string? Note, decimal TotalAmount, OrderStatus OrderStatus, PaymentStatus PaymentStatus, DateTimeOffset CreatedAt, List<OrderItemDto> Items);
public record PaymentDto(int Id, int OrderId, string TransactionCode, string Provider, decimal Amount, PaymentStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? PaidAt, string? FailureReason);
public record CustomerDto(string Id, string FullName, string Email, string PhoneNumber, DateTimeOffset CreatedAt, bool IsActive, List<string> Roles, int OrderCount, decimal TotalSpent);
public record DashboardDto(int TotalProducts, int TotalCustomers, int TotalOrders, decimal Revenue, int PendingOrders, List<OrderDto> RecentOrders);

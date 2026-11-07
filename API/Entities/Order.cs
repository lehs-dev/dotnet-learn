using Microsoft.AspNetCore.Identity;
using ElectroShop.Contracts.Enums;
namespace ElectroShop.Api.Entities;

public class Order
{
    public int Id
    {
        get; set;
    }
    public string OrderCode { get; set; } = "";
    public Guid CheckoutKey
    {
        get; set;
    }
    public string UserId { get; set; } = "";
    public ApplicationUser User { get; set; } = null!;
    public string ReceiverName { get; set; } = "";
    public string ReceiverPhone { get; set; } = "";
    public string ShippingAddress { get; set; } = "";
    public string? Note
    {
        get; set;
    }
    public decimal TotalAmount
    {
        get; set;
    }
    public OrderStatus OrderStatus
    {
        get; set;
    }
    public PaymentStatus PaymentStatus
    {
        get; set;
    }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public byte[] RowVersion { get; set; } = [];
    public List<OrderItem> Items { get; set; } = [];
    public List<PaymentTransaction> Payments { get; set; } = [];
}

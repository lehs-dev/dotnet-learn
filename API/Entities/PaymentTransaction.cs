using ElectroShop.Contracts.Enums;
using Microsoft.AspNetCore.Identity;

namespace ElectroShop.Api.Entities;

public class PaymentTransaction
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    public Guid AttemptKey { get; set; }
    public string TransactionCode { get; set; } = "";
    public string Provider { get; set; } = "Sandbox";
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? PaidAt { get; set; }
    public string? FailureReason { get; set; }
}

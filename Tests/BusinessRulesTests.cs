using ElectroShop.Api.Entities;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
namespace ElectroShop.Tests;

public sealed class BusinessRulesTests
{
    [Theory]
    [InlineData(0, 2, 5, true, 2)]
    [InlineData(2, 3, 5, true, 5)]
    [InlineData(5, 1, 5, false, 1)]
    public void Cart_adds_or_replaces_quantity_within_stock(int current, int requested, int stock, bool add, int expected) => Assert.Equal(expected, BusinessRules.CartQuantity(current, requested, stock, add));
    [Theory]
    [InlineData(4, 2, 5, true)]
    [InlineData(0, 6, 5, false)]
    [InlineData(0, 0, 5, true)]
    [InlineData(0, -1, 5, false)]
    [InlineData(0, 1000, 5000, true)]
    public void Cart_rejects_overselling_and_invalid_quantities(int current, int requested, int stock, bool add) => Assert.Equal(409, Assert.Throws<ShopException>(() => BusinessRules.CartQuantity(current, requested, stock, add)).StatusCode);
    [Fact]
    public void Cart_total_uses_decimal_price_and_quantity()
    {
        var cart = new CartDto([new(1, "A", "a", "", 12345.67m, 3, 10, true), new(2, "B", "b", "", 999.99m, 2, 10, true)]);
        Assert.Equal(39036.99m, cart.TotalAmount);
    }
    [Fact]
    public void Order_total_uses_purchase_snapshot()
    {
        var items = new[] { new OrderItem { ProductName = "A", UnitPrice = 100.25m, Quantity = 3, Product = new Product { Price = 1000 } }, new OrderItem { UnitPrice = 50.50m, Quantity = 2 } };
        Assert.Equal(401.75m, BusinessRules.Total(items));
    }
    [Fact] public void Other_user_cannot_read_order() => Assert.Equal(404, Assert.Throws<ShopException>(() => BusinessRules.EnsureOwner("owner", "someone-else")).StatusCode);
    [Fact] public void Owner_can_read_order() => BusinessRules.EnsureOwner("owner", "owner");
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, PaymentStatus.Success, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed, PaymentStatus.Failed, false)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, PaymentStatus.Pending, true)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled, PaymentStatus.Success, false)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Shipping, PaymentStatus.Success, true)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled, PaymentStatus.Success, false)]
    [InlineData(OrderStatus.Shipping, OrderStatus.Completed, PaymentStatus.Success, true)]
    [InlineData(OrderStatus.Completed, OrderStatus.Pending, PaymentStatus.Success, false)]
    public void Order_transitions_preserve_payment_and_stock_rules(OrderStatus current, OrderStatus next, PaymentStatus payment, bool allowed) => Assert.Equal(allowed, BusinessRules.CanTransition(current, next, payment));
    [Theory]
    [InlineData(false, true, 10, 1)]
    [InlineData(true, false, 10, 1)]
    [InlineData(true, true, 0, 1)]
    [InlineData(true, true, 1, 2)]
    public void Inactive_or_insufficient_product_cannot_be_bought(bool active, bool categoryActive, int stock, int requested)
    {
        var product = new Product { Name = "Thiết bị", IsActive = active, StockQuantity = stock, Category = new Category { IsActive = categoryActive } };
        Assert.Equal(409, Assert.Throws<ShopException>(() => BusinessRules.ValidateStock(product, requested)).StatusCode);
    }
    [Theory]
    [InlineData(true, false, 0)]
    public void Sole_active_admin_cannot_be_disabled_or_demoted(bool wasAdmin, bool willRemainAdmin, int otherAdmins) => Assert.Equal(409, Assert.Throws<ShopException>(() => BusinessRules.EnsureAdminRemains(wasAdmin, willRemainAdmin, otherAdmins)).StatusCode);
    [Theory]
    [InlineData(true, true, 0)]
    [InlineData(true, false, 1)]
    [InlineData(false, false, 0)]
    public void Admin_changes_are_allowed_when_an_active_admin_remains(bool wasAdmin, bool willRemainAdmin, int otherAdmins) => BusinessRules.EnsureAdminRemains(wasAdmin, willRemainAdmin, otherAdmins);

}

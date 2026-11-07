using ElectroShop.Api.Entities;
using ElectroShop.Contracts.Enums;
namespace ElectroShop.Api.Services;

public sealed class ShopException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
public static class BusinessRules
{
    public static int CartQuantity(int current, int requested, int stock, bool add)
    {
        var quantity = add ? checked(current + requested) : requested;
        if (requested < 1 || quantity > 999 || quantity > stock)
            throw new ShopException(409, "Số lượng không hợp lệ hoặc vượt quá tồn kho.");
        return quantity;
    }
    public static void EnsureAdminRemains(bool wasAdmin, bool willRemainAdmin, int otherAdmins)
    {
        if (wasAdmin && !willRemainAdmin && otherAdmins == 0)
            throw new ShopException(409, "Không thể vô hiệu hóa tài khoản quản trị cuối cùng.");
    }
    public static decimal Total(IEnumerable<OrderItem> items) => items.Sum(x => x.UnitPrice * x.Quantity);
    public static void EnsureOwner(string ownerId, string userId)
    {
        if (ownerId != userId)
            throw new ShopException(404, "Không tìm thấy đơn hàng.");
    }
    public static bool CanTransition(OrderStatus current, OrderStatus next, PaymentStatus payment) => current == next || (current, next) switch
    {
        (OrderStatus.Pending, OrderStatus.Confirmed) => payment == PaymentStatus.Success,
        (OrderStatus.Confirmed, OrderStatus.Shipping) => payment == PaymentStatus.Success,
        (OrderStatus.Shipping, OrderStatus.Completed) => payment == PaymentStatus.Success,
        (OrderStatus.Pending, OrderStatus.Cancelled) => payment != PaymentStatus.Success,
        _ => false
    };
    public static void ValidateStock(Product product, int quantity)
    {
        if (!product.IsActive || !product.Category.IsActive || quantity < 1 || quantity > product.StockQuantity)
            throw new ShopException(409, $"Sản phẩm {product.Name} đã ẩn hoặc không đủ hàng.");
    }
}

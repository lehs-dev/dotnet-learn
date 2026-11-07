using System.Data;
using ElectroShop.Api.Data;
using ElectroShop.Api.Entities;
using ElectroShop.Api.Interfaces;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.EntityFrameworkCore;

namespace ElectroShop.Api.Services;

public class OrderService(AppDbContext db) : IOrderService
{
    private IQueryable<Order> Orders => db.Orders.Include(x => x.User).Include(x => x.Items);

    public async Task<PagedResult<OrderDto>> ListAsync(
        string userId,
        int page,
        int pageSize,
        CancellationToken ct
    )
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var q = Orders.AsNoTracking().Where(x => x.UserId == userId);
        var count = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(x => x.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return new(rows.Select(x => x.ToDto()).ToList(), count, page, pageSize);
    }

    public async Task<OrderDto> GetAsync(string userId, int orderId, CancellationToken ct)
    {
        var order =
            await Orders
                .AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, ct)
            ?? throw new ShopException(404, "Không tìm thấy đơn hàng.");
        return order.ToDto();
    }

    public async Task<OrderDto> CheckoutAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken ct
    )
    {
        if (request.CheckoutKey == Guid.Empty)
            throw new ShopException(400, "Thiếu mã chống lặp thanh toán.");
        // Serializable + unique(UserId, CheckoutKey) ngăn hai đơn cho một lần gửi form.
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var existing = await Orders.SingleOrDefaultAsync(
            x => x.UserId == userId && x.CheckoutKey == request.CheckoutKey,
            ct
        );
        if (existing is not null)
            return existing.ToDto();
        var cart = await db
            .Carts.Include(x => x.Items)
                .ThenInclude(x => x.Product)
                    .ThenInclude(x => x.Category)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is null || cart.Items.Count == 0)
            throw new ShopException(409, "Giỏ hàng đang trống.");
        foreach (var item in cart.Items)
            BusinessRules.ValidateStock(item.Product, item.Quantity);
        var order = new Order
        {
            UserId = userId,
            CheckoutKey = request.CheckoutKey,
            OrderCode = "ES-" + Guid.NewGuid().ToString("N")[..16].ToUpperInvariant(),
            ReceiverName = request.ReceiverName.Trim(),
            ReceiverPhone = request.ReceiverPhone.Trim(),
            ShippingAddress = request.ShippingAddress.Trim(),
            Note = request.Note,
            Items = cart
                .Items.Select(x => new OrderItem
                {
                    ProductId = x.ProductId,
                    ProductName = x.Product.Name,
                    ProductSKU = x.Product.SKU,
                    UnitPrice = x.Product.Price,
                    Quantity = x.Quantity,
                    LineTotal = x.Product.Price * x.Quantity,
                })
                .ToList(),
        };
        order.TotalAmount = BusinessRules.Total(order.Items);
        db.Orders.Add(order);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        order.User = await db.Users.SingleAsync(x => x.Id == userId, ct);
        return order.ToDto();
    }

    public async Task<PaymentDto> PayAsync(
        string userId,
        int orderId,
        SandboxPaymentRequest request,
        CancellationToken ct
    )
    {
        if (request.AttemptKey == Guid.Empty)
            throw new ShopException(400, "Thiếu mã lần thanh toán.");
        await using var tx = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct
        );
        var order =
            await db
                .Orders.Include(x => x.Items)
                    .ThenInclude(x => x.Product)
                        .ThenInclude(x => x.Category)
                .Include(x => x.Payments)
                .AsSplitQuery()
                .SingleOrDefaultAsync(x => x.Id == orderId && x.UserId == userId, ct)
            ?? throw new ShopException(404, "Không tìm thấy đơn hàng.");
        var repeated = order.Payments.SingleOrDefault(x => x.AttemptKey == request.AttemptKey);
        if (repeated is not null)
            return repeated.ToDto();
        var paid = order.Payments.SingleOrDefault(x => x.Status == PaymentStatus.Success);
        if (paid is not null)
            return paid.ToDto();
        if (order.OrderStatus != OrderStatus.Pending)
            throw new ShopException(409, "Đơn hàng không còn chờ thanh toán.");
        string? failure = request.Success ? null : "Thanh toán thất bại theo lựa chọn mô phỏng.";
        if (request.Success)
        {
            try
            {
                foreach (var item in order.Items)
                    BusinessRules.ValidateStock(item.Product, item.Quantity);
            }
            catch (ShopException ex)
            {
                failure = ex.Message;
            }
        }
        var payment = new PaymentTransaction
        {
            OrderId = order.Id,
            AttemptKey = request.AttemptKey,
            TransactionCode = "SB-" + Guid.NewGuid().ToString("N"),
            Amount = order.TotalAmount,
            Status = failure is null ? PaymentStatus.Success : PaymentStatus.Failed,
            FailureReason = failure,
        };
        if (failure is null)
        {
            payment.PaidAt = DateTimeOffset.UtcNow;
            foreach (var item in order.Items)
            {
                item.Product.StockQuantity -= item.Quantity;
                item.Product.UpdatedAt = DateTimeOffset.UtcNow;
            }
            // Chỉ bỏ số lượng đã mua; giữ lại phần khách vừa thêm trong lần mua khác.
            var cart = await db
                .Carts.Include(x => x.Items)
                .SingleOrDefaultAsync(x => x.UserId == userId, ct);
            if (cart is not null)
            {
                foreach (var purchased in order.Items)
                {
                    var item = cart.Items.SingleOrDefault(x => x.ProductId == purchased.ProductId);
                    if (item is null)
                        continue;
                    if (item.Quantity <= purchased.Quantity)
                        db.CartItems.Remove(item);
                    else
                        item.Quantity -= purchased.Quantity;
                }
                cart.UpdatedAt = DateTimeOffset.UtcNow;
            }
        }
        order.PaymentStatus = payment.Status;
        order.UpdatedAt = DateTimeOffset.UtcNow;
        db.PaymentTransactions.Add(payment);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return payment.ToDto();
    }
}

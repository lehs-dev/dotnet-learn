using System.Data;
using ElectroShop.Api.Data;
using ElectroShop.Api.Entities;
using ElectroShop.Api.Interfaces;
using ElectroShop.Contracts.DTOs;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Services;

public class CartService(AppDbContext db) : ICartService
{
    public async Task<CartDto> GetAsync(string userId, CancellationToken ct) => new(await db.CartItems.AsNoTracking().Where(x => x.Cart.UserId == userId)
     .OrderBy(x => x.Id).Select(x => new CartItemDto(x.ProductId, x.Product.Name, x.Product.Slug, x.Product.ImageUrl, x.Product.Price, x.Quantity, x.Product.StockQuantity, x.Product.IsActive && x.Product.Category.IsActive)).ToListAsync(ct));
    public async Task SetAsync(string userId, int productId, int quantity, bool add, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var product = await db.Products.Include(x => x.Category).SingleOrDefaultAsync(x => x.Id == productId, ct) ?? throw new ShopException(404, "Không tìm thấy sản phẩm.");
        BusinessRules.ValidateStock(product, quantity);
        var cart = await db.Carts.Include(x => x.Items).SingleOrDefaultAsync(x => x.UserId == userId, ct);
        if (cart is null)
        {
            cart = new Cart { UserId = userId };
            db.Carts.Add(cart);
        }
        var item = cart.Items.SingleOrDefault(x => x.ProductId == productId);
        int next = BusinessRules.CartQuantity(item?.Quantity ?? 0, quantity, product.StockQuantity, add);
        if (item is null)
            cart.Items.Add(new CartItem { ProductId = productId, Quantity = next });
        else
            item.Quantity = next;
        cart.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
    public async Task RemoveAsync(string userId, int? productId, CancellationToken ct)
    {
        var query = db.CartItems.Where(x => x.Cart.UserId == userId);
        if (productId.HasValue)
            query = query.Where(x => x.ProductId == productId.Value);
        await query.ExecuteDeleteAsync(ct);
    }
}

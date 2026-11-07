using ElectroShop.Api.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ElectroShop.Api.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.Property(x => x.FullName).HasMaxLength(100);
    }
}

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.Property(x => x.Name).HasMaxLength(100);
        b.Property(x => x.Slug).HasMaxLength(100);
        b.Property(x => x.Description).HasMaxLength(1000);
        b.HasIndex(x => x.Slug).IsUnique();
    }
}

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Slug).HasMaxLength(200);
        b.Property(x => x.SKU).HasMaxLength(50);
        b.Property(x => x.Description).HasMaxLength(4000);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasIndex(x => x.SKU).IsUnique();
        b.HasIndex(x => x.Slug).IsUnique();
        b.HasOne(x => x.Category)
            .WithMany(x => x.Products)
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Product_Price", "[Price] >= 0");
            t.HasCheckConstraint("CK_Product_Stock", "[StockQuantity] >= 0");
        });
    }
}

public class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> b)
    {
        b.HasIndex(x => x.UserId).IsUnique();
        b.HasOne(x => x.User)
            .WithOne()
            .HasForeignKey<Cart>(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> b)
    {
        b.HasIndex(x => new { x.CartId, x.ProductId }).IsUnique();
        b.HasOne(x => x.Cart)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.CartId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t =>
            t.HasCheckConstraint("CK_CartItem_Quantity", "[Quantity] > 0 AND [Quantity] <= 999")
        );
    }
}

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.Property(x => x.OrderCode).HasMaxLength(40);
        b.HasIndex(x => x.OrderCode).IsUnique();
        b.HasIndex(x => new { x.UserId, x.CheckoutKey }).IsUnique();
        b.HasIndex(x => x.CreatedAt);
        b.Property(x => x.ReceiverName).HasMaxLength(100);
        b.Property(x => x.ReceiverPhone).HasMaxLength(20);
        b.Property(x => x.ShippingAddress).HasMaxLength(500);
        b.Property(x => x.Note).HasMaxLength(1000);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.Property(x => x.RowVersion).IsConcurrencyToken();
        b.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Order_Total", "[TotalAmount] >= 0"));
    }
}

public class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> b)
    {
        b.Property(x => x.ProductName).HasMaxLength(200);
        b.Property(x => x.ProductSKU).HasMaxLength(50);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.LineTotal).HasPrecision(18, 2);
        b.HasOne(x => x.Order)
            .WithMany(x => x.Items)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderItem_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint(
                "CK_OrderItem_Price",
                "[UnitPrice] >= 0 AND [LineTotal] = [UnitPrice] * [Quantity]"
            );
        });
    }
}

public class PaymentConfiguration : IEntityTypeConfiguration<PaymentTransaction>
{
    public void Configure(EntityTypeBuilder<PaymentTransaction> b)
    {
        b.Property(x => x.TransactionCode).HasMaxLength(40);
        b.Property(x => x.Provider).HasMaxLength(30);
        b.Property(x => x.FailureReason).HasMaxLength(500);
        b.Property(x => x.Amount).HasPrecision(18, 2);
        b.HasIndex(x => x.TransactionCode).IsUnique();
        b.HasIndex(x => new { x.OrderId, x.AttemptKey }).IsUnique();
        // Chỉ một giao dịch thành công cho mỗi đơn, kể cả khi hai request tới đồng thời.
        b.HasIndex(x => x.OrderId).IsUnique().HasFilter("[Status] = 1");
        b.HasOne(x => x.Order)
            .WithMany(x => x.Payments)
            .HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        b.ToTable(t => t.HasCheckConstraint("CK_Payment_Amount", "[Amount] >= 0"));
    }
}

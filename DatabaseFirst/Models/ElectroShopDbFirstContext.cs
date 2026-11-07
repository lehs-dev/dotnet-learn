using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace ElectroShop.DatabaseFirst.Models;

public partial class ElectroShopDbFirstContext : DbContext
{
    public ElectroShopDbFirstContext() { }

    public ElectroShopDbFirstContext(DbContextOptions<ElectroShopDbFirstContext> options)
        : base(options) { }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Product> Products { get; set; }

    // Scaffold sinh OnConfiguring chứa chuỗi kết nối; đã thay bằng biến môi trường để KHÔNG
    // đưa mật khẩu vào source code. Lệnh scaffold gốc (chạy lại khi schema thay đổi):
    //   dotnet ef dbcontext scaffold "$ConnectionStrings__DbFirst" Microsoft.EntityFrameworkCore.SqlServer \
    //     --project DatabaseFirst --output-dir Models --context ElectroShopDbFirstContext --context-dir Models --force
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
            optionsBuilder.UseSqlServer(
                Environment.GetEnvironmentVariable("ConnectionStrings__DbFirst")
                    ?? throw new InvalidOperationException(
                        "Hãy đặt biến môi trường ConnectionStrings__DbFirst."
                    )
            );
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasIndex(e => e.Slug, "IX_Categories_Slug").IsUnique();

            entity.Property(e => e.IsActive).HasDefaultValue(true, "DF_Categories_IsActive");
            entity.Property(e => e.Name).HasMaxLength(100);
            entity.Property(e => e.Slug).HasMaxLength(100);
        });

        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasIndex(e => e.CategoryId, "IX_Products_CategoryId");

            entity
                .Property(e => e.CreatedAt)
                .HasDefaultValueSql("(sysdatetimeoffset())", "DF_Products_CreatedAt");
            entity
                .Property(e => e.ImageUrl)
                .HasMaxLength(500)
                .HasDefaultValue("", "DF_Products_Image");
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Price).HasColumnType("decimal(18, 2)");

            entity
                .HasOne(d => d.Category)
                .WithMany(p => p.Products)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_Products_Categories");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}

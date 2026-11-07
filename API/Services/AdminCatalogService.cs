using ElectroShop.Api.Data;
using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Services;

public class AdminCatalogService(AppDbContext db)
{
    public async Task<PagedResult<ProductDto>> ListAsync(string? q, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Products.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || x.SKU.Contains(q));
        return new(await query.OrderBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).Select(Mapping.ProductProjection).ToListAsync(ct), await query.CountAsync(ct), page, pageSize);
    }
    public async Task<ProductDto> SaveAsync(int? id, ProductRequest r, CancellationToken ct)
    {
        if (!Uri.TryCreate(r.ImageUrl, UriKind.RelativeOrAbsolute, out var image) || (image.IsAbsoluteUri && image.Scheme != "https" && image.Scheme != "http") || r.ImageUrl.StartsWith("//"))
            throw new ShopException(400, "Đường dẫn ảnh không hợp lệ.");
        var category = await db.Categories.SingleOrDefaultAsync(x => x.Id == r.CategoryId, ct) ?? throw new ShopException(400, "Danh mục không tồn tại.");
        var product = id.HasValue ? await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ShopException(404, "Không tìm thấy sản phẩm.") : new Product();
        if (id.HasValue)
        {
            if (string.IsNullOrEmpty(r.RowVersion))
                throw new ShopException(409, "Thiếu phiên bản sản phẩm. Vui lòng tải lại.");
            byte[] version;
            try
            {
                version = Convert.FromBase64String(r.RowVersion);
            }
            catch (FormatException) { throw new ShopException(400, "Phiên bản sản phẩm không hợp lệ."); }
            db.Entry(product).Property(x => x.RowVersion).OriginalValue = version;
        }
        product.Name = r.Name.Trim();
        product.Slug = r.Slug.Trim();
        product.SKU = r.SKU.Trim();
        product.Description = r.Description.Trim();
        product.Price = r.Price;
        product.StockQuantity = r.StockQuantity;
        product.CategoryId = r.CategoryId;
        product.Category = category;
        product.ImageUrl = r.ImageUrl.Trim();
        product.IsActive = r.IsActive;
        product.UpdatedAt = DateTimeOffset.UtcNow;
        if (!id.HasValue)
            db.Products.Add(product);
        await db.SaveChangesAsync(ct);
        return product.ToDto();
    }
    public async Task HideProductAsync(int id, CancellationToken ct)
    {
        var p = await db.Products.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw new ShopException(404, "Không tìm thấy sản phẩm.");
        p.IsActive = false;
        p.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
    }
    public async Task<CategoryDto> SaveCategoryAsync(int? id, CategoryRequest r, CancellationToken ct)
    {
        var category = id.HasValue ? await db.Categories.FindAsync([id.Value], ct) ?? throw new ShopException(404, "Không tìm thấy danh mục.") : new Category();
        category.Name = r.Name.Trim();
        category.Slug = r.Slug.Trim();
        category.Description = r.Description.Trim();
        category.IsActive = r.IsActive;
        if (!id.HasValue)
            db.Categories.Add(category);
        await db.SaveChangesAsync(ct);
        return category.ToDto();
    }
    public async Task HideCategoryAsync(int id, CancellationToken ct)
    {
        var c = await db.Categories.FindAsync([id], ct) ?? throw new ShopException(404, "Không tìm thấy danh mục.");
        c.IsActive = false;
        await db.SaveChangesAsync(ct);
    }
}

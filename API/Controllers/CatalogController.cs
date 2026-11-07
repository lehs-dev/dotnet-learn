using ElectroShop.Api.Data;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Controllers;

[ApiController, Route("api")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    [HttpGet("categories")]
    public async Task<List<CategoryDto>> Categories(CancellationToken ct) => await db.Categories.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Id).Select(x => new CategoryDto(x.Id, x.Name, x.Slug, x.Description, x.IsActive)).ToListAsync(ct);
    [HttpGet("products")]
    public async Task<PagedResult<ProductDto>> Products(string? q, int? categoryId, string? sort, int page = 1, int pageSize = 12, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Products.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q.Trim()));
        if (categoryId.HasValue)
            query = query.Where(x => x.CategoryId == categoryId);
        var count = await query.CountAsync(ct);
        query = sort switch
        {
            "price-asc" => query.OrderBy(x => x.Price).ThenBy(x => x.Id),
            "price-desc" => query.OrderByDescending(x => x.Price).ThenBy(x => x.Id),
            _ => query.OrderBy(x => x.Id)
        };
        return new(await query.Skip((page - 1) * pageSize).Take(pageSize).Select(Mapping.ProductProjection).ToListAsync(ct), count, page, pageSize);
    }
    [HttpGet("products/{slug}")]
    public async Task<ProductDto> Product(string slug, CancellationToken ct) => await db.Products.AsNoTracking().Where(x => x.IsActive && x.Category.IsActive && x.Slug == slug).Select(Mapping.ProductProjection).SingleOrDefaultAsync(ct) ?? throw new ShopException(404, "Không tìm thấy sản phẩm.");
}

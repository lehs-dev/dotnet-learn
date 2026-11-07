using ElectroShop.Api.Data;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Controllers;

[ApiController, Authorize(Roles = "Admin"), Route("api/admin")]
public class AdminController(AdminService service, AdminCatalogService catalog, AppDbContext db) : ControllerBase
{
    [HttpGet("dashboard")] public Task<DashboardDto> Dashboard(CancellationToken ct) => service.DashboardAsync(ct);
    [HttpGet("products")] public Task<PagedResult<ProductDto>> Products(string? q, int page = 1, int pageSize = 20, CancellationToken ct = default) => catalog.ListAsync(q, page, pageSize, ct);
    [HttpPost("products")] public Task<ProductDto> AddProduct(ProductRequest r, CancellationToken ct) => catalog.SaveAsync(null, r, ct);
    [HttpPut("products/{id:int}")] public Task<ProductDto> UpdateProduct(int id, ProductRequest r, CancellationToken ct) => catalog.SaveAsync(id, r, ct);
    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> HideProduct(int id, CancellationToken ct)
    {
        await catalog.HideProductAsync(id, ct);
        return NoContent();
    }
    [HttpGet("categories")] public async Task<List<CategoryDto>> Categories(CancellationToken ct) => (await db.Categories.AsNoTracking().OrderBy(x => x.Id).ToListAsync(ct)).Select(x => x.ToDto()).ToList();
    [HttpPost("categories")] public Task<CategoryDto> AddCategory(CategoryRequest r, CancellationToken ct) => catalog.SaveCategoryAsync(null, r, ct);
    [HttpPut("categories/{id:int}")] public Task<CategoryDto> UpdateCategory(int id, CategoryRequest r, CancellationToken ct) => catalog.SaveCategoryAsync(id, r, ct);
    [HttpDelete("categories/{id:int}")]
    public async Task<IActionResult> HideCategory(int id, CancellationToken ct)
    {
        await catalog.HideCategoryAsync(id, ct);
        return NoContent();
    }
    [HttpGet("orders")] public Task<PagedResult<OrderDto>> Orders(string? q, OrderStatus? status, int page = 1, int pageSize = 20, CancellationToken ct = default) => service.OrdersAsync(q, status, page, pageSize, ct);
    [HttpPut("orders/{id:int}/status")] public Task<OrderDto> Status(int id, OrderStatusRequest r, CancellationToken ct) => service.StatusAsync(id, r.Status, ct);
    [HttpGet("customers")] public Task<PagedResult<CustomerDto>> Customers(string? q, int page = 1, int pageSize = 20, CancellationToken ct = default) => service.CustomersAsync(q, page, pageSize, true, ct);
    [HttpGet("customers/{id}/orders")] public Task<List<OrderDto>> CustomerOrders(string id, CancellationToken ct) => service.CustomerOrdersAsync(id, ct);
    [HttpPut("customers/{id}")]
    public async Task<IActionResult> Profile(string id, CustomerProfileRequest r, CancellationToken ct)
    {
        await service.UpdateProfileAsync(id, r, ct);
        return NoContent();
    }
    [HttpGet("accounts")] public Task<PagedResult<CustomerDto>> Accounts(string? q, int page = 1, int pageSize = 20, CancellationToken ct = default) => service.CustomersAsync(q, page, pageSize, false, ct);
    [HttpPut("accounts/{id}/access")]
    public async Task<IActionResult> Access(string id, AccountAccessRequest r, CancellationToken ct)
    {
        await service.AccessAsync(id, r, ct);
        return NoContent();
    }
}

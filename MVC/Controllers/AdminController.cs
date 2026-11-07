using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using System.Net;
using System.Security.Claims;
using ElectroShop.Mvc.Services;
using ElectroShop.Mvc.ViewModels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.Mvc.Controllers;

[Authorize(Roles = "Admin"), Route("admin")]
public sealed class AdminController(ShopApiClient api, TokenStore tokens) : Controller
{
    // Đăng nhập quản trị RIÊNG tại /admin/login: gọi api/auth/admin/login của API.
    // API chỉ cấp token khi tài khoản có vai trò Admin; tài khoản khách hàng bị từ chối (403).
    // Tài khoản quản trị được tạo riêng bằng seed hoặc lệnh --create-admin của API,
    // không thể tạo qua form đăng ký khách hàng (đăng ký luôn gán vai trò Customer).
    [AllowAnonymous, HttpGet("login")]
    public IActionResult Login()
    {
        if (User.IsInRole("Admin"))
            return Redirect("/admin");
        return View("Login");
    }
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Vui lòng nhập email và mật khẩu hợp lệ.";
            return Redirect("/admin/login");
        }
        try
        {
            var auth = await api.PostAsync<AuthResponse>("api/auth/admin/login", input, ct)
                ?? throw new ApiException("Không nhận được phiên đăng nhập quản trị.", HttpStatusCode.BadGateway);
            if (!auth.User.Roles.Contains("Admin"))
            {
                TempData["Error"] = "Tài khoản này không có quyền quản trị.";
                return Redirect("/admin/login");
            }
            tokens.Remove(User.FindFirst("session_id")?.Value);
            var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, auth.User.Id), new(ClaimTypes.Name, auth.User.FullName), new(ClaimTypes.Email, auth.User.Email), new("session_id", tokens.Add(auth)) };
            claims.AddRange(auth.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { ExpiresUtc = auth.ExpiresAt, IsPersistent = false });
            TempData["Success"] = "Đăng nhập quản trị thành công.";
            return Redirect("/admin");
        }
        catch (ApiException ex)
        {
            TempData["Error"] = ex.Message;
            return Redirect("/admin/login");
        }
    }
    private static readonly string[] Tabs = ["dashboard", "orders", "products", "customers", "accounts"];
    [HttpGet("")]
    public async Task<IActionResult> Index(string tab = "dashboard", string? q = null, string? status = null, int page = 1, int? editProductId = null, int? editCategoryId = null, string? customerId = null, CancellationToken ct = default)
    {
        var model = new AdminViewModel { Tab = Tabs.Contains(tab) ? tab : "dashboard", Q = q, Status = status, Page = Math.Max(1, page), EditProductId = editProductId, EditCategoryId = editCategoryId, SelectedCustomerId = customerId };
        var query = $"q={Uri.EscapeDataString(q ?? "")}&page={model.Page}&pageSize=20";
        try
        {
            switch (model.Tab)
            {
                case "dashboard":
                    model.Dashboard = await api.GetAsync<DashboardDto>("api/admin/dashboard", ct);
                    break;
                case "orders":
                    model.Orders = await api.GetAsync<PagedResult<OrderDto>>($"api/admin/orders?{query}&status={Uri.EscapeDataString(status ?? "")}", ct);
                    break;
                case "products":
                    model.Products = await api.GetAsync<PagedResult<ProductDto>>($"api/admin/products?{query}", ct);
                    model.Categories = await api.GetAsync<List<CategoryDto>>("api/admin/categories", ct);
                    if (editProductId.HasValue && model.Products.Items.FirstOrDefault(x => x.Id == editProductId) is { } p)
                        model.Product = new ProductRequest { CategoryId = p.CategoryId, Name = p.Name, Slug = p.Slug, SKU = p.SKU, Description = p.Description, Price = p.Price, StockQuantity = p.StockQuantity, ImageUrl = p.ImageUrl, IsActive = p.IsActive, RowVersion = p.RowVersion };
                    else
                        model.EditProductId = null;
                    if (editCategoryId.HasValue && model.Categories.FirstOrDefault(x => x.Id == editCategoryId) is { } c)
                        model.Category = new CategoryRequest { Name = c.Name, Slug = c.Slug, Description = c.Description, IsActive = c.IsActive };
                    else
                        model.EditCategoryId = null;
                    break;
                case "customers":
                    model.Customers = await api.GetAsync<PagedResult<CustomerDto>>($"api/admin/customers?{query}", ct);
                    if (!string.IsNullOrEmpty(customerId))
                        model.CustomerOrders = await api.GetAsync<List<OrderDto>>($"api/admin/customers/{Uri.EscapeDataString(customerId)}/orders", ct);
                    break;
                case "accounts":
                    model.Accounts = await api.GetAsync<PagedResult<CustomerDto>>($"api/admin/accounts?{query}", ct);
                    break;
            }
        }
        catch (ApiException ex) { model.Error = ex.Message; }
        return View(model);
    }
    [HttpPost("product")]
    public async Task<IActionResult> SaveProduct(int? id, [Bind(Prefix = "Product")] ProductRequest input, CancellationToken ct)
    {
        input.RowVersion ??= "";
        if (!ModelState.IsValid)
            return Invalid("products");
        return await Write("products", async () => { if (id.HasValue) await api.PutAsync<ProductDto>($"api/admin/products/{id}", input, ct); else await api.PostAsync<ProductDto>("api/admin/products", input, ct); }, "Đã lưu sản phẩm.");
    }
    [HttpPost("product/hide")]
    public Task<IActionResult> HideProduct(int id, CancellationToken ct) => Write("products", () => api.DeleteAsync($"api/admin/products/{id}", ct), "Đã ẩn sản phẩm; lịch sử đơn hàng được giữ lại.");
    [HttpPost("category")]
    public async Task<IActionResult> SaveCategory(int? id, [Bind(Prefix = "Category")] CategoryRequest input, CancellationToken ct)
    {
        input.Description ??= "";
        if (!ModelState.IsValid)
            return Invalid("products");
        return await Write("products", async () => { if (id.HasValue) await api.PutAsync<CategoryDto>($"api/admin/categories/{id}", input, ct); else await api.PostAsync<CategoryDto>("api/admin/categories", input, ct); }, "Đã lưu danh mục.");
    }
    [HttpPost("category/hide")]
    public Task<IActionResult> HideCategory(int id, CancellationToken ct) => Write("products", () => api.DeleteAsync($"api/admin/categories/{id}", ct), "Đã ẩn danh mục.");
    [HttpPost("order/status")]
    public async Task<IActionResult> ChangeOrder(int id, OrderStatusRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return Invalid("orders");
        return await Write("orders", async () => { await api.PutAsync<OrderDto>($"api/admin/orders/{id}/status", input, ct); }, "Đã cập nhật trạng thái đơn hàng.");
    }
    [HttpPost("customer")]
    public async Task<IActionResult> SaveCustomer(string id, CustomerProfileRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || !ModelState.IsValid)
            return Invalid("customers");
        return await Write("customers", async () => { await api.PutAsync<object?>($"api/admin/customers/{Uri.EscapeDataString(id)}", input, ct); }, "Đã cập nhật hồ sơ khách hàng.");
    }
    [HttpPost("account")]
    public async Task<IActionResult> SaveAccount(string id, AccountAccessRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id) || !ModelState.IsValid)
            return Invalid("accounts");
        return await Write("accounts", async () => { await api.PutAsync<object?>($"api/admin/accounts/{Uri.EscapeDataString(id)}/access", input, ct); }, "Đã cập nhật quyền và trạng thái tài khoản.");
    }
    private IActionResult Invalid(string tab)
    {
        TempData["Error"] = "Dữ liệu chưa hợp lệ. Vui lòng kiểm tra các trường bắt buộc, giá và số lượng.";
        return RedirectToAction(nameof(Index), new
        {
            tab
        });
    }
    private async Task<IActionResult> Write(string tab, Func<Task> action, string success)
    {
        try
        {
            await action();
            TempData["Success"] = success;
        }
        catch (ApiException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Index), new
        {
            tab
        });
    }
}

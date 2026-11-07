using System.Security.Claims;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages.Admin;

[Authorize(Roles = "Admin")]
public sealed class IndexModel(ShopApiClient api, TokenStore tokens) : ShopPageModel
{
    [BindProperty(SupportsGet = true)]
    public string Tab { get; set; } = "dashboard";

    [BindProperty(SupportsGet = true)]
    public string? Q { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Status { get; set; }

    [BindProperty(SupportsGet = true), FromQuery(Name = "page")]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public string? CustomerId { get; set; }
    public DashboardDto? Dashboard { get; set; }
    public PagedResult<ProductDto>? Products { get; set; }
    public List<CategoryDto> Categories { get; set; } = [];
    public PagedResult<OrderDto>? Orders { get; set; }
    public PagedResult<CustomerDto>? Customers { get; set; }
    public List<OrderDto>? CustomerOrders { get; set; }
    public int TotalPages =>
        Products?.TotalPages ?? Orders?.TotalPages ?? Customers?.TotalPages ?? 0;
    public int TotalCount =>
        Products?.TotalCount ?? Orders?.TotalCount ?? Customers?.TotalCount ?? 0;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (Tab is not ("dashboard" or "orders" or "products" or "customers" or "accounts"))
            Tab = "dashboard";
        try
        {
            var query =
                $"q={Uri.EscapeDataString(Q ?? "")}&page={Math.Max(1, PageNumber)}&pageSize=10";
            switch (Tab)
            {
                case "dashboard":
                    Dashboard = await api.GetAsync<DashboardDto>("api/admin/dashboard", ct);
                    break;
                case "orders":
                    Orders = await api.GetAsync<PagedResult<OrderDto>>(
                        $"api/admin/orders?{query}&status={Uri.EscapeDataString(Status ?? "")}",
                        ct
                    );
                    break;
                case "products":
                    Products = await api.GetAsync<PagedResult<ProductDto>>(
                        $"api/admin/products?{query}",
                        ct
                    );
                    Categories = await api.GetAsync<List<CategoryDto>>("api/admin/categories", ct);
                    break;
                default:
                    Customers = await api.GetAsync<PagedResult<CustomerDto>>(
                        $"api/admin/{Tab}?{query}",
                        ct
                    );
                    if (Tab == "customers" && !string.IsNullOrEmpty(CustomerId))
                        CustomerOrders = await api.GetAsync<List<OrderDto>>(
                            $"api/admin/customers/{Uri.EscapeDataString(CustomerId)}/orders",
                            ct
                        );
                    break;
            }
        }
        catch (ApiException ex)
        {
            Error = ex.Message;
        }
    }

    private IActionResult AdminRedirect(string tab, string message, bool success = false)
    {
        var returnUrl = Request.Form["returnUrl"].ToString();
        var target =
            Url.IsLocalUrl(returnUrl)
            && (returnUrl == "/admin" || returnUrl.StartsWith("/admin?", StringComparison.Ordinal))
                ? returnUrl
                : "/admin?tab=" + tab;
        return MessageRedirect(target, message, success);
    }

    private IActionResult Invalid(string tab) =>
        AdminRedirect(
            tab,
            string.Join(
                " ",
                ModelState
                    .Values.SelectMany(x => x.Errors)
                    .Select(x =>
                        string.IsNullOrEmpty(x.ErrorMessage)
                            ? "Dữ liệu không hợp lệ."
                            : x.ErrorMessage
                    )
            )
        );

    public async Task<IActionResult> OnPostProductAsync(
        int id,
        ProductRequest product,
        CancellationToken ct
    )
    {
        product.RowVersion ??= "";
        if (!ModelState.IsValid)
            return Invalid("products");
        try
        {
            if (id == 0)
                await api.PostAsync<ProductDto>("api/admin/products", product, ct);
            else
                await api.PutAsync<ProductDto>($"api/admin/products/{id}", product, ct);
            return AdminRedirect("products", "Đã lưu sản phẩm.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("products", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostHideProductAsync(int id, CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync($"api/admin/products/{id}", ct);
            return AdminRedirect(
                "products",
                "Đã ẩn sản phẩm. Lịch sử đơn hàng được giữ nguyên.",
                true
            );
        }
        catch (ApiException ex)
        {
            return AdminRedirect("products", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostCategoryAsync(
        int id,
        CategoryRequest category,
        CancellationToken ct
    )
    {
        category.Description ??= "";
        if (!ModelState.IsValid)
            return Invalid("products");
        try
        {
            if (id == 0)
                await api.PostAsync<CategoryDto>("api/admin/categories", category, ct);
            else
                await api.PutAsync<CategoryDto>($"api/admin/categories/{id}", category, ct);
            return AdminRedirect("products", "Đã lưu danh mục.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("products", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostHideCategoryAsync(int id, CancellationToken ct)
    {
        try
        {
            await api.DeleteAsync($"api/admin/categories/{id}", ct);
            return AdminRedirect("products", "Đã ẩn danh mục.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("products", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostOrderStatusAsync(
        int id,
        OrderStatus status,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
            return Invalid("orders");
        try
        {
            await api.PutAsync<OrderDto>(
                $"api/admin/orders/{id}/status",
                new OrderStatusRequest { Status = status },
                ct
            );
            return AdminRedirect("orders", "Đã cập nhật trạng thái đơn hàng.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("orders", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostProfileAsync(
        string id,
        CustomerProfileRequest profile,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
            return Invalid("customers");
        try
        {
            await api.PutAsync<object>(
                $"api/admin/customers/{Uri.EscapeDataString(id)}",
                profile,
                ct
            );
            return AdminRedirect("customers", "Đã cập nhật hồ sơ khách hàng.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("customers", ex.Message);
        }
    }

    public async Task<IActionResult> OnPostAccessAsync(
        string id,
        AccountAccessRequest access,
        CancellationToken ct
    )
    {
        if (!ModelState.IsValid)
            return Invalid("accounts");
        try
        {
            await api.PutAsync<object>(
                $"api/admin/accounts/{Uri.EscapeDataString(id)}/access",
                access,
                ct
            );
            if (
                id == User.FindFirstValue(ClaimTypes.NameIdentifier)
                && (access.Role != "Admin" || !access.IsActive)
            )
            {
                tokens.Remove(User.FindFirstValue("session"));
                await HttpContext.SignOutAsync();
                return MessageRedirect(
                    "/admin/login",
                    "Quyền tài khoản đã thay đổi. Vui lòng đăng nhập lại.",
                    true
                );
            }
            return AdminRedirect("accounts", "Đã cập nhật quyền và trạng thái tài khoản.", true);
        }
        catch (ApiException ex)
        {
            return AdminRedirect("accounts", ex.Message);
        }
    }

    public static IEnumerable<OrderStatus> NextStatuses(OrderDto order) =>
        order.OrderStatus switch
        {
            OrderStatus.Pending => order.PaymentStatus == PaymentStatus.Success
                ? [OrderStatus.Confirmed]
                : [OrderStatus.Cancelled],
            OrderStatus.Confirmed when order.PaymentStatus == PaymentStatus.Success =>
            [
                OrderStatus.Shipping,
            ],
            OrderStatus.Shipping when order.PaymentStatus == PaymentStatus.Success =>
            [
                OrderStatus.Completed,
            ],
            _ => [],
        };
}

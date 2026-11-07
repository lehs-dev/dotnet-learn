using System.Net;
using System.Net.Http.Json;
using ElectroShop.Api.Data;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Enums;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ElectroShop.Tests;

[Collection("SQL Server")]
public sealed class SystemFlowsTests(SqlServerFixture fixture)
{
    [Theory]
    [InlineData("customers")]
    [InlineData("accounts")]
    public async Task Admin_lists_support_SQLite_search_pagination_roles_and_purchase_totals(
        string resource
    )
    {
        var (customer, user) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(price: 19.25m);
        (
            await customer.PostAsJsonAsync(
                "/api/cart/items",
                new CartQuantityRequest { ProductId = product.Id, Quantity = 2 }
            )
        ).EnsureSuccessStatusCode();
        var order = await CreateOrder(customer);
        (
            await customer.PostAsJsonAsync(
                $"/api/payments/{order.Id}",
                new SandboxPaymentRequest(),
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();

        using var admin = await fixture.AdminAsync();
        var page = (
            await admin.GetFromJsonAsync<PagedResult<CustomerDto>>(
                $"/api/admin/{resource}?q={Uri.EscapeDataString(user.Email)}&pageSize=1",
                SqlServerFixture.Json
            )
        )!;
        var row = Assert.Single(page.Items);
        Assert.Equal(user.Id, row.Id);
        Assert.Equal(user.CreatedAt, row.CreatedAt);
        Assert.Contains("Customer", row.Roles);
        Assert.Equal(1, row.OrderCount);
        Assert.Equal(38.50m, row.TotalSpent);
        Assert.Equal(1, page.TotalCount);
        var second = (
            await admin.GetFromJsonAsync<PagedResult<CustomerDto>>(
                $"/api/admin/{resource}?q={Uri.EscapeDataString(user.Email)}&pageSize=1&page=2",
                SqlServerFixture.Json
            )
        )!;
        Assert.Empty(second.Items);
        Assert.Equal(1, second.TotalCount);
    }

    [Theory]
    [InlineData("price-asc", false)]
    [InlineData("price-desc", true)]
    public async Task Catalog_sorts_prices_numerically_in_SQLite(string sort, bool descending)
    {
        var marker = "sort-" + Guid.NewGuid().ToString("N");
        var prices = new[] { 2.50m, 10.25m, 100m, 999999999998.99m, 999999999999m };
        foreach (var price in prices)
        {
            var p = await fixture.ProductAsync(price: price);
            using var scope = fixture.Factory.Services.CreateScope();
            await scope
                .ServiceProvider.GetRequiredService<AppDbContext>()
                .Products.Where(x => x.Id == p.Id)
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.Name, marker));
        }
        using var client = fixture.Factory.CreateClient();
        var page = (
            await client.GetFromJsonAsync<PagedResult<ProductDto>>(
                $"/api/products?q={marker}&sort={sort}",
                SqlServerFixture.Json
            )
        )!;
        Assert.Equal(descending ? prices.Reverse() : prices, page.Items.Select(x => x.Price));
    }

    [Fact]
    public async Task Saving_unchanged_admin_access_preserves_the_current_session()
    {
        using var admin = await fixture.AdminAsync();
        var me = (await admin.GetFromJsonAsync<UserDto>("/api/auth/me", SqlServerFixture.Json))!;
        var saved = await admin.PutAsJsonAsync(
            $"/api/admin/accounts/{me.Id}/access",
            new AccountAccessRequest { Role = "Admin", IsActive = true }
        );
        Assert.Equal(HttpStatusCode.NoContent, saved.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync("/api/admin/accounts")).StatusCode);
    }

    [Fact]
    public async Task Changed_account_access_revokes_old_tokens_and_blocks_login_until_unlocked()
    {
        var (customer, user) = await fixture.CustomerAsync();
        using var admin = await fixture.AdminAsync();
        (
            await admin.PutAsJsonAsync(
                $"/api/admin/accounts/{user.Id}/access",
                new AccountAccessRequest { Role = "Customer", IsActive = false }
            )
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await customer.GetAsync("/api/auth/me")).StatusCode
        );
        (
            await admin.PutAsJsonAsync(
                $"/api/admin/accounts/{user.Id}/access",
                new AccountAccessRequest { Role = "Customer", IsActive = true }
            )
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await customer.GetAsync("/api/cart")).StatusCode
        );
    }

    [Fact]
    public async Task Profile_updates_and_phone_search_are_visible_to_customers_and_admins()
    {
        var (customer, user) = await fixture.CustomerAsync();
        using var admin = await fixture.AdminAsync();
        var profile = new CustomerProfileRequest
        {
            FullName = "Hồ sơ đã cập nhật",
            PhoneNumber = "0987654321",
        };
        (
            await admin.PutAsJsonAsync($"/api/admin/customers/{user.Id}", profile)
        ).EnsureSuccessStatusCode();
        var me = (await customer.GetFromJsonAsync<UserDto>("/api/auth/me", SqlServerFixture.Json))!;
        Assert.Equal(profile.FullName, me.FullName);
        Assert.Equal(profile.PhoneNumber, me.PhoneNumber);
        var results = (
            await admin.GetFromJsonAsync<PagedResult<CustomerDto>>(
                "/api/admin/customers?q=%200987654321%20",
                SqlServerFixture.Json
            )
        )!;
        Assert.Contains(results.Items, x => x.Id == user.Id);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (
                await admin.PutAsJsonAsync(
                    $"/api/admin/customers/{user.Id}",
                    new CustomerProfileRequest()
                )
            ).StatusCode
        );
    }

    [Fact]
    public async Task Category_and_product_CRUD_hide_and_restore_catalog_without_losing_history()
    {
        var product = await fixture.ProductAsync();
        using var admin = await fixture.AdminAsync();
        var category = new CategoryRequest
        {
            Name = "Danh mục đã sửa",
            Slug = "updated-" + Guid.NewGuid().ToString("N"),
            Description = "",
        };
        (
            await admin.PutAsJsonAsync($"/api/admin/categories/{product.CategoryId}", category)
        ).EnsureSuccessStatusCode();
        var update = new ProductRequest
        {
            Name = "Sản phẩm đã sửa",
            Slug = product.Slug,
            SKU = product.SKU,
            CategoryId = product.CategoryId,
            Price = 456.78m,
            StockQuantity = 3,
            Description = "Nội dung đã cập nhật",
            RowVersion = product.RowVersion,
        };
        (
            await admin.PutAsJsonAsync(
                $"/api/admin/products/{product.Id}",
                update,
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();
        using var client = fixture.Factory.CreateClient();
        var current = (
            await client.GetFromJsonAsync<ProductDto>(
                $"/api/products/{product.Slug}",
                SqlServerFixture.Json
            )
        )!;
        Assert.Equal(category.Name, current.CategoryName);
        Assert.Equal(update.Price, current.Price);
        (
            await admin.DeleteAsync($"/api/admin/categories/{product.CategoryId}")
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/products/{product.Slug}")).StatusCode
        );
        (
            await admin.PutAsJsonAsync($"/api/admin/categories/{product.CategoryId}", category)
        ).EnsureSuccessStatusCode();
        (await admin.DeleteAsync($"/api/admin/products/{product.Id}")).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/products/{product.Slug}")).StatusCode
        );
        var hidden = (
            await admin.GetFromJsonAsync<PagedResult<ProductDto>>(
                $"/api/admin/products?q={product.SKU}",
                SqlServerFixture.Json
            )
        )!.Items.Single();
        update.RowVersion = hidden.RowVersion;
        (
            await admin.PutAsJsonAsync(
                $"/api/admin/products/{product.Id}",
                update,
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.OK,
            (await client.GetAsync($"/api/products/{product.Slug}")).StatusCode
        );
    }

    [Fact]
    public async Task Paid_orders_follow_fulfillment_transitions_and_appear_in_customer_history()
    {
        var (customer, user) = await fixture.CustomerAsync();
        var p = await fixture.ProductAsync();
        (
            await customer.PostAsJsonAsync(
                "/api/cart/items",
                new CartQuantityRequest { ProductId = p.Id }
            )
        ).EnsureSuccessStatusCode();
        var order = await CreateOrder(customer);
        using var admin = await fixture.AdminAsync();
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await admin.PutAsJsonAsync(
                    $"/api/admin/orders/{order.Id}/status",
                    new OrderStatusRequest { Status = OrderStatus.Confirmed },
                    SqlServerFixture.Json
                )
            ).StatusCode
        );
        (
            await customer.PostAsJsonAsync(
                $"/api/payments/{order.Id}",
                new SandboxPaymentRequest(),
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();
        foreach (
            var status in new[]
            {
                OrderStatus.Confirmed,
                OrderStatus.Shipping,
                OrderStatus.Completed,
            }
        )
            (
                await admin.PutAsJsonAsync(
                    $"/api/admin/orders/{order.Id}/status",
                    new OrderStatusRequest { Status = status },
                    SqlServerFixture.Json
                )
            ).EnsureSuccessStatusCode();
        var history = (
            await admin.GetFromJsonAsync<List<OrderDto>>(
                $"/api/admin/customers/{user.Id}/orders",
                SqlServerFixture.Json
            )
        )!;
        Assert.Equal(OrderStatus.Completed, Assert.Single(history).OrderStatus);
        var dashboard = (
            await admin.GetFromJsonAsync<DashboardDto>(
                "/api/admin/dashboard",
                SqlServerFixture.Json
            )
        )!;
        Assert.True(dashboard.Revenue >= p.Price);
    }

    [Fact]
    public async Task Cancelled_unpaid_orders_cannot_be_paid_and_leave_stock_and_cart_intact()
    {
        var (customer, _) = await fixture.CustomerAsync();
        var p = await fixture.ProductAsync(stock: 4);
        (
            await customer.PostAsJsonAsync(
                "/api/cart/items",
                new CartQuantityRequest { ProductId = p.Id }
            )
        ).EnsureSuccessStatusCode();
        var order = await CreateOrder(customer);
        using var admin = await fixture.AdminAsync();
        (
            await admin.PutAsJsonAsync(
                $"/api/admin/orders/{order.Id}/status",
                new OrderStatusRequest { Status = OrderStatus.Cancelled },
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            HttpStatusCode.Conflict,
            (
                await customer.PostAsJsonAsync(
                    $"/api/payments/{order.Id}",
                    new SandboxPaymentRequest(),
                    SqlServerFixture.Json
                )
            ).StatusCode
        );
        Assert.Equal((4, 0, 0), await fixture.StateAsync(p.Id, order.Id));
        Assert.Single(
            (await customer.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
        (await customer.DeleteAsync($"/api/cart/items/{p.Id}")).EnsureSuccessStatusCode();
        Assert.Empty(
            (await customer.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
    }

    [Fact]
    public async Task Payment_preserves_quantities_added_after_checkout_and_clear_removes_the_rest()
    {
        var (customer, _) = await fixture.CustomerAsync();
        var p = await fixture.ProductAsync(stock: 10);
        (
            await customer.PostAsJsonAsync(
                "/api/cart/items",
                new CartQuantityRequest { ProductId = p.Id, Quantity = 2 }
            )
        ).EnsureSuccessStatusCode();
        var order = await CreateOrder(customer);
        (
            await customer.PostAsJsonAsync(
                "/api/cart/items",
                new CartQuantityRequest { ProductId = p.Id, Quantity = 3 }
            )
        ).EnsureSuccessStatusCode();
        (
            await customer.PostAsJsonAsync(
                $"/api/payments/{order.Id}",
                new SandboxPaymentRequest(),
                SqlServerFixture.Json
            )
        ).EnsureSuccessStatusCode();
        Assert.Equal(
            3,
            Assert
                .Single(
                    (
                        await customer.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json)
                    )!.Items
                )
                .Quantity
        );
        (await customer.DeleteAsync("/api/cart")).EnsureSuccessStatusCode();
        Assert.Empty(
            (await customer.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
    }

    [Fact]
    public async Task Sole_active_admin_cannot_be_disabled_or_demoted_through_the_API()
    {
        using var admin = await fixture.AdminAsync();
        var me = (await admin.GetFromJsonAsync<UserDto>("/api/auth/me", SqlServerFixture.Json))!;
        using var scope = fixture.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var others = await db
            .Users.Where(x =>
                x.Id != me.Id
                && x.IsActive
                && db.UserRoles.Any(ur =>
                    ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin")
                )
            )
            .Select(x => x.Id)
            .ToListAsync();
        await db
            .Users.Where(x => others.Contains(x.Id))
            .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, false));
        try
        {
            foreach (
                var change in new[]
                {
                    new AccountAccessRequest { Role = "Customer" },
                    new AccountAccessRequest { Role = "Admin", IsActive = false },
                }
            )
                Assert.Equal(
                    HttpStatusCode.Conflict,
                    (
                        await admin.PutAsJsonAsync($"/api/admin/accounts/{me.Id}/access", change)
                    ).StatusCode
                );
            Assert.Equal(
                HttpStatusCode.OK,
                (await admin.GetAsync("/api/admin/dashboard")).StatusCode
            );
        }
        finally
        {
            await db
                .Users.Where(x => others.Contains(x.Id))
                .ExecuteUpdateAsync(set => set.SetProperty(x => x.IsActive, true));
        }
    }

    private static async Task<OrderDto> CreateOrder(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/orders",
            new CheckoutRequest
            {
                ReceiverName = "Người nhận",
                ReceiverPhone = "0901234567",
                ShippingAddress = "123 Nguyễn Huệ, Thành phố Hồ Chí Minh",
            },
            SqlServerFixture.Json
        );
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OrderDto>(SqlServerFixture.Json))!;
    }
}

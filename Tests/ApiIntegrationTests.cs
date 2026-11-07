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
public sealed class ApiIntegrationTests(SqlServerFixture fixture)
{
    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<T>(SqlServerFixture.Json))!;
    }

    private static CheckoutRequest Checkout() =>
        new()
        {
            CheckoutKey = Guid.NewGuid(),
            ReceiverName = "Người nhận kiểm thử",
            ReceiverPhone = "0901234567",
            ShippingAddress = "123 Đường Kiểm Thử, Thành phố Hồ Chí Minh",
        };

    private static async Task AddAsync(HttpClient client, int id, int quantity)
    {
        var response = await client.PostAsJsonAsync(
            "/api/cart/items",
            new CartQuantityRequest { ProductId = id, Quantity = quantity },
            SqlServerFixture.Json
        );
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private static async Task<OrderDto> OrderAsync(HttpClient client, CheckoutRequest request) =>
        await ReadAsync<OrderDto>(
            await client.PostAsJsonAsync("/api/orders", request, SqlServerFixture.Json)
        );

    private static async Task<PaymentDto> PayAsync(
        HttpClient client,
        int id,
        bool success,
        Guid? key = null
    ) =>
        await ReadAsync<PaymentDto>(
            await client.PostAsJsonAsync(
                $"/api/payments/{id}",
                new SandboxPaymentRequest { Success = success, AttemptKey = key ?? Guid.NewGuid() },
                SqlServerFixture.Json
            )
        );

    [SqlServerFact]
    public async Task Registration_assigns_customer_and_current_user_comes_from_validated_JWT()
    {
        var (client, user) = await fixture.CustomerAsync();
        Assert.Contains("Customer", user.Roles);
        Assert.DoesNotContain("Admin", user.Roles);
        var current = await client.GetFromJsonAsync<UserDto>("/api/auth/me", SqlServerFixture.Json);
        Assert.Equal(user.Id, current!.Id);
        using var anonymous = fixture.Factory.CreateClient();
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync("/api/cart")).StatusCode
        );
    }

    [SqlServerFact]
    public async Task Cart_add_update_total_oversell_and_user_isolation_use_real_SQL()
    {
        var (client, _) = await fixture.CustomerAsync();
        var (other, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(stock: 5, price: 100.25m);
        await AddAsync(client, product.Id, 2);
        await AddAsync(client, product.Id, 1);
        var cart = await client.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json);
        Assert.Equal(3, Assert.Single(cart!.Items).Quantity);
        Assert.Equal(300.75m, cart.TotalAmount);
        var tooMany = await client.PostAsJsonAsync(
            "/api/cart/items",
            new CartQuantityRequest { ProductId = product.Id, Quantity = 3 },
            SqlServerFixture.Json
        );
        Assert.Equal(HttpStatusCode.Conflict, tooMany.StatusCode);
        var otherCart = await other.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json);
        Assert.Empty(otherCart!.Items);
        var update = await client.PutAsJsonAsync(
            $"/api/cart/items/{product.Id}",
            new CartQuantityRequest { ProductId = product.Id, Quantity = 5 },
            SqlServerFixture.Json
        );
        Assert.Equal(HttpStatusCode.NoContent, update.StatusCode);
        Assert.Equal(
            501.25m,
            (
                await client.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json)
            )!.TotalAmount
        );
    }

    [SqlServerFact]
    public async Task Checkout_failure_retry_success_and_replayed_requests_do_not_duplicate_data()
    {
        var (client, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(stock: 10, price: 500.50m);
        await AddAsync(client, product.Id, 2);
        var request = Checkout();
        var order = await OrderAsync(client, request);
        Assert.Equal(1001m, order.TotalAmount);
        Assert.Equal(1001m, Assert.Single(order.Items).LineTotal);
        var repeatedOrder = await OrderAsync(client, request);
        Assert.Equal(order.Id, repeatedOrder.Id);
        var failedKey = Guid.NewGuid();
        var failed = await PayAsync(client, order.Id, false, failedKey);
        Assert.Equal(PaymentStatus.Failed, failed.Status);
        var repeatedFail = await PayAsync(client, order.Id, true, failedKey);
        Assert.Equal(failed.Id, repeatedFail.Id);
        Assert.Equal(PaymentStatus.Failed, repeatedFail.Status);
        Assert.Equal((10, 1, 0), await fixture.StateAsync(product.Id, order.Id));
        Assert.Single(
            (await client.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
        var successKey = Guid.NewGuid();
        var paid = await PayAsync(client, order.Id, true, successKey);
        Assert.Equal(PaymentStatus.Success, paid.Status);
        Assert.NotNull(paid.PaidAt);
        Assert.Equal((8, 2, 1), await fixture.StateAsync(product.Id, order.Id));
        Assert.Empty(
            (await client.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
        Assert.Equal(paid.Id, (await PayAsync(client, order.Id, true, successKey)).Id);
        Assert.Equal(paid.Id, (await PayAsync(client, order.Id, true)).Id);
        Assert.Equal((8, 2, 1), await fixture.StateAsync(product.Id, order.Id));
        Assert.Equal(order.Id, (await OrderAsync(client, request)).Id);
        Assert.Equal(
            PaymentStatus.Success,
            (
                await client.GetFromJsonAsync<OrderDto>(
                    $"/api/orders/{order.Id}",
                    SqlServerFixture.Json
                )
            )!.PaymentStatus
        );
    }

    [SqlServerFact]
    public async Task Orders_payments_and_admin_endpoints_enforce_ownership_and_role()
    {
        var (owner, _) = await fixture.CustomerAsync();
        var (other, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync();
        await AddAsync(owner, product.Id, 1);
        var order = await OrderAsync(owner, Checkout());
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/orders/{order.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await other.GetAsync($"/api/payments/{order.Id}")).StatusCode
        );
        Assert.Equal(
            HttpStatusCode.NotFound,
            (
                await other.PostAsJsonAsync(
                    $"/api/payments/{order.Id}",
                    new SandboxPaymentRequest(),
                    SqlServerFixture.Json
                )
            ).StatusCode
        );
        Assert.Empty(
            (
                await other.GetFromJsonAsync<PagedResult<OrderDto>>(
                    "/api/orders",
                    SqlServerFixture.Json
                )
            )!.Items
        );
        foreach (
            var path in new[]
            {
                "dashboard",
                "products",
                "orders",
                "customers",
                "accounts",
                "categories",
            }
        )
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await owner.GetAsync("/api/admin/" + path)).StatusCode
            );
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (
                await owner.PutAsJsonAsync(
                    $"/api/admin/products/{product.Id}",
                    new ProductRequest(),
                    SqlServerFixture.Json
                )
            ).StatusCode
        );
    }

    [SqlServerFact]
    public async Task Stock_lost_between_checkout_and_payment_records_failure_without_clearing_cart()
    {
        var (client, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(stock: 1);
        await AddAsync(client, product.Id, 1);
        var order = await OrderAsync(client, Checkout());
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db
                .Products.Where(x => x.Id == product.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.StockQuantity, 0));
        }
        var failed = await PayAsync(client, order.Id, true);
        Assert.Equal(PaymentStatus.Failed, failed.Status);
        Assert.NotNull(failed.FailureReason);
        Assert.Equal((0, 1, 0), await fixture.StateAsync(product.Id, order.Id));
        Assert.Single(
            (await client.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json))!.Items
        );
    }

    [SqlServerFact]
    public async Task Admin_price_changes_are_visible_but_paid_order_snapshot_is_immutable()
    {
        var (client, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(price: 100000);
        await AddAsync(client, product.Id, 2);
        var order = await OrderAsync(client, Checkout());
        await PayAsync(client, order.Id, true);
        using var admin = await fixture.AdminAsync();
        var current = await client.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Slug}",
            SqlServerFixture.Json
        );
        var update = new ProductRequest
        {
            CategoryId = current!.CategoryId,
            Name = current.Name + " phiên bản mới",
            Slug = current.Slug,
            SKU = current.SKU,
            Description = current.Description,
            Price = 200000,
            StockQuantity = current.StockQuantity,
            ImageUrl = current.ImageUrl,
            IsActive = true,
            RowVersion = current.RowVersion,
        };
        await ReadAsync<ProductDto>(
            await admin.PutAsJsonAsync(
                $"/api/admin/products/{product.Id}",
                update,
                SqlServerFixture.Json
            )
        );
        var changed = await client.GetFromJsonAsync<ProductDto>(
            $"/api/products/{product.Slug}",
            SqlServerFixture.Json
        );
        Assert.Equal(200000, changed!.Price);
        var history = await client.GetFromJsonAsync<OrderDto>(
            $"/api/orders/{order.Id}",
            SqlServerFixture.Json
        );
        Assert.Equal(100000, Assert.Single(history!.Items).UnitPrice);
        Assert.Equal(product.Name, Assert.Single(history.Items).ProductName);
        Assert.Equal(200000, history.TotalAmount);
        var stale = await admin.PutAsJsonAsync(
            $"/api/admin/products/{product.Id}",
            update,
            SqlServerFixture.Json
        );
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var illegal = await admin.PutAsJsonAsync(
            $"/api/admin/orders/{order.Id}/status",
            new OrderStatusRequest { Status = OrderStatus.Cancelled },
            SqlServerFixture.Json
        );
        Assert.Equal(HttpStatusCode.Conflict, illegal.StatusCode);
    }

    [SqlServerFact]
    public async Task Concurrent_payment_requests_never_deduct_stock_twice()
    {
        var (client, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(stock: 5);
        await AddAsync(client, product.Id, 2);
        var order = await OrderAsync(client, Checkout());
        var requests = Enumerable
            .Range(0, 2)
            .Select(_ =>
                client.PostAsJsonAsync(
                    $"/api/payments/{order.Id}",
                    new SandboxPaymentRequest { Success = true, AttemptKey = Guid.NewGuid() },
                    SqlServerFixture.Json
                )
            );
        var responses = await Task.WhenAll(requests);
        Assert.Contains(responses, x => x.IsSuccessStatusCode);
        Assert.All(
            responses,
            x => Assert.True(x.IsSuccessStatusCode || x.StatusCode == HttpStatusCode.Conflict)
        );
        // Nếu SQL chọn deadlock victim, client được thử lại an toàn bằng mã mới.
        await PayAsync(client, order.Id, true);
        Assert.Equal((3, 1, 1), await fixture.StateAsync(product.Id, order.Id));
    }

    [SqlServerFact]
    public async Task Two_orders_competing_for_last_unit_allow_only_one_successful_purchase()
    {
        var (first, _) = await fixture.CustomerAsync();
        var (second, _) = await fixture.CustomerAsync();
        var product = await fixture.ProductAsync(stock: 1);
        await AddAsync(first, product.Id, 1);
        await AddAsync(second, product.Id, 1);
        var orderA = await OrderAsync(first, Checkout());
        var orderB = await OrderAsync(second, Checkout());
        var keyA = Guid.NewGuid();
        var keyB = Guid.NewGuid();
        var responses = await Task.WhenAll(
            first.PostAsJsonAsync(
                $"/api/payments/{orderA.Id}",
                new SandboxPaymentRequest { Success = true, AttemptKey = keyA },
                SqlServerFixture.Json
            ),
            second.PostAsJsonAsync(
                $"/api/payments/{orderB.Id}",
                new SandboxPaymentRequest { Success = true, AttemptKey = keyB },
                SqlServerFixture.Json
            )
        );
        Assert.All(
            responses,
            x => Assert.True(x.IsSuccessStatusCode || x.StatusCode == HttpStatusCode.Conflict)
        );
        var paidA = responses[0].IsSuccessStatusCode
            ? await ReadAsync<PaymentDto>(responses[0])
            : await PayAsync(first, orderA.Id, true, keyA);
        var paidB = responses[1].IsSuccessStatusCode
            ? await ReadAsync<PaymentDto>(responses[1])
            : await PayAsync(second, orderB.Id, true, keyB);
        Assert.Single(new[] { paidA, paidB }, x => x.Status == PaymentStatus.Success);
        Assert.Single(new[] { paidA, paidB }, x => x.Status == PaymentStatus.Failed);
        Assert.Equal(0, (await fixture.StateAsync(product.Id, orderA.Id)).Stock);
        var cartA = await first.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json);
        var cartB = await second.GetFromJsonAsync<CartDto>("/api/cart", SqlServerFixture.Json);
        Assert.Equal(paidA.Status == PaymentStatus.Success ? 0 : 1, cartA!.Items.Count);
        Assert.Equal(paidB.Status == PaymentStatus.Success ? 0 : 1, cartB!.Items.Count);
    }
}

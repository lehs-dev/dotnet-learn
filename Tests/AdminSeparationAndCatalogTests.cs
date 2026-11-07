using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using ElectroShop.Api.Data.Seed;
using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
namespace ElectroShop.Tests;

// Kiểm chứng yêu cầu "Admin tách riêng khỏi khách hàng" và "mỗi sản phẩm một ảnh demo".
// Chạy in-process qua WebApplicationFactory (không qua TCP) trên SQL Server thật.
[Collection("SQL Server")]
public sealed class AdminSeparationAndCatalogTests(SqlServerFixture fixture)
{
    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ElectroShop.Tests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<(string Email, string Password, string UserId)> RegisterCustomerAsync()
    {
        var client = fixture.Factory.CreateClient();
        var email = $"test-admin-gate-{Guid.NewGuid():N}@electroshop.test";
        var password = "Test!aA1" + Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = email, Password = password, ConfirmPassword = password,
            FullName = "Khách kiểm thử cổng admin", PhoneNumber = "0901234567"
        }, SqlServerFixture.Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(SqlServerFixture.Json))!;
        return (email, password, auth.User.Id);
    }

    private async Task DeleteUserAsync(string userId)
    {
        using var scope = fixture.Factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await users.FindByIdAsync(userId);
        if (user is not null)
            await users.DeleteAsync(user);
    }

    [SqlServerFact]
    public async Task Customer_is_rejected_at_admin_gate_and_admin_is_rejected_at_customer_gate()
    {
        var (email, password, userId) = await RegisterCustomerAsync();
        try
        {
            var anonymous = fixture.Factory.CreateClient();
            // Khách hàng dùng đúng mật khẩu nhưng sai cổng -> 403 tại cổng quản trị.
            var atAdminGate = await anonymous.PostAsJsonAsync("/api/auth/admin/login",
                new LoginRequest { Email = email, Password = password }, SqlServerFixture.Json);
            Assert.Equal(HttpStatusCode.Forbidden, atAdminGate.StatusCode);

            // Nâng tài khoản này thành Admin bằng UserManager (mô phỏng tài khoản quản trị được tạo riêng).
            using (var scope = fixture.Factory.Services.CreateScope())
            {
                var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                var user = await users.FindByIdAsync(userId);
                Assert.True((await users.RemoveFromRoleAsync(user!, "Customer")).Succeeded);
                Assert.True((await users.AddToRoleAsync(user!, "Admin")).Succeeded);
            }

            // Nay là Admin: cổng khách hàng từ chối, cổng quản trị chấp nhận và chỉ trả vai trò Admin.
            var atCustomerGate = await anonymous.PostAsJsonAsync("/api/auth/login",
                new LoginRequest { Email = email, Password = password }, SqlServerFixture.Json);
            Assert.Equal(HttpStatusCode.Forbidden, atCustomerGate.StatusCode);

            var atAdminGateOk = await anonymous.PostAsJsonAsync("/api/auth/admin/login",
                new LoginRequest { Email = email, Password = password }, SqlServerFixture.Json);
            atAdminGateOk.EnsureSuccessStatusCode();
            var auth = (await atAdminGateOk.Content.ReadFromJsonAsync<AuthResponse>(SqlServerFixture.Json))!;
            Assert.Equal(["Admin"], auth.User.Roles);
        }
        finally { await DeleteUserAsync(userId); }
    }

    [SqlServerFact]
    public async Task Registered_customer_can_never_call_admin_api_but_admin_can()
    {
        var (customerClient, _) = await fixture.CustomerAsync();
        var forbidden = await customerClient.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var adminClient = await fixture.AdminAsync();
        var ok = await adminClient.GetAsync("/api/admin/dashboard");
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
    }

    [SqlServerFact]
    public async Task Seeded_catalog_has_22_products_each_with_its_own_existing_demo_image()
    {
        // Seed idempotent ngay trong test để độc lập với trạng thái DB trước đó.
        using (var scope = fixture.Factory.Services.CreateScope())
        {
            var config = new ConfigurationBuilder().Build();
            await SeedData.InitializeAsync(scope.ServiceProvider, new TestHostEnvironment(), config);
        }
        var client = fixture.Factory.CreateClient();
        var page = (await client.GetFromJsonAsync<PagedResult<ProductDto>>("/api/products?pageSize=100", SqlServerFixture.Json))!;
        var seeded = page.Items.Where(x => x.SKU.StartsWith("ES-", StringComparison.Ordinal)).ToList();
        Assert.Equal(22, seeded.Count);
        Assert.Equal(22, seeded.Select(x => x.ImageUrl).Distinct().Count());
        Assert.All(seeded, p => Assert.StartsWith("/_content/ElectroShop.UiKit/images/products/", p.ImageUrl));

        // Mọi đường ảnh trong DB đều trỏ tới file SVG thật trong UiKit.
        var root = FindSolutionRoot();
        foreach (var product in seeded)
        {
            var relative = product.ImageUrl.Replace("/_content/ElectroShop.UiKit/", "UiKit/wwwroot/");
            var file = Path.Combine(root, relative);
            Assert.True(File.Exists(file), $"Thiếu file ảnh cho {product.SKU}: {relative}");
            Assert.Contains("<svg", await File.ReadAllTextAsync(file));
        }
        var hero = Path.Combine(root, "UiKit/wwwroot/images/hero-banner.svg");
        Assert.True(File.Exists(hero), "Thiếu ảnh hero-banner.svg dùng ở trang chủ cả ba frontend.");
    }

    private static string FindSolutionRoot()
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(AdminSeparationAndCatalogTests).Assembly.Location)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ElectroShop.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Không tìm thấy ElectroShop.sln từ thư mục chạy test.");
    }
}

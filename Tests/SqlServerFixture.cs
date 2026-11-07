using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ElectroShop.Api.Authentication;
using ElectroShop.Api.Data;
using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ElectroShop.Tests;

[CollectionDefinition("SQL Server", DisableParallelization = true)]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>;
public sealed class SqlServerFixture : IAsyncLifetime
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };
    private WebApplicationFactory<Program>? factory;
    private readonly List<string> userIds = [];
    private readonly List<int> productIds = [];
    private readonly List<int> categoryIds = [];
    public WebApplicationFactory<Program> Factory => factory ?? throw new InvalidOperationException("Thiếu cấu hình cơ sở dữ liệu.");
    public async Task InitializeAsync()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=ElectroShop_Test.db");
        // Key ngẫu nhiên chỉ dùng trong process test khi chưa có key development.
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("Jwt__Key")))
            Environment.SetEnvironmentVariable("Jwt__Key", Convert.ToHexString(RandomNumberGenerator.GetBytes(48)));
        factory = new TestApiFactory();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in new[] { "Admin", "Customer" })
        if (!await roles.RoleExistsAsync(role))
            Assert.True((await roles.CreateAsync(new IdentityRole(role))).Succeeded);
    }
    public async Task<(HttpClient Client, UserDto User)> CustomerAsync()
    {
        var client = Factory.CreateClient();
        var email = $"test-{Guid.NewGuid():N}@electroshop.test";
        var password = "Test!aA1" + Guid.NewGuid().ToString("N");
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest { Email = email, Password = password, ConfirmPassword = password, FullName = "Khách kiểm thử", PhoneNumber = "0901234567" }, Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(Json))!;
        userIds.Add(auth.User.Id);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return (client, auth.User);
    }
    public async Task<HttpClient> AdminAsync()
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await db.Users.FirstOrDefaultAsync(x => x.IsActive && db.UserRoles.Any(ur => ur.UserId == x.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Admin")));
        if (admin is null)
        {
            var (_, user) = await CustomerAsync();
            admin = await users.FindByIdAsync(user.Id);
            Assert.True((await users.RemoveFromRoleAsync(admin!, "Customer")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin!, "Admin")).Succeeded);
        }
        var auth = await scope.ServiceProvider.GetRequiredService<TokenService>().CreateAsync(admin!);
        var client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        return client;
    }
    public async Task<ProductDto> ProductAsync(int stock = 10, decimal price = 125000.25m)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var suffix = Guid.NewGuid().ToString("N");
        var category = new Category { Name = "Danh mục kiểm thử", Slug = "test-category-" + suffix, Description = "Dữ liệu test tự dọn" };
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        categoryIds.Add(category.Id);
        var product = new Product { Name = "Thiết bị kiểm thử " + suffix, Slug = "test-product-" + suffix, SKU = "T-" + suffix, CategoryId = category.Id, Price = price, StockQuantity = stock, Description = "Thiết bị dùng riêng cho integration test.", ImageUrl = "/_content/ElectroShop.UiKit/images/laptop.svg" };
        db.Products.Add(product);
        await db.SaveChangesAsync();
        productIds.Add(product.Id);
        return new(product.Id, category.Id, category.Name, product.Name, product.Slug, product.SKU, product.Description, product.Price, stock, product.ImageUrl, true, Convert.ToBase64String(product.RowVersion));
    }
    public async Task<(int Stock, int Payments, int Successes)> StateAsync(int productId, int orderId)
    {
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return (await db.Products.Where(x => x.Id == productId).Select(x => x.StockQuantity).SingleAsync(), await db.PaymentTransactions.CountAsync(x => x.OrderId == orderId), await db.PaymentTransactions.CountAsync(x => x.OrderId == orderId && x.Status == ElectroShop.Contracts.Enums.PaymentStatus.Success));
    }
    public async Task DisposeAsync()
    {
        if (factory is null)
            return;
        try
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await using var tx = await db.Database.BeginTransactionAsync();
            await db.PaymentTransactions.Where(x => userIds.Contains(x.Order.UserId)).ExecuteDeleteAsync();
            await db.OrderItems.Where(x => userIds.Contains(x.Order.UserId)).ExecuteDeleteAsync();
            await db.Orders.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.CartItems.Where(x => userIds.Contains(x.Cart.UserId)).ExecuteDeleteAsync();
            await db.Carts.Where(x => userIds.Contains(x.UserId)).ExecuteDeleteAsync();
            await db.Users.Where(x => userIds.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Products.Where(x => productIds.Contains(x.Id)).ExecuteDeleteAsync();
            await db.Categories.Where(x => categoryIds.Contains(x.Id)).ExecuteDeleteAsync();
            await tx.CommitAsync();
        }
        finally { await factory.DisposeAsync(); }
    }
    private sealed class TestApiFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            // Content root trỏ đúng thư mục project API (tên thư mục là "API", khác tên assembly
            // ElectroShop.Api) để WebApplicationFactory tìm thấy appsettings dù chạy bằng
            // dotnet test hay xUnit console runner.
            var dir = new DirectoryInfo(Path.GetDirectoryName(typeof(SqlServerFixture).Assembly.Location)!);
            while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ElectroShop.sln")))
                dir = dir.Parent;
            if (dir is not null)
                builder.UseContentRoot(Path.Combine(dir.FullName, "API"));
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?> { { "Database:Initialize", "false" } }));
        }
    }
}

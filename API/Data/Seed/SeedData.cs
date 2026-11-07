using ElectroShop.Api.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
namespace ElectroShop.Api.Data.Seed;

public static class SeedData
{
    public static async Task InitializeAsync(IServiceProvider services, IHostEnvironment environment, IConfiguration config)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var roles = services.GetRequiredService<RoleManager<IdentityRole>>();
        var users = services.GetRequiredService<UserManager<ApplicationUser>>();
        foreach (var name in new[] { "Admin", "Customer" })
        if (!await roles.RoleExistsAsync(name))
            Check(await roles.CreateAsync(new IdentityRole(name)));
        string[] names = ["Laptop", "Điện thoại", "Tai nghe", "Bàn phím", "Chuột", "Màn hình", "Phụ kiện"];
        string[] slugs = ["laptop", "dien-thoai", "tai-nghe", "ban-phim", "chuot", "man-hinh", "phu-kien"];
        for (int i = 0; i < names.Length; i++)
        if (!await db.Categories.AnyAsync(x => x.Slug == slugs[i]))
            db.Categories.Add(new Category { Name = names[i], Slug = slugs[i], Description = $"Khám phá {names[i].ToLowerInvariant()} dành cho học tập và làm việc." });
        await db.SaveChangesAsync();
        (string Name, string Slug, decimal Price, int Category, string Image)[] products = [
         ("Laptop ASUS Vivobook 15","asus-vivobook-15",15990000,0,"laptop"),("Laptop Lenovo IdeaPad Slim 5","lenovo-ideapad-slim-5",18490000,0,"laptop"),("Laptop Dell Inspiron 14","dell-inspiron-14",21490000,0,"laptop"),("Laptop HP Pavilion 15","hp-pavilion-15",17490000,0,"laptop"),
   ("Điện thoại Samsung Galaxy A56","samsung-galaxy-a56",9990000,1,"phone"),("Điện thoại iPhone 16 128GB","iphone-16-128gb",19990000,1,"phone"),("Điện thoại Xiaomi Redmi Note 14","xiaomi-redmi-note-14",5490000,1,"phone"),
   ("Tai nghe Sony WH-CH720N","sony-wh-ch720n",2990000,2,"headphones"),("Tai nghe JBL Tune 520BT","jbl-tune-520bt",1190000,2,"headphones"),("Tai nghe Samsung Galaxy Buds FE","samsung-galaxy-buds-fe",1690000,2,"headphones"),
   ("Bàn phím cơ Keychron K2","keychron-k2",1890000,3,"keyboard"),("Bàn phím Logitech K380","logitech-k380",749000,3,"keyboard"),("Bàn phím Akko 3068B","akko-3068b",1590000,3,"keyboard"),
   ("Chuột Logitech MX Master 3S","logitech-mx-master-3s",2290000,4,"mouse"),("Chuột không dây Rapoo M300","rapoo-m300",349000,4,"mouse"),("Chuột gaming Razer DeathAdder","razer-deathadder",790000,4,"mouse"),
   ("Màn hình LG UltraWide 29 inch","lg-ultrawide-29",4990000,5,"monitor"),("Màn hình Dell 24 inch IPS","dell-24-ips",3290000,5,"monitor"),("Màn hình ASUS ProArt 27 inch","asus-proart-27",7490000,5,"monitor"),
   ("Sạc nhanh Anker USB-C 65W","anker-usbc-65w",899000,6,"accessory"),("Ổ cứng SSD Samsung T7 1TB","samsung-t7-1tb",2290000,6,"accessory"),("Hub USB-C Ugreen 6 trong 1","ugreen-hub-6-in-1",690000,6,"accessory")
        ];
        for (int i = 0; i < products.Length; i++)
        {
            var p = products[i];
            var sku = $"ES-{i + 1:0000}";
            if (await db.Products.AnyAsync(x => x.SKU == sku))
                continue;
            var category = await db.Categories.SingleAsync(x => x.Slug == slugs[p.Category]);
            db.Products.Add(new Product { Name = p.Name, Slug = p.Slug, SKU = sku, Price = p.Price, CategoryId = category.Id, StockQuantity = i == 15 ? 0 : 12 + i, ImageUrl = $"/_content/ElectroShop.UiKit/images/{p.Image}.svg", Description = $"{p.Name} phù hợp cho học tập, làm việc và giải trí hằng ngày. Thiết kế gọn gàng, dễ sử dụng. Sản phẩm minh họa phục vụ học .NET 10; giá chỉ là dữ liệu mẫu." });
        }
        await db.SaveChangesAsync();
        if (environment.IsDevelopment())
        {
            var email = config["Seed:AdminEmail"];
            var password = config["Seed:AdminPassword"];
            if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
            {
                var admin = await users.FindByEmailAsync(email);
                if (admin is null)
                {
                    admin = new ApplicationUser { UserName = email, Email = email, FullName = "Quản trị ElectroShop", EmailConfirmed = true };
                    Check(await users.CreateAsync(admin, password));
                }
                if (!await users.IsInRoleAsync(admin, "Admin"))
                    Check(await users.AddToRoleAsync(admin, "Admin"));
            }
        }
    }
    private static void Check(IdentityResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException("Seed Identity thất bại: " + string.Join("; ", result.Errors.Select(x => x.Description)));
    }
}

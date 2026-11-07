// DATABASE FIRST - Bước 3: dùng DbContext được scaffold sinh ra để truy vấn database có sẵn.
// Chạy:  dotnet run --project DatabaseFirst
// (đọc chuỗi kết nối từ biến môi trường ConnectionStrings__DbFirst; xem cách scaffold ở đầu file
//  ElectroShopDbFirstContext.cs trong Models/ — không chứa mật khẩu trong source.)
using ElectroShop.DatabaseFirst.Models;
using Microsoft.EntityFrameworkCore;

var connection =
    Environment.GetEnvironmentVariable("ConnectionStrings__DbFirst")
    ?? throw new InvalidOperationException(
        "Hãy đặt biến môi trường ConnectionStrings__DbFirst trỏ tới ElectroShopDbFirst."
    );

var options = new DbContextOptionsBuilder<ElectroShopDbFirstContext>()
    .UseSqlServer(connection)
    .Options;
await using var db = new ElectroShopDbFirstContext(options);

Console.WriteLine("== Database First: dữ liệu đọc bằng entity scaffold từ database có sẵn ==");
foreach (
    var category in await db.Categories.Include(c => c.Products).OrderBy(c => c.Id).ToListAsync()
)
{
    Console.WriteLine($"Danh mục {category.Id}: {category.Name} ({category.Slug})");
    foreach (var product in category.Products)
        Console.WriteLine(
            $"  - {product.Name} | {product.Price:N0} VND | tồn {product.StockQuantity} | {product.ImageUrl}"
        );
}
Console.WriteLine(
    $"Tổng: {await db.Categories.CountAsync()} danh mục, {await db.Products.CountAsync()} sản phẩm."
);

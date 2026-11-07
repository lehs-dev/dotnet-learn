using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace ElectroShop.Api.Data;
// EF CLI tạo migration mà không cần khởi chạy API/JWT hay kết nối tới SQL Server.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var config = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json", optional: true).AddEnvironmentVariables().Build();
        var connection = config.GetConnectionString("DefaultConnection") ?? "Server=db;Database=ElectroShopDb;Integrated Security=true;TrustServerCertificate=true";
        if (string.IsNullOrWhiteSpace(connection))
            connection = "Server=db;Database=ElectroShopDb;Integrated Security=true;TrustServerCertificate=true";
        return new(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connection, o => o.UseCompatibilityLevel(150)).Options);
    }
}

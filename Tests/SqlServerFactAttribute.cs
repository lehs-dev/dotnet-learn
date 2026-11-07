namespace ElectroShop.Tests;
// Thiếu cấu hình thì báo Skip rõ ràng; tuyệt đối không thay SQL Server bằng provider khác.
public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")))
            Skip = "Cần ConnectionStrings__DefaultConnection trỏ đến ElectroShopDb trên SQL Server để chạy integration tests.";
    }
}

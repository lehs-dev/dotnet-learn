namespace ElectroShop.Tests;

public sealed class SqlServerFactAttribute : FactAttribute
{
    public SqlServerFactAttribute()
    {
        var conn = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        if (string.IsNullOrWhiteSpace(conn))
        {
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "Data Source=ElectroShop_Test.db");
        }
    }
}

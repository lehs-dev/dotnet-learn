using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using ElectroShop.Api.Authentication;
using ElectroShop.Api.Data;
using ElectroShop.Api.Data.Seed;
using ElectroShop.Api.Entities;
using ElectroShop.Api.Interfaces;
using ElectroShop.Api.Extensions;
using ElectroShop.Api.Middleware;
using ElectroShop.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);
var connection = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<AppDbContext>(o => o.UseSqlServer(string.IsNullOrWhiteSpace(connection) ? "Server=db;Database=ElectroShopDb;Integrated Security=true;TrustServerCertificate=true" : connection, sql => sql.UseCompatibilityLevel(150)));
builder.Services.AddIdentityCore<ApplicationUser>(o => { o.User.RequireUniqueEmail = true; o.Password.RequiredLength = 8; o.Lockout.MaxFailedAccessAttempts = 5; o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10); })
 .AddRoles<IdentityRole>().AddEntityFrameworkStores<AppDbContext>();
var key = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32) throw new InvalidOperationException("Cấu hình Jwt__Key phải có ít nhất 32 byte; xem .env.example.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
{
    o.TokenValidationParameters = new()
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
        ClockSkew = TimeSpan.FromSeconds(15)
    };
    o.Events = new JwtBearerEvents
    {
        OnTokenValidated = async c =>
        {
            var users = c.HttpContext.RequestServices.GetRequiredService<UserManager<ApplicationUser>>();
            var id = c.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            var user = id is null ? null : await users.FindByIdAsync(id);
            if (user is null || !user.IsActive || await users.IsLockedOutAsync(user) || user.SecurityStamp != c.Principal?.FindFirstValue("stamp"))
                c.Fail("Phiên đăng nhập hết hiệu lực.");
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddShopServices();
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.InvalidModelStateResponseFactory = context =>
        new BadRequestObjectResult(new ValidationProblemDetails(context.ModelState)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Dữ liệu không hợp lệ",
            Detail = "Vui lòng kiểm tra các trường thông tin.",
            Extensions = { ["traceId"] = context.HttpContext.TraceIdentifier }
        }))
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
var app = builder.Build();
app.UseMiddleware<ExceptionMiddleware>();
app.UseStatusCodePages(async c =>
{
    await Results.Problem(statusCode: c.HttpContext.Response.StatusCode, title: "Không thể thực hiện yêu cầu", detail: c.HttpContext.Response.StatusCode == 403 ? "Bạn không có quyền thực hiện thao tác này." : "Yêu cầu không hợp lệ hoặc cần đăng nhập.").ExecuteAsync(c.HttpContext);
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
app.MapGet("/health", async (AppDbContext db, CancellationToken ct) => await db.Database.CanConnectAsync(ct) ? Results.Ok(new { status = "Healthy", database = "ElectroShopDb" }) : Results.Problem(statusCode: 503, detail: "SQL Server chưa sẵn sàng."));
if (builder.Configuration.GetValue<bool>("Database:Initialize") || args.Contains("--seed-only"))
{
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.InitializeAsync(scope.ServiceProvider, app.Environment, builder.Configuration);
}
if (!args.Contains("--seed-only")) app.Run();
public partial class Program
{
}

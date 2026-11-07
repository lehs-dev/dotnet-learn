using ElectroShop.Mvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder
    .Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        // DTO đã có Required rõ ràng; RowVersion khi thêm mới và mô tả danh mục được phép trống.
        options.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true;
    })
    .AddCookieTempDataProvider(options =>
    {
        options.Cookie.Name = "ElectroShop.Mvc.TempData";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    });
builder.Services.AddAntiforgery(options =>
{
    options.Cookie.Name = "ElectroShop.Mvc.Antiforgery";
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddHttpClient<ShopApiClient>(http =>
{
    http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100");
    http.Timeout = TimeSpan.FromSeconds(25);
});
builder
    .Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "ElectroShop.Mvc.Auth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.LoginPath = "/";
        options.AccessDeniedPath = "/";
        options.SlidingExpiration = false;
        // Khu vực /admin có cổng đăng nhập quản trị riêng; phần còn lại dùng popup khách hàng ở trang chủ.
        options.Events.OnRedirectToLogin = context =>
        {
            var returnUrl = HttpMethods.IsGet(context.Request.Method)
                ? context.Request.Path + context.Request.QueryString
                : "/cart";
            context.Response.Redirect(
                context.Request.Path.StartsWithSegments("/admin")
                    ? "/admin/login"
                    : "/?login=1&ReturnUrl=" + Uri.EscapeDataString(returnUrl)
            );
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.Redirect(
                context.HttpContext.Request.Path.StartsWithSegments("/admin")
                    ? "/admin/login?denied=1"
                    : "/?denied=1"
            );
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var store = context.HttpContext.RequestServices.GetRequiredService<TokenStore>();
            if (store.Get(context.Principal?.FindFirst("session_id")?.Value) is null)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync();
            }
        };
    });
builder.Services.AddAuthorization();
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.Run();

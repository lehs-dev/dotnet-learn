using ElectroShop.Blazor.Components;
using ElectroShop.Blazor.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();
builder.Services.AddCascadingAuthenticationState();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<SessionTokenStore>();
builder.Services.AddScoped<ShopApiClient>();
builder.Services.AddHttpClient("ShopApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100/");
    client.Timeout = TimeSpan.FromSeconds(30);
});
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "ElectroShop.Blazor.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.LoginPath = "/";
    options.AccessDeniedPath = "/";
    options.SlidingExpiration = false;
    // Khu vực /admin có cổng đăng nhập quản trị riêng; phần còn lại dùng popup khách hàng ở trang chủ.
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/admin"))
        {
            context.Response.Redirect("/admin/login");
            return Task.CompletedTask;
        }
        var returnUrl = context.Request.Path + context.Request.QueryString;
        context.Response.Redirect("/?login=1&ReturnUrl=" + Uri.EscapeDataString(returnUrl));
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        context.Response.Redirect(context.Request.Path.StartsWithSegments("/admin") ? "/admin/login?denied=1" : "/?accessDenied=1");
        return Task.CompletedTask;
    };
    options.Events.OnValidatePrincipal = async context =>
    {
        var store = context.HttpContext.RequestServices.GetRequiredService<SessionTokenStore>();
        if (store.Get(context.Principal?.FindFirst("session_id")?.Value) is null)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    };
});
builder.Services.AddAntiforgery(options => options.Cookie.Name = "ElectroShop.Blazor.Antiforgery");
var app = builder.Build();
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.MapStaticAssets();
app.MapShopAuthentication();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();

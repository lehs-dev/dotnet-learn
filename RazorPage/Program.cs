using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddRazorPages().AddMvcOptions(o => o.SuppressImplicitRequiredAttributeForNonNullableReferenceTypes = true).AddCookieTempDataProvider(o => o.Cookie.Name = "ElectroShop.RazorPages.TempData");
builder.Services.AddAntiforgery(o => o.Cookie.Name = "ElectroShop.RazorPages.Antiforgery");
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<TokenStore>();
builder.Services.AddHttpClient<ShopApiClient>(http => { http.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5100"); http.Timeout = TimeSpan.FromSeconds(25); });
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie(options =>
{
    options.Cookie.Name = "ElectroShop.RazorPages";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromHours(1);
    options.SlidingExpiration = false;
    options.Events.OnRedirectToLogin = ctx => { ctx.Response.Redirect("/?login=1"); return Task.CompletedTask; };
    options.Events.OnRedirectToAccessDenied = ctx => { ctx.Response.Redirect("/?denied=1"); return Task.CompletedTask; };
    options.Events.OnValidatePrincipal = async ctx => { if (ctx.HttpContext.RequestServices.GetRequiredService<TokenStore>().Get(ctx.Principal?.FindFirst("session")?.Value) is null) { ctx.RejectPrincipal(); await Microsoft.AspNetCore.Authentication.AuthenticationHttpContextExtensions.SignOutAsync(ctx.HttpContext); } };
});
builder.Services.AddAuthorization();
var app = builder.Build();
if (!app.Environment.IsDevelopment()) { app.UseExceptionHandler("/Error"); app.UseHsts(); }
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();

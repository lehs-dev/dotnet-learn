using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ElectroShop.Blazor.Services;

public static class AuthEndpoints
{
    public static void MapShopAuthentication(this WebApplication app)
    {
        app.MapPost("/auth/login", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, SessionTokenStore store) => Authenticate(context, antiforgery, clients, store, false));
        app.MapPost("/auth/register", (HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, SessionTokenStore store) => Authenticate(context, antiforgery, clients, store, true));
        app.MapPost("/auth/logout", async (HttpContext context, IAntiforgery antiforgery, SessionTokenStore store) =>
        {
            try
            {
                await antiforgery.ValidateRequestAsync(context);
            }
            catch (AntiforgeryValidationException) { return Results.BadRequest("Phiên biểu mẫu đã hết hạn. Vui lòng tải lại trang."); }
            store.Remove(context.User.FindFirstValue("session_id"));
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.LocalRedirect("/");
        });
    }

    private static async Task<IResult> Authenticate(HttpContext context, IAntiforgery antiforgery, IHttpClientFactory clients, SessionTokenStore store, bool register)
    {
        try
        {
            await antiforgery.ValidateRequestAsync(context);
        }
        catch (AntiforgeryValidationException) { return Results.BadRequest("Phiên biểu mẫu đã hết hạn. Vui lòng tải lại trang."); }
        var form = await context.Request.ReadFormAsync(context.RequestAborted);
        LoginRequest model = register ? new RegisterRequest
        {
            Email = form["Email"].ToString(),
            Password = form["Password"].ToString(),
            FullName = form["FullName"].ToString(),
            PhoneNumber = form["PhoneNumber"].ToString(),
            ConfirmPassword = form["ConfirmPassword"].ToString()
        } : new LoginRequest { Email = form["Email"].ToString(), Password = form["Password"].ToString() };
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(model, new ValidationContext(model), validation, true))
            return Error(string.Join(" ", validation.Select(x => x.ErrorMessage)), register);
        try
        {
            using var client = clients.CreateClient("ShopApi");
            using var response = await client.PostAsync(register ? "api/auth/register" : "api/auth/login", JsonContent.Create(model, model.GetType(), options: ShopApiClient.Json), context.RequestAborted);
            if (!response.IsSuccessStatusCode)
                return Error(await ShopApiClient.ErrorAsync(response, context.RequestAborted), register);
            var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(ShopApiClient.Json, context.RequestAborted);
            if (auth is null || auth.ExpiresAt <= DateTimeOffset.UtcNow)
                return Error("API trả về phiên đăng nhập không hợp lệ.", register);
            store.Remove(context.User.FindFirstValue("session_id"));
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, auth.User.Id), new(ClaimTypes.Name, auth.User.FullName),
                new(ClaimTypes.Email, auth.User.Email), new("session_id", store.Add(auth))
            };
            claims.AddRange(auth.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { ExpiresUtc = auth.ExpiresAt, IsPersistent = false, AllowRefresh = false });
            var returnUrl = form["ReturnUrl"].ToString();
            return Results.LocalRedirect(IsLocal(returnUrl) ? returnUrl : "/");
        }
        catch (HttpRequestException) { return Error("Không thể kết nối API. Vui lòng thử lại sau.", register); }
        catch (TaskCanceledException) { return Error("API phản hồi quá lâu. Vui lòng thử lại.", register); }
    }
    private static bool IsLocal(string path) => path.StartsWith('/') && !path.StartsWith("//") && !path.StartsWith("/\\");
    private static IResult Error(string message, bool register) => Results.LocalRedirect($"/?{(register ? "register" : "login")}=1&authError={Uri.EscapeDataString(message)}");
}

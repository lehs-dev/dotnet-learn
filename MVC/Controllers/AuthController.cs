using System.Security.Claims;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using ElectroShop.Mvc.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
namespace ElectroShop.Mvc.Controllers;

[Route("auth")]
public sealed class AuthController(ShopApiClient api, TokenStore tokens) : Controller
{
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return AuthError("Vui lòng nhập email và mật khẩu hợp lệ.", "login");
        try
        {
            await SignIn(await api.PostAsync<AuthResponse>("api/auth/login", input, ct));
            return Redirect("/");
        }
        catch (ApiException ex) { return AuthError(ex.Message, "login"); }
    }
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest input, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return AuthError(string.Join(" ", ModelState.Values.SelectMany(x => x.Errors).Select(x => x.ErrorMessage)), "register");
        try
        {
            await SignIn(await api.PostAsync<AuthResponse>("api/auth/register", input, ct));
            TempData["Success"] = "Đăng ký thành công. Chào mừng bạn đến ElectroShop!";
            return Redirect("/");
        }
        catch (ApiException ex) { return AuthError(ex.Message, "register"); }
    }
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        tokens.Remove(User.FindFirst("session_id")?.Value);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Redirect("/");
    }
    private IActionResult AuthError(string message, string modal)
    {
        TempData["AuthError"] = message;
        TempData["AuthModal"] = modal;
        return Redirect("/?auth=" + modal);
    }
    private async Task SignIn(AuthResponse auth)
    {
        tokens.Remove(User.FindFirst("session_id")?.Value);
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, auth.User.Id), new(ClaimTypes.Name, auth.User.FullName), new(ClaimTypes.Email, auth.User.Email), new("session_id", tokens.Add(auth)) };
        claims.AddRange(auth.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)), new AuthenticationProperties { ExpiresUtc = auth.ExpiresAt, IsPersistent = false });
    }
}

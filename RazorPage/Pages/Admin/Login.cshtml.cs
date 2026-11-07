using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages.Admin;

// Đăng nhập quản trị RIÊNG tại /admin/login: gọi api/auth/admin/login của API.
// API chỉ cấp token khi tài khoản có vai trò Admin; tài khoản khách hàng bị từ chối (403).
// Tài khoản quản trị được tạo riêng bằng seed hoặc lệnh --create-admin của API,
// không thể tạo qua form đăng ký khách hàng (đăng ký luôn gán vai trò Customer).
public sealed class LoginModel(ShopApiClient api, TokenStore tokens) : ShopPageModel
{
    public IActionResult OnGet()
    {
        if (User.IsInRole("Admin"))
            return Redirect("/admin");
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var request = new LoginRequest
        {
            Email = form["Email"].ToString(),
            Password = form["Password"].ToString(),
        };
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
            return MessageRedirect(
                "/admin/login",
                string.Join(" ", validation.Select(x => x.ErrorMessage))
            );
        try
        {
            var result =
                await api.PostAsync<AuthResponse>("api/auth/admin/login", request, ct)
                ?? throw new ApiException("Không nhận được phiên đăng nhập quản trị.");
            if (!result.User.Roles.Contains("Admin"))
                return MessageRedirect("/admin/login", "Tài khoản này không có quyền quản trị.");
            tokens.Remove(User.FindFirstValue("session"));
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, result.User.Id),
                new(ClaimTypes.Name, result.User.FullName),
                new(ClaimTypes.Email, result.User.Email),
                new("session", tokens.Add(result)),
            };
            claims.AddRange(result.User.Roles.Select(role => new Claim(ClaimTypes.Role, role)));
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(
                    new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)
                ),
                new AuthenticationProperties { ExpiresUtc = result.ExpiresAt, IsPersistent = false }
            );
            return MessageRedirect("/admin", "Đăng nhập quản trị thành công.", true);
        }
        catch (ApiException ex)
        {
            return MessageRedirect("/admin/login", ex.Message);
        }
    }
}

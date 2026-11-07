using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using ElectroShop.RazorPages.Models;
using ElectroShop.RazorPages.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.RazorPages.Pages.Auth;

public sealed class SignInModel(ShopApiClient api, TokenStore tokens) : ShopPageModel
{
    public IActionResult OnGet() => Redirect("/");

    public async Task<IActionResult> OnPostAsync(string actionName, CancellationToken ct)
    {
        if (actionName == "logout")
        {
            tokens.Remove(User.FindFirstValue("session"));
            await HttpContext.SignOutAsync();
            return Redirect("/");
        }
        var form = await Request.ReadFormAsync(ct);
        var returnUrl = form["ReturnUrl"].ToString();
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        var failureUrl = "/?" + actionName + "=1&ReturnUrl=" + Uri.EscapeDataString(destination);
        LoginRequest request =
            actionName == "register"
                ? new RegisterRequest
                {
                    Email = form["Email"].ToString(),
                    Password = form["Password"].ToString(),
                    FullName = form["FullName"].ToString(),
                    PhoneNumber = form["PhoneNumber"].ToString(),
                    ConfirmPassword = form["ConfirmPassword"].ToString(),
                }
                : new LoginRequest
                {
                    Email = form["Email"].ToString(),
                    Password = form["Password"].ToString(),
                };
        if (actionName is not ("login" or "register"))
            return NotFound();
        var validation = new List<ValidationResult>();
        if (!Validator.TryValidateObject(request, new ValidationContext(request), validation, true))
            return AuthFailure(
                failureUrl,
                actionName,
                string.Join(" ", validation.Select(x => x.ErrorMessage))
            );
        try
        {
            var result =
                await api.PostAsync<AuthResponse>("api/auth/" + actionName, request, ct)
                ?? throw new ApiException("Không nhận được phiên đăng nhập.");
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
            return MessageRedirect(
                destination,
                actionName == "register"
                    ? "Đăng ký thành công. Chào mừng bạn đến ElectroShop!"
                    : "Đăng nhập thành công.",
                true
            );
        }
        catch (ApiException ex)
        {
            return AuthFailure(failureUrl, actionName, ex.Message);
        }
    }

    private IActionResult AuthFailure(string url, string modal, string message)
    {
        TempData["AuthError"] = message;
        TempData["AuthModal"] = modal;
        return LocalRedirect(url);
    }
}

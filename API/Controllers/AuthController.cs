using System.Security.Claims;
using ElectroShop.Api.Authentication;
using ElectroShop.Api.Entities;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace ElectroShop.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(UserManager<ApplicationUser> users, TokenService tokens)
    : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser
        {
            UserName = request.Email.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName.Trim(),
            PhoneNumber = request.PhoneNumber.Trim(),
            LockoutEnabled = true,
        };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ShopException(
                400,
                "Không thể đăng ký. Email phải duy nhất; mật khẩu ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt."
            );
        var role = await users.AddToRoleAsync(user, "Customer");
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            throw new ShopException(503, "Chưa cấu hình vai trò. Hãy chạy seed database.");
        }
        return StatusCode(201, await tokens.CreateAsync(user));
    }

    // Cổng đăng nhập khách hàng: tài khoản Admin KHÔNG dùng chung cổng này.
    [HttpPost("login")]
    public async Task<AuthResponse> Login(LoginRequest request)
    {
        var user = await AuthenticateAsync(request);
        if (await users.IsInRoleAsync(user, "Admin"))
            throw new ShopException(
                403,
                "Tài khoản quản trị không đăng nhập ở cổng khách hàng. Vui lòng vào trang /admin của website để đăng nhập quản trị."
            );
        return await tokens.CreateAsync(user);
    }

    // Cổng đăng nhập quản trị riêng (dùng bởi trang /admin/login của cả ba frontend):
    // chỉ chấp nhận tài khoản có vai trò Admin, tài khoản khách hàng bị từ chối ngay tại API.
    [HttpPost("admin/login")]
    public async Task<AuthResponse> AdminLogin(LoginRequest request)
    {
        var user = await AuthenticateAsync(request);
        if (!await users.IsInRoleAsync(user, "Admin"))
            throw new ShopException(
                403,
                "Tài khoản này không có quyền quản trị. Khu vực /admin chỉ dành cho tài khoản quản trị riêng."
            );
        return await tokens.CreateAsync(user);
    }

    private async Task<ApplicationUser> AuthenticateAsync(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            throw new ShopException(
                401,
                "Thông tin đăng nhập không đúng hoặc tài khoản đang bị khóa."
            );
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            throw new ShopException(
                401,
                "Thông tin đăng nhập không đúng hoặc tài khoản đang bị khóa."
            );
        }
        await users.ResetAccessFailedCountAsync(user);
        return user;
    }

    [Authorize, HttpGet("me")]
    public async Task<UserDto> Me()
    {
        var user =
            await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!)
            ?? throw new ShopException(401, "Phiên đăng nhập không hợp lệ.");
        return new(
            user.Id,
            user.FullName,
            user.Email!,
            user.PhoneNumber ?? "",
            (await users.GetRolesAsync(user)).ToList(),
            user.IsActive,
            user.CreatedAt
        );
    }
}

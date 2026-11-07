using ElectroShop.Api.Authentication;
using ElectroShop.Api.Entities;
using ElectroShop.Api.Services;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Requests;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace ElectroShop.Api.Controllers;

[ApiController, Route("api/auth")]
public class AuthController(UserManager<ApplicationUser> users, TokenService tokens) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request)
    {
        var user = new ApplicationUser { UserName = request.Email.Trim(), Email = request.Email.Trim(), FullName = request.FullName.Trim(), PhoneNumber = request.PhoneNumber.Trim(), LockoutEnabled = true };
        var result = await users.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new ShopException(400, "Không thể đăng ký. Email phải duy nhất; mật khẩu ít nhất 8 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.");
        var role = await users.AddToRoleAsync(user, "Customer");
        if (!role.Succeeded)
        {
            await users.DeleteAsync(user);
            throw new ShopException(503, "Chưa cấu hình vai trò. Hãy chạy seed database.");
        }
        return StatusCode(201, await tokens.CreateAsync(user));
    }
    [HttpPost("login")]
    public async Task<AuthResponse> Login(LoginRequest request)
    {
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null || !user.IsActive || await users.IsLockedOutAsync(user))
            throw new ShopException(401, "Thông tin đăng nhập không đúng hoặc tài khoản đang bị khóa.");
        if (!await users.CheckPasswordAsync(user, request.Password))
        {
            await users.AccessFailedAsync(user);
            throw new ShopException(401, "Thông tin đăng nhập không đúng hoặc tài khoản đang bị khóa.");
        }
        await users.ResetAccessFailedCountAsync(user);
        return await tokens.CreateAsync(user);
    }
    [Authorize, HttpGet("me")]
    public async Task<UserDto> Me()
    {
        var user = await users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier)!) ?? throw new ShopException(401, "Phiên đăng nhập không hợp lệ.");
        return new(user.Id, user.FullName, user.Email!, user.PhoneNumber ?? "", (await users.GetRolesAsync(user)).ToList(), user.IsActive, user.CreatedAt);
    }
}

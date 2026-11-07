using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ElectroShop.Api.Entities;
using ElectroShop.Contracts.DTOs;
using ElectroShop.Contracts.Responses;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
namespace ElectroShop.Api.Authentication;

public class TokenService(IConfiguration configuration, UserManager<ApplicationUser> users)
{
    public async Task<AuthResponse> CreateAsync(ApplicationUser user)
    {
        var roles = (await users.GetRolesAsync(user)).ToList();
        var expires = DateTimeOffset.UtcNow.AddMinutes(60);
        var claims = new List<Claim> { new(JwtRegisteredClaimNames.Sub, user.Id), new(ClaimTypes.Name, user.FullName), new("stamp", user.SecurityStamp ?? ""), new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) };
        claims.AddRange(roles.Select(x => new Claim(ClaimTypes.Role, x)));
        var jwt = new JwtSecurityToken(configuration["Jwt:Issuer"], configuration["Jwt:Audience"], claims, expires: expires.UtcDateTime,
         signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(jwt), expires, new UserDto(user.Id, user.FullName, user.Email!, user.PhoneNumber ?? "", roles, user.IsActive, user.CreatedAt));
    }
}

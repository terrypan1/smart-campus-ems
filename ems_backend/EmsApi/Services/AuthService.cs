using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using EmsApi.Dtos;
using EmsApi.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EmsApi.Services;

public class AuthService(EmsDbContext db, IConfiguration config)
{
	private static readonly PasswordHasher<User> Hasher = new();

	public async Task<LoginResponseDto> LoginAsync(LoginRequestDto req)
	{
		var user = await db.Users.FirstOrDefaultAsync(u => u.Account == req.Account && u.IsActive);
		var result = user == null ? PasswordVerificationResult.Failed : Hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password);
		// 帳號不存在與密碼錯誤回同一句話，不透露帳號是否存在
		if (user == null || result == PasswordVerificationResult.Failed) throw new UnauthorizedAccessException("帳號或密碼錯誤");

		if (result == PasswordVerificationResult.SuccessRehashNeeded)
		{
			user.PasswordHash = Hasher.HashPassword(user, req.Password);
			await db.SaveChangesAsync();
		}

		var expires = DateTime.UtcNow.AddHours(config.GetValue("Jwt:ExpireHours", 12));
		var token = new JwtSecurityToken(
			issuer: config["Jwt:Issuer"],
			audience: config["Jwt:Audience"],
			claims:
			[
				new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
				new Claim(ClaimTypes.Name, user.Account),
				new Claim(ClaimTypes.Role, user.Role),
			],
			expires: expires,
			signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:SignKey"]!)), SecurityAlgorithms.HmacSha256));

		return new LoginResponseDto(new JwtSecurityTokenHandler().WriteToken(token), expires, ToDto(user));
	}

	public async Task<UserDto> GetUserAsync(int userId)
	{
		var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive) ?? throw new UnauthorizedAccessException("帳號不存在或已停用");
		return ToDto(user);
	}

	private static UserDto ToDto(User u) => new(u.Id, u.Account, u.DisplayName, u.Role);
}

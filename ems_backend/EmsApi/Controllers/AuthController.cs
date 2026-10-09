using System.Security.Claims;
using EmsApi.Dtos;
using EmsApi.Services;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController(AuthService authService) : ControllerBase
{
	[AllowAnonymous]
	[HttpPost("Login")]
	public async Task<IActionResult> Login([FromBody] LoginRequestDto request) =>
		Ok(ApiResult.Ok(await authService.LoginAsync(request), "登入成功"));

	[HttpGet("Me")]
	public async Task<IActionResult> Me()
	{
		var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
		return Ok(ApiResult.Ok(await authService.GetUserAsync(userId)));
	}
}

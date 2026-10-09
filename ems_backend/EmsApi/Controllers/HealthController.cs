using EmsApi.Models;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class HealthController(EmsDbContext db) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> Get()
	{
		var dbOk = await db.Database.CanConnectAsync();
		return Ok(ApiResult.Ok(new { db = dbOk, time = DateTimeOffset.UtcNow }));
	}
}

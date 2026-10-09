using EmsApi.Services;
using EmsApi.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class MetersController(MeterQueryService meters) : ControllerBase
{
	[HttpGet]
	public async Task<IActionResult> GetAll() => Ok(ApiResult.Ok(await meters.GetMetersAsync()));

	/// <summary>每顆電表最新一筆讀值與通訊狀態</summary>
	[HttpGet("Latest")]
	public async Task<IActionResult> GetLatest() => Ok(ApiResult.Ok(await meters.GetLatestAsync()));
}

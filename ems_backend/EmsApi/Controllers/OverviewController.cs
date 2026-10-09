using EmsApi.Services.Energy;
using EmsApi.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OverviewController(OverviewService overview) : ControllerBase
{
	/// <summary>首頁摘要：今天有沒有超標、今日／本月用電與去年同期、本月最高需量、電費、碳排、太陽能、系統狀態</summary>
	[HttpGet]
	public async Task<IActionResult> Get() => Ok(ApiResult.Ok(await overview.GetAsync(DateTimeOffset.UtcNow)));
}

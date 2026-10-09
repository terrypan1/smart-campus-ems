using EmsApi.Services.Energy;
using EmsApi.Services.Settings;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class EnergyController(EnergyQueryService energy, StructureService structure, AggregationService aggregation, IServiceScopeFactory scopeFactory, ILogger<EnergyController> logger) : ControllerBase
{
	/// <summary>用電結構：level＝day／month，groupBy＝spaceType／building／category／meter；buildingId 下鑽到某棟</summary>
	[HttpGet("Structure")]
	public async Task<IActionResult> Structure([FromQuery] string level, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] string groupBy = "building", [FromQuery] int? buildingId = null) =>
		Ok(ApiResult.Ok(await structure.GetAsync(level, from, to, groupBy, buildingId)));

	/// <summary>全校（不帶 buildingId）或某棟，每日／每月用電</summary>
	[HttpGet("Summary")]
	public async Task<IActionResult> Summary([FromQuery] string level, [FromQuery] DateOnly from, [FromQuery] DateOnly to, [FromQuery] int? buildingId) =>
		Ok(ApiResult.Ok(await energy.GetSummaryAsync(level, from, to, buildingId)));

	/// <summary>某顆電表某一天 24 小時的用電與狀態</summary>
	[HttpGet("Hourly")]
	public async Task<IActionResult> Hourly([FromQuery] int meterId, [FromQuery] DateOnly date) =>
		Ok(ApiResult.Ok(await energy.GetHourlyAsync(meterId, date)));

	/// <summary>手動全部重算（backfill 之後用）。在背景執行，立即回應</summary>
	[Authorize(Policy = "Admin")]
	[HttpPost("Recompute")]
	public IActionResult Recompute()
	{
		_ = Task.Run(async () =>
		{
			try
			{
				await aggregation.RecomputeAllAsync(CancellationToken.None);
				using var scope = scopeFactory.CreateScope();
				var n = await scope.ServiceProvider.GetRequiredService<AlarmService>().RebuildHistoryAsync(CancellationToken.None);
				logger.LogInformation("手動重算完成，已重新補記歷史告警（{N} 列異動）", n);
			}
			catch (Exception ex) { logger.LogError(ex, "手動重算失敗"); }
		});
		return Accepted(ApiResult.Ok<object?>(null, "已開始重算，完成後會寫入 log"));
	}
}

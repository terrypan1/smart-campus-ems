using EmsApi.Dtos;
using EmsApi.Services.Energy;
using EmsApi.Services.Settings;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class DemandController(DemandService demand, AggregationService aggregation, IServiceScopeFactory scopeFactory, ILogger<DemandController> logger) : ControllerBase
{
	private static DateOnly Today => AggregationService.LocalDate(DateTimeOffset.UtcNow);

	/// <summary>目前 15 分鐘區塊的即時需量、契約容量與狀態</summary>
	[HttpGet("Realtime")]
	public async Task<IActionResult> Realtime() => Ok(ApiResult.Ok(await demand.GetRealtimeAsync(DateTimeOffset.UtcNow)));

	/// <summary>某天（台北）96 個 15 分鐘需量</summary>
	[HttpGet("Day")]
	public async Task<IActionResult> Day([FromQuery] DateOnly? date) => Ok(ApiResult.Ok(await demand.GetDayAsync(date ?? Today, DateTimeOffset.UtcNow)));

	/// <summary>最近 N 個月各時段最高需量與超約</summary>
	[HttpGet("Monthly")]
	public async Task<IActionResult> Monthly([FromQuery] int months = 13) => Ok(ApiResult.Ok(await demand.GetMonthlyAsync(months, Today)));

	/// <summary>需量事件（預設最近 30 天，最多 500 筆）</summary>
	[HttpGet("Events")]
	public async Task<IActionResult> Events([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
	{
		var f = AggregationService.LocalDayStart(from ?? Today.AddDays(-29));
		var t = AggregationService.LocalDayStart((to ?? Today).AddDays(1));
		return Ok(ApiResult.Ok(await demand.GetEventsAsync(f, t)));
	}

	[HttpGet("Contract")]
	public async Task<IActionResult> GetContract() => Ok(ApiResult.Ok(DemandService.ToDto(await demand.GetContractAsync())));

	/// <summary>修改契約容量與警戒門檻；改完在背景重新補記歷史超約事件</summary>
	[Authorize(Policy = "Admin")]
	[HttpPut("Contract")]
	public async Task<IActionResult> PutContract([FromBody] ContractUpdateDto dto)
	{
		var result = await demand.UpdateContractAsync(dto, User.Identity?.Name ?? "");
		_ = Task.Run(async () =>
		{
			try
			{
				await aggregation.RebuildHistoryEventsAsync(CancellationToken.None);
				using var scope = scopeFactory.CreateScope();
				await scope.ServiceProvider.GetRequiredService<AlarmService>().RebuildHistoryAsync(CancellationToken.None);
			}
			catch (Exception ex) { logger.LogError(ex, "重新補記歷史超約事件／告警失敗"); }
		});
		return Ok(ApiResult.Ok(result, "已更新契約設定"));
	}
}

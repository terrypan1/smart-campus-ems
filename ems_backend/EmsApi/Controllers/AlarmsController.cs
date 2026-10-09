using EmsApi.Dtos;
using EmsApi.Services.Settings;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AlarmsController(AlarmService alarms) : ControllerBase
{
	/// <summary>status：active（未結束）／unack（未確認）／all</summary>
	[HttpGet]
	public async Task<IActionResult> List([FromQuery] string status = "all", [FromQuery] string? type = null, [FromQuery] string? level = null,
		[FromQuery] DateTimeOffset? from = null, [FromQuery] DateTimeOffset? to = null, [FromQuery] int limit = 500) =>
		Ok(ApiResult.Ok(await alarms.ListAsync(status, type, level, from, to, limit)));

	/// <summary>未確認數、各等級、未結束各類型、最新 10 筆（頂欄鈴鐺）</summary>
	[HttpGet("Summary")]
	public async Task<IActionResult> Summary() => Ok(ApiResult.Ok(await alarms.SummaryAsync()));

	/// <summary>批次確認</summary>
	[HttpPost("Ack")]
	public async Task<IActionResult> Ack([FromBody] AckRequestDto dto) =>
		Ok(ApiResult.Ok(await alarms.AckAsync(dto.Ids, User.Identity?.Name ?? ""), "已確認"));

	[HttpPost("{id:long}/Ack")]
	public async Task<IActionResult> AckOne(long id) => Ok(ApiResult.Ok(await alarms.AckAsync([id], User.Identity?.Name ?? ""), "已確認"));

	/// <summary>重新補記歷史告警（重算彙總、改契約之後）</summary>
	[Authorize(Policy = "Admin")]
	[HttpPost("RebuildHistory")]
	public async Task<IActionResult> RebuildHistory(CancellationToken ct) => Ok(ApiResult.Ok(await alarms.RebuildHistoryAsync(ct), "已重新補記歷史告警"));
}

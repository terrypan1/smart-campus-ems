using EmsApi.Dtos;
using EmsApi.Services.Energy;
using EmsApi.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CarbonController(CarbonService carbon) : ControllerBase
{
	private static DateOnly Today => AggregationService.LocalDate(DateTimeOffset.UtcNow);

	/// <summary>近 N 個月每月排放與太陽能減碳（逐月套該年度係數）</summary>
	[HttpGet("Monthly")]
	public async Task<IActionResult> Monthly([FromQuery] int months = 13) => Ok(ApiResult.Ok(await carbon.MonthlyAsync(months, Today)));

	/// <summary>某年 1/1 到今天（或年底）累計，對去年同期</summary>
	[HttpGet("Summary")]
	public async Task<IActionResult> Summary([FromQuery] int? year) => Ok(ApiResult.Ok(await carbon.SummaryAsync(year ?? Today.Year, Today)));

	/// <summary>已建的排碳係數</summary>
	[HttpGet("Factors")]
	public async Task<IActionResult> Factors() =>
		Ok(ApiResult.Ok((await carbon.FactorsAsync()).Select(f => new EmissionFactorDto(f.Year, f.Factor, f.Source, f.IsProvisional)).ToList()));

	/// <summary>每月明細 Excel</summary>
	[HttpGet("Export")]
	public async Task<IActionResult> Export([FromQuery] int months = 13) =>
		File(await carbon.ExportAsync(months, Today), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"EMS_碳排_{Today:yyyyMMdd}.xlsx");
}

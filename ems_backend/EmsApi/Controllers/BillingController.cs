using EmsApi.Services.Energy;
using EmsApi.Utils;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

[Route("api/[controller]")]
[ApiController]
public class BillingController(BillingService billing) : ControllerBase
{
	private static DateOnly Today => AggregationService.LocalDate(DateTimeOffset.UtcNow);

	/// <summary>某月電費試算明細（month 任一天皆可，預設本月）</summary>
	[HttpGet("Estimate")]
	public async Task<IActionResult> Estimate([FromQuery] DateOnly? month) => Ok(ApiResult.Ok(await billing.EstimateAsync(month ?? Today, Today)));

	/// <summary>最近 N 個月電費總額</summary>
	[HttpGet("History")]
	public async Task<IActionResult> History([FromQuery] int months = 13) => Ok(ApiResult.Ok(await billing.HistoryAsync(months, Today)));

	/// <summary>現行電價表（含來源文件與頁碼）</summary>
	[HttpGet("Tariff")]
	public async Task<IActionResult> Tariff() => Ok(ApiResult.Ok(await billing.GetTariffAsync()));
}

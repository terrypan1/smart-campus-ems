using System.Security.Claims;
using EmsApi.Dtos;
using EmsApi.Services.Settings;
using EmsApi.Utils;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EmsApi.Controllers;

/// <summary>系統設定（docs/實作/06 §3）：讀取開放給登入者（電價、時段、離峰日畫面要顯示），修改限 admin</summary>
[Route("api/[controller]")]
[ApiController]
public class SettingsController(SettingsService settings) : ControllerBase
{
	private string Account => User.Identity?.Name ?? "";

	[HttpGet("Tariff")]
	public async Task<IActionResult> Tariff() => Ok(ApiResult.Ok(await settings.TariffRatesAsync()));

	[Authorize(Policy = "Admin")]
	[HttpPut("Tariff")]
	public async Task<IActionResult> PutTariff([FromBody] List<TariffRateEditDto> edits)
	{
		await settings.UpdateTariffAsync(edits, Account);
		return Ok(ApiResult.Ok(await settings.TariffRatesAsync(), "已更新電價"));
	}

	[Authorize(Policy = "Admin")]
	[HttpPost("Tariff/Restore")]
	public async Task<IActionResult> RestoreTariff() => Ok(ApiResult.Ok(await settings.RestoreTariffAsync(Account), "已還原台電公告值"));

	[HttpGet("Scheme")]
	public async Task<IActionResult> Scheme() => Ok(ApiResult.Ok(await settings.SchemeAsync()));

	[Authorize(Policy = "Admin")]
	[HttpPut("Scheme")]
	public async Task<IActionResult> PutScheme([FromBody] SchemeDto dto) => Ok(ApiResult.Ok(await settings.SetSchemeAsync(dto.Scheme, Account), "已切換時段方案，請重算彙總"));

	[HttpGet("OffPeakDays")]
	public async Task<IActionResult> OffPeakDays() => Ok(ApiResult.Ok(await settings.OffPeakDaysAsync()));

	[Authorize(Policy = "Admin")]
	[HttpPost("OffPeakDays")]
	public async Task<IActionResult> AddOffPeakDay([FromBody] OffPeakDayCreateDto dto) => Ok(ApiResult.Ok(await settings.AddOffPeakDayAsync(dto, Account), "已新增離峰日，請重算彙總"));

	[Authorize(Policy = "Admin")]
	[HttpDelete("OffPeakDays/{id:int}")]
	public async Task<IActionResult> DeleteOffPeakDay(int id)
	{
		await settings.DeleteOffPeakDayAsync(id, Account);
		return Ok(ApiResult.Ok<object?>(null, "已刪除離峰日，請重算彙總"));
	}

	[Authorize(Policy = "Admin")]
	[HttpPut("EmissionFactors")]
	public async Task<IActionResult> PutFactor([FromBody] EmissionFactorEditDto dto)
	{
		await settings.UpsertFactorAsync(dto, Account);
		return Ok(ApiResult.Ok<object?>(null, "已儲存排碳係數"));
	}

	[Authorize(Policy = "Admin")]
	[HttpDelete("EmissionFactors/{year:int}")]
	public async Task<IActionResult> DeleteFactor(int year)
	{
		await settings.DeleteFactorAsync(year, Account);
		return Ok(ApiResult.Ok<object?>(null, "已刪除"));
	}

	[Authorize(Policy = "Admin")]
	[HttpGet("Meters")]
	public async Task<IActionResult> Meters() => Ok(ApiResult.Ok(await settings.MetersAsync()));

	[Authorize(Policy = "Admin")]
	[HttpPut("Meters/{id:int}")]
	public async Task<IActionResult> PutMeter(int id, [FromBody] MeterUpdateDto dto)
	{
		await settings.UpdateMeterAsync(id, dto, Account);
		return Ok(ApiResult.Ok<object?>(null, "已更新電表"));
	}

	/// <summary>用畫面上填的連線立即讀一次（不寫入資料庫）</summary>
	[Authorize(Policy = "Admin")]
	[HttpPost("Meters/{id:int}/Test")]
	public async Task<IActionResult> TestMeter(int id, [FromBody] MeterUpdateDto dto) =>
		Ok(ApiResult.Ok(await SettingsService.TestMeterAsync(dto.ModbusHost, dto.ModbusPort, dto.UnitId, id)));

	[Authorize(Policy = "Admin")]
	[HttpGet("Users")]
	public async Task<IActionResult> Users() => Ok(ApiResult.Ok(await settings.UsersAsync()));

	[Authorize(Policy = "Admin")]
	[HttpPost("Users")]
	public async Task<IActionResult> CreateUser([FromBody] UserCreateDto dto) => Ok(ApiResult.Ok(await settings.CreateUserAsync(dto, Account), "已新增使用者"));

	[Authorize(Policy = "Admin")]
	[HttpPut("Users/{id:int}")]
	public async Task<IActionResult> PutUser(int id, [FromBody] UserUpdateDto dto)
	{
		await settings.UpdateUserAsync(id, dto, Account);
		return Ok(ApiResult.Ok<object?>(null, "已更新使用者"));
	}

	[Authorize(Policy = "Admin")]
	[HttpPost("Users/{id:int}/ResetPassword")]
	public async Task<IActionResult> ResetPassword(int id, [FromBody] PasswordResetDto dto)
	{
		await settings.ResetPasswordAsync(id, dto.Password, Account);
		return Ok(ApiResult.Ok<object?>(null, "已重設密碼"));
	}

	/// <summary>每個人改自己的密碼</summary>
	[HttpPost("ChangePassword")]
	public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
	{
		await settings.ChangeOwnPasswordAsync(int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!), dto);
		return Ok(ApiResult.Ok<object?>(null, "已變更密碼"));
	}

	[Authorize(Policy = "Admin")]
	[HttpGet("Audit")]
	public async Task<IActionResult> Audit([FromQuery] int limit = 300) => Ok(ApiResult.Ok(await settings.AuditAsync(limit)));
}

using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using EmsApi.Dtos;
using EmsApi.Models;
using EmsApi.Services.Collector;
using EmsApi.Services.Energy;
using FluentModbus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Settings;

/// <summary>系統設定（docs/實作/06 §3）：電價、時段方案、離峰日、排碳係數、電表、使用者；每個修改都寫稽核</summary>
public class SettingsService(EmsDbContext db, AuditService audit, RuntimeSettings runtime)
{
	private static readonly PasswordHasher<User> Hasher = new();

	/* ---------- 電價 ---------- */

	public Task<List<TariffRateAdminDto>> TariffRatesAsync() =>
		db.TariffRates.AsNoTracking().Where(r => r.Scheme == TariffItems.SchemeHv3Univ)
			.OrderBy(r => r.Item).ThenByDescending(r => r.Season)
			.Select(r => new TariffRateAdminDto(r.Season, r.Item, r.Rate, r.OfficialRate, r.Rate != r.OfficialRate, r.Source, r.UpdatedAt, r.UpdatedBy))
			.ToListAsync();

	public async Task UpdateTariffAsync(List<TariffRateEditDto> edits, string account)
	{
		var rows = await db.TariffRates.Where(r => r.Scheme == TariffItems.SchemeHv3Univ).ToListAsync();
		var changes = new List<object>();
		foreach (var e in edits)
		{
			if (e.Rate < 0 || e.Rate > 10000) throw new ArgumentException($"{e.Season}／{e.Item} 費率不合理：{e.Rate}");
			var r = rows.FirstOrDefault(x => x.Season == e.Season && x.Item == e.Item) ?? throw new ArgumentException($"沒有這個費率項目：{e.Season}／{e.Item}");
			if (r.Rate == e.Rate) continue;
			changes.Add(new { r.Season, r.Item, before = r.Rate, after = e.Rate });
			(r.Rate, r.UpdatedAt, r.UpdatedBy) = (e.Rate, DateTimeOffset.UtcNow, account);
		}
		if (changes.Count == 0) return;
		audit.Add(account, "電價", null, changes);
		await db.SaveChangesAsync();
	}

	public async Task<int> RestoreTariffAsync(string account)
	{
		var rows = await db.TariffRates.Where(r => r.Scheme == TariffItems.SchemeHv3Univ && r.Rate != r.OfficialRate).ToListAsync();
		if (rows.Count == 0) return 0;
		audit.Add(account, "電價（還原台電公告值）", rows.Select(r => new { r.Season, r.Item, r.Rate }).ToList(), rows.Select(r => new { r.Season, r.Item, Rate = r.OfficialRate }).ToList());
		foreach (var r in rows) (r.Rate, r.UpdatedAt, r.UpdatedBy) = (r.OfficialRate, DateTimeOffset.UtcNow, account);
		await db.SaveChangesAsync();
		return rows.Count;
	}

	/* ---------- 時段方案 ---------- */

	public async Task<SchemeDto> SchemeAsync()
	{
		var s = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(x => x.Key == RuntimeSettings.TouSchemeKey);
		return new SchemeDto(runtime.TouScheme, s?.UpdatedAt, s?.UpdatedBy);
	}

	/// <summary>切換 HV3／HV2：立即生效於新的計算；歷史彙總要重算才會更新（畫面提供立即重算）</summary>
	public async Task<SchemeDto> SetSchemeAsync(string scheme, string account)
	{
		if (scheme != TouSchemes.HighVoltage3 && scheme != TouSchemes.HighVoltage2) throw new ArgumentException("時段方案只能是 HV3（三段式）或 HV2（二段式）");
		var s = await db.AppSettings.FirstOrDefaultAsync(x => x.Key == RuntimeSettings.TouSchemeKey);
		var before = runtime.TouScheme;
		if (s == null) db.AppSettings.Add(s = new AppSetting { Key = RuntimeSettings.TouSchemeKey });
		(s.Value, s.UpdatedAt, s.UpdatedBy) = (scheme, DateTimeOffset.UtcNow, account);
		audit.Add(account, "時段方案", new { scheme = before }, new { scheme });
		await db.SaveChangesAsync();
		runtime.TouScheme = scheme;
		return new SchemeDto(scheme, s.UpdatedAt, s.UpdatedBy);
	}

	/* ---------- 離峰日 ---------- */

	public async Task<List<OffPeakDayDto>> OffPeakDaysAsync()
	{
		var rows = await db.OffPeakDays.AsNoTracking().OrderBy(d => d.Date).Select(d => new OffPeakDayDto(d.Id, d.Date, d.Name, false)).ToListAsync();
		// 國曆固定日期（條文固定，寫在程式裡）也列出來，方便對照
		var fixedDays = TouCalendar.FixedOffPeakNames.Select(f => new OffPeakDayDto(null, new DateOnly(2000, f.MonthDay / 100, f.MonthDay % 100), f.Name, true));
		return [.. fixedDays, .. rows];
	}

	public async Task<OffPeakDayDto> AddOffPeakDayAsync(OffPeakDayCreateDto dto, string account)
	{
		if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("請輸入名稱");
		if (await db.OffPeakDays.AnyAsync(d => d.Date == dto.Date)) throw new ArgumentException($"{dto.Date:yyyy-MM-dd} 已經是離峰日");
		var d = new OffPeakDay { Date = dto.Date, Name = dto.Name.Trim() };
		db.OffPeakDays.Add(d);
		audit.Add(account, "離峰日", null, new { d.Date, d.Name });
		await db.SaveChangesAsync();
		await ReloadOffPeakDaysAsync();
		return new OffPeakDayDto(d.Id, d.Date, d.Name, false);
	}

	public async Task DeleteOffPeakDayAsync(int id, string account)
	{
		var d = await db.OffPeakDays.FindAsync(id) ?? throw new KeyNotFoundException("找不到這個離峰日");
		db.OffPeakDays.Remove(d);
		audit.Add(account, "離峰日", new { d.Date, d.Name }, null);
		await db.SaveChangesAsync();
		await ReloadOffPeakDaysAsync();
	}

	public async Task ReloadOffPeakDaysAsync() => TouCalendar.SetOffPeakDays(await db.OffPeakDays.AsNoTracking().Select(d => d.Date).ToListAsync());

	/* ---------- 排碳係數 ---------- */

	public async Task UpsertFactorAsync(EmissionFactorEditDto dto, string account)
	{
		if (dto.Year is < 2000 or > 2100) throw new ArgumentException("年度請用西元年");
		if (dto.Factor is <= 0 or > 2) throw new ArgumentException("係數不合理（kg CO2e/kWh）");
		var f = await db.EmissionFactors.FirstOrDefaultAsync(x => x.Year == dto.Year);
		var before = f == null ? null : new { f.Year, f.Factor, f.Source, f.IsProvisional };
		if (f == null) db.EmissionFactors.Add(f = new EmissionFactor { Year = dto.Year });
		(f.Factor, f.Source, f.IsProvisional) = (dto.Factor, dto.Source.Trim(), dto.IsProvisional);
		audit.Add(account, $"排碳係數 {dto.Year}", before, new { f.Year, f.Factor, f.Source, f.IsProvisional });
		await db.SaveChangesAsync();
	}

	public async Task DeleteFactorAsync(int year, string account)
	{
		var f = await db.EmissionFactors.FirstOrDefaultAsync(x => x.Year == year) ?? throw new KeyNotFoundException("找不到這個年度");
		if (await db.EmissionFactors.CountAsync() == 1) throw new ArgumentException("至少要保留一個年度的係數");
		db.EmissionFactors.Remove(f);
		audit.Add(account, $"排碳係數 {year}", new { f.Year, f.Factor, f.Source }, null);
		await db.SaveChangesAsync();
	}

	/* ---------- 電表 ---------- */

	public Task<List<MeterAdminDto>> MetersAsync() =>
		db.Meters.AsNoTracking().OrderBy(m => m.Building!.Code).ThenBy(m => m.UnitId)
			.Select(m => new MeterAdminDto(m.Id, m.Code, m.Name, m.Category, m.RatedKw, m.BuildingId, m.Building!.Name, m.ModbusHost, m.ModbusPort, m.UnitId, m.IsEnabled))
			.ToListAsync();

	public static readonly string[] Categories = ["lighting_socket", "aircon", "power", "other", "solar"];

	/// <summary>不提供刪除（有歷史資料），只能停用；接真實電表時改 Modbus 連線即可</summary>
	public async Task UpdateMeterAsync(int id, MeterUpdateDto dto, string account)
	{
		if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("請輸入名稱");
		if (!Categories.Contains(dto.Category)) throw new ArgumentException("用途不正確");
		if (string.IsNullOrWhiteSpace(dto.ModbusHost)) throw new ArgumentException("請輸入 Modbus host");
		if (dto.ModbusPort is < 1 or > 65535) throw new ArgumentException("Port 必須在 1–65535");
		if (dto.UnitId is < 1 or > 247) throw new ArgumentException("Unit ID 必須在 1–247");
		var m = await db.Meters.FindAsync(id) ?? throw new KeyNotFoundException("找不到這顆電表");
		var before = new { m.Name, m.Category, m.IsEnabled, m.ModbusHost, m.ModbusPort, m.UnitId };
		(m.Name, m.Category, m.IsEnabled, m.ModbusHost, m.ModbusPort, m.UnitId) = (dto.Name.Trim(), dto.Category, dto.IsEnabled, dto.ModbusHost.Trim(), dto.ModbusPort, dto.UnitId);
		audit.Add(account, $"電表 {m.Code}", before, new { m.Name, m.Category, m.IsEnabled, m.ModbusHost, m.ModbusPort, m.UnitId });
		await db.SaveChangesAsync();
	}

	/// <summary>立即讀一次（用畫面上填的連線，還沒儲存也能測），不寫入資料庫</summary>
	public static async Task<MeterTestDto> TestMeterAsync(string host, int port, int unitId, int meterId)
	{
		var sw = Stopwatch.StartNew();
		using var client = new ModbusTcpClient { ConnectTimeout = 2000, ReadTimeout = 2000, WriteTimeout = 2000 };
		try
		{
			var address = IPAddress.TryParse(host, out var ip) ? ip : (await Dns.GetHostAddressesAsync(host)).First(a => a.AddressFamily == AddressFamily.InterNetwork);
			client.Connect(new IPEndPoint(address, port), ModbusEndianness.BigEndian);
			var data = await client.ReadHoldingRegistersAsync((byte)unitId, MeterRegisterDecoder.StartAddress, MeterRegisterDecoder.RegisterCount);
			var r = MeterRegisterDecoder.Decode(data.Span, meterId, DateTimeOffset.UtcNow);
			return new MeterTestDto(true, "讀取成功", (int)sw.ElapsedMilliseconds, r.Kw, r.Pf, (r.Vrs + r.Vst + r.Vtr) / 3, r.KwhTotal);
		}
		catch (Exception ex)
		{
			return new MeterTestDto(false, $"讀取失敗：{ex.Message}", (int)sw.ElapsedMilliseconds, null, null, null, null);
		}
	}

	/* ---------- 使用者 ---------- */

	public Task<List<UserAdminDto>> UsersAsync() =>
		db.Users.AsNoTracking().OrderBy(u => u.Id).Select(u => new UserAdminDto(u.Id, u.Account, u.DisplayName, u.Role, u.IsActive, u.CreatedAt)).ToListAsync();

	public async Task<UserAdminDto> CreateUserAsync(UserCreateDto dto, string account)
	{
		var acc = dto.Account.Trim();
		if (!global::System.Text.RegularExpressions.Regex.IsMatch(acc, "^[A-Za-z0-9_.-]{3,50}$")) throw new ArgumentException("帳號只能用英數字與 _ . -，3–50 字");
		if (await db.Users.AnyAsync(u => u.Account == acc)) throw new ArgumentException("帳號已存在");
		CheckRole(dto.Role);
		CheckPassword(dto.Password);
		var u = new User { Account = acc, DisplayName = string.IsNullOrWhiteSpace(dto.DisplayName) ? acc : dto.DisplayName.Trim(), Role = dto.Role, CreatedAt = DateTimeOffset.UtcNow };
		u.PasswordHash = Hasher.HashPassword(u, dto.Password);
		db.Users.Add(u);
		audit.Add(account, $"使用者 {acc}", null, new { u.Account, u.DisplayName, u.Role, u.IsActive });
		await db.SaveChangesAsync();
		return new UserAdminDto(u.Id, u.Account, u.DisplayName, u.Role, u.IsActive, u.CreatedAt);
	}

	public async Task UpdateUserAsync(int id, UserUpdateDto dto, string account)
	{
		CheckRole(dto.Role);
		var u = await db.Users.FindAsync(id) ?? throw new KeyNotFoundException("找不到這個使用者");
		// 不能停用或降級最後一個（啟用中的）admin
		if (u.Role == Roles.Admin && u.IsActive && (dto.Role != Roles.Admin || !dto.IsActive)
			&& !await db.Users.AnyAsync(x => x.Id != id && x.Role == Roles.Admin && x.IsActive))
			throw new ArgumentException("這是最後一個啟用中的管理者，不能停用或降級");
		var before = new { u.DisplayName, u.Role, u.IsActive };
		(u.DisplayName, u.Role, u.IsActive) = (string.IsNullOrWhiteSpace(dto.DisplayName) ? u.Account : dto.DisplayName.Trim(), dto.Role, dto.IsActive);
		audit.Add(account, $"使用者 {u.Account}", before, new { u.DisplayName, u.Role, u.IsActive });
		await db.SaveChangesAsync();
	}

	public async Task ResetPasswordAsync(int id, string password, string account)
	{
		CheckPassword(password);
		var u = await db.Users.FindAsync(id) ?? throw new KeyNotFoundException("找不到這個使用者");
		u.PasswordHash = Hasher.HashPassword(u, password);
		audit.Add(account, $"使用者 {u.Account}（重設密碼）", null, null);
		await db.SaveChangesAsync();
	}

	public async Task ChangeOwnPasswordAsync(int userId, ChangePasswordDto dto)
	{
		var u = await db.Users.FindAsync(userId) ?? throw new UnauthorizedAccessException("帳號不存在");
		if (Hasher.VerifyHashedPassword(u, u.PasswordHash, dto.OldPassword) == PasswordVerificationResult.Failed) throw new ArgumentException("目前的密碼不正確");
		CheckPassword(dto.NewPassword);
		u.PasswordHash = Hasher.HashPassword(u, dto.NewPassword);
		audit.Add(u.Account, $"使用者 {u.Account}（變更自己的密碼）", null, null);
		await db.SaveChangesAsync();
	}

	private static void CheckRole(string role)
	{
		if (role != Roles.Admin && role != Roles.Viewer) throw new ArgumentException("角色只能是 admin 或 viewer");
	}

	private static void CheckPassword(string password)
	{
		if (string.IsNullOrEmpty(password) || password.Length < 8) throw new ArgumentException("密碼至少 8 個字元");
	}

	/* ---------- 稽核 ---------- */

	public Task<List<AuditLogDto>> AuditAsync(int limit) =>
		db.AuditLogs.AsNoTracking().OrderByDescending(a => a.At).Take(Math.Clamp(limit, 1, 1000))
			.Select(a => new AuditLogDto(a.Id, a.At, a.Account, a.Item, a.Before, a.After)).ToListAsync();
}

using System.Text.Json;
using EmsApi.Models;
using EmsApi.Services.Energy;
using EmsApi.Services.Settings;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services;

/// <summary>啟動時：套用 migration → 主檔為空時匯入 data/meters.json → 沒有帳號時建立預設帳號</summary>
public static class DbSeeder
{
	public static async Task MigrateAndSeedAsync(IServiceProvider services, IConfiguration config, ILogger logger)
	{
		using var scope = services.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
		await db.Database.MigrateAsync();

		if (!await db.Campuses.AnyAsync())
		{
			var path = config["Seed:MetersFile"] ?? "data/meters.json";
			var catalog = JsonSerializer.Deserialize<CatalogFile>(await File.ReadAllTextAsync(path), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
				?? throw new InvalidOperationException($"讀不到電表主檔：{path}");
			var host = config["Collector:DefaultModbusHost"] ?? "127.0.0.1";
			var port = config.GetValue("Collector:DefaultModbusPort", 1502);

			var campus = new Campus { Code = catalog.Campus.Code, Name = catalog.Campus.Name, ContractKw = catalog.Campus.ContractKw };
			foreach (var bd in catalog.Buildings)
			{
				var building = new Building { Code = bd.Code, Name = bd.Name, SpaceType = bd.SpaceType };
				building.Meters.AddRange(catalog.Meters.Where(m => m.Building == bd.Code).Select(m => new Meter
				{
					Code = $"{catalog.Campus.Code}-{m.UnitId:00}",
					Name = m.Name,
					Category = m.Category,
					RatedKw = m.RatedKw,
					ModbusHost = host,
					ModbusPort = port,
					UnitId = m.UnitId,
				}));
				campus.Buildings.Add(building);
			}
			db.Campuses.Add(campus);
			await db.SaveChangesAsync();
			logger.LogInformation("已匯入電表主檔 {Path}：{Buildings} 棟、{Meters} 顆電表", path, catalog.Buildings.Count, catalog.Meters.Count);
		}

		// Demo 契約容量與警戒門檻（docs/實作/03 §4）；之後在畫面上改，不會被覆蓋
		if (!await db.ContractSettings.AnyAsync())
		{
			db.ContractSettings.Add(new ContractSetting
			{
				RegularKw = 1900, WarnPct = 70, ShedAirconPct = 85, ShedFanPct = 90, ShedLightPct = 95,
				UpdatedAt = DateTimeOffset.UtcNow, UpdatedBy = "seed",
			});
			logger.LogInformation("已建立預設契約容量 1,900 kW");
		}

		// 電價（docs/實作/03 §3.1–3.2）；06 的電價設定頁會改這張表
		if (!await db.TariffRates.AnyAsync())
		{
			const string src = "調整後各類用戶電價表（114/10/1 起實施）第 20–21 頁 高壓電力 三段式（尖峰時間固定）大學（專）適用";
			foreach (var (season, item, rate) in new (string, string, decimal)[]
			{
				(Seasons.Summer, TariffItems.BasicRegular, 223.60m), (Seasons.NonSummer, TariffItems.BasicRegular, 166.90m),
				(Seasons.Summer, TariffItems.BasicHalf, 166.90m), (Seasons.NonSummer, TariffItems.BasicHalf, 166.90m),
				(Seasons.Summer, TariffItems.BasicSatHalf, 44.70m), (Seasons.NonSummer, TariffItems.BasicSatHalf, 33.30m),
				(Seasons.Summer, TariffItems.BasicOff, 44.70m), (Seasons.NonSummer, TariffItems.BasicOff, 33.30m),
				(Seasons.Summer, TariffItems.EnergyPeak, 5.80m),
				(Seasons.Summer, TariffItems.EnergyHalf, 3.63m), (Seasons.NonSummer, TariffItems.EnergyHalf, 3.40m),
				(Seasons.Summer, TariffItems.EnergySatHalf, 1.78m), (Seasons.NonSummer, TariffItems.EnergySatHalf, 1.65m),
				(Seasons.Summer, TariffItems.EnergyOff, 1.58m), (Seasons.NonSummer, TariffItems.EnergyOff, 1.45m),
			})
				db.TariffRates.Add(new TariffRate { Scheme = TariffItems.SchemeHv3Univ, Season = season, Item = item, Rate = rate, OfficialRate = rate, Source = src });
			logger.LogInformation("已建立電價表 {Scheme}", TariffItems.SchemeHv3Univ);
		}
		await db.SaveChangesAsync();

		// 電力排碳係數（docs/實作/05 §2.1）：能源署公告的公用售電業電力排碳係數，公告文件存在 data/能源署排碳係數/
		if (!await db.EmissionFactors.AnyAsync())
		{
			foreach (var (year, factor, file) in new[]
			{
				(2022, 0.495m, "111年度電力排碳係數.pdf"),
				(2023, 0.494m, "112年度電力排碳係數.pdf"),
				(2024, 0.474m, "113年度電力排碳係數.pdf"),
				// 114 年起另有產業 0.466、民生住宅 0.471；沿用歷年同一口徑的公用售電業係數
				(2025, 0.467m, "114年度電力排碳係數.pdf（公用售電業；115/6/2 公告）"),
			})
				db.EmissionFactors.Add(new EmissionFactor { Year = year, Factor = factor, Source = $"經濟部能源署 {file}" });
			await db.SaveChangesAsync();
			logger.LogInformation("已建立電力排碳係數 111–114 年度");
		}

		// 農曆與補充的離峰日（docs/實作/06 §3.1）：初始值＝程式內建的 2025–2026；之後在系統設定新增
		if (!await db.OffPeakDays.AnyAsync())
		{
			db.OffPeakDays.AddRange(TouCalendar.BuiltInLunarDays.Select(d => new OffPeakDay { Date = d.Date, Name = d.Name }));
			await db.SaveChangesAsync();
			logger.LogInformation("已建立離峰日 {N} 天", TouCalendar.BuiltInLunarDays.Count);
		}
		TouCalendar.SetOffPeakDays(await db.OffPeakDays.AsNoTracking().Select(d => d.Date).ToListAsync());

		// 時段方案：系統設定改過就用資料庫的值，否則用 appsettings 的 Tariff:Scheme
		var scheme = await db.AppSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key == RuntimeSettings.TouSchemeKey);
		if (scheme != null) services.GetRequiredService<RuntimeSettings>().TouScheme = scheme.Value;

		// 預設帳號：設定檔有給密碼、資料庫還沒有這個帳號 → 建立。已存在的帳號不動（改設定不會覆蓋密碼）
		var hasher = new PasswordHasher<User>();
		var existing = await db.Users.Select(u => u.Account).ToListAsync();
		if (existing.Count == 0 && string.IsNullOrWhiteSpace(config["Seed:AdminPassword"]))
			throw new InvalidOperationException("資料庫沒有任何帳號，但 Seed:AdminPassword 尚未設定");
		foreach (var (account, name, role, key) in new[]
		{
			("admin", "系統管理員", Roles.Admin, "Seed:AdminPassword"),
			("viewer", "檢視者", Roles.Viewer, "Seed:ViewerPassword"),
			("sysadmin", "系統管理員", Roles.Admin, "Seed:SysadminPassword"),
		})
		{
			var password = config[key];
			if (existing.Contains(account) || string.IsNullOrWhiteSpace(password)) continue;
			var user = new User { Account = account, DisplayName = name, Role = role, CreatedAt = DateTimeOffset.UtcNow };
			user.PasswordHash = hasher.HashPassword(user, password);
			db.Users.Add(user);
			logger.LogInformation("已建立預設帳號 {Account}", account);
		}
		await db.SaveChangesAsync();
	}

	private sealed record CatalogFile(CampusFile Campus, List<BuildingFile> Buildings, List<MeterFile> Meters);
	private sealed record CampusFile(string Code, string Name, double ContractKw);
	private sealed record BuildingFile(string Code, string Name, string SpaceType);
	private sealed record MeterFile(int UnitId, string Building, string Name, string Category, double RatedKw);
}

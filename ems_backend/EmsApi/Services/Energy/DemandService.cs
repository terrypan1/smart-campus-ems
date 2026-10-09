using EmsApi.Dtos;
using EmsApi.Models;
using EmsApi.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>需量查詢、即時需量、契約設定（docs/實作/03 §5）</summary>
public class DemandService(EmsDbContext db, RuntimeSettings settings, AuditService audit)
{
	private string Scheme => settings.TouScheme;

	/// <summary>警戒等級（由低到高）：門檻是經常契約容量的百分比；卸載只記錄、不實際控制設備</summary>
	public static IEnumerable<(string Level, decimal ThresholdKw, string Action)> Thresholds(ContractSetting c) =>
	[
		(DemandLevels.Warn, c.RegularKw * c.WarnPct / 100, "警示：需量接近契約容量"),
		(DemandLevels.ShedAircon, c.RegularKw * c.ShedAirconPct / 100, "模擬卸載：關閉空調（不實際控制）"),
		(DemandLevels.ShedFan, c.RegularKw * c.ShedFanPct / 100, "模擬卸載：關閉進排風（不實際控制）"),
		(DemandLevels.ShedLight, c.RegularKw * c.ShedLightPct / 100, "模擬卸載：關閉照明（不實際控制）"),
	];

	/// <summary>需量已跨過的最高等級；沒跨過警示門檻回 null。capacityKw＝該時段契約容量</summary>
	public static string? LevelOf(decimal netKw, decimal capacityKw, ContractSetting c) =>
		netKw >= capacityKw ? DemandLevels.OverContract : Thresholds(c).LastOrDefault(t => netKw >= t.ThresholdKw).Level;

	public static Contract ToContract(ContractSetting c) => new(c.RegularKw, c.HalfPeakKw, c.SatHalfKw, c.OffPeakKw);

	public static ContractDto ToDto(ContractSetting c) =>
		new(c.RegularKw, c.HalfPeakKw, c.SatHalfKw, c.OffPeakKw, c.WarnPct, c.ShedAirconPct, c.ShedFanPct, c.ShedLightPct, c.UpdatedAt, c.UpdatedBy);

	private static object AuditView(ContractSetting c) =>
		new { c.RegularKw, c.HalfPeakKw, c.SatHalfKw, c.OffPeakKw, c.WarnPct, c.ShedAirconPct, c.ShedFanPct, c.ShedLightPct };

	public async Task<ContractSetting> GetContractAsync() =>
		await db.ContractSettings.AsNoTracking().OrderBy(c => c.Id).FirstOrDefaultAsync()
		?? throw new InvalidOperationException("contract_settings 沒有資料");

	public async Task<ContractDto> UpdateContractAsync(ContractUpdateDto dto, string account)
	{
		if (dto.RegularKw <= 0) throw new ArgumentException("經常契約容量必須大於 0");
		if (dto.HalfPeakKw < 0 || dto.SatHalfKw < 0 || dto.OffPeakKw < 0) throw new ArgumentException("契約容量不可為負數");
		if (!(dto.WarnPct > 0 && dto.WarnPct < dto.ShedAirconPct && dto.ShedAirconPct < dto.ShedFanPct && dto.ShedFanPct < dto.ShedLightPct && dto.ShedLightPct <= 100))
			throw new ArgumentException("警戒門檻必須 0 < 警示 < 關空調 < 關進排風 < 關照明 ≤ 100");

		var c = await db.ContractSettings.OrderBy(x => x.Id).FirstAsync();
		var before = AuditView(c);
		(c.RegularKw, c.HalfPeakKw, c.SatHalfKw, c.OffPeakKw) = (dto.RegularKw, dto.HalfPeakKw, dto.SatHalfKw, dto.OffPeakKw);
		(c.WarnPct, c.ShedAirconPct, c.ShedFanPct, c.ShedLightPct) = (dto.WarnPct, dto.ShedAirconPct, dto.ShedFanPct, dto.ShedLightPct);
		c.UpdatedAt = DateTimeOffset.UtcNow;
		c.UpdatedBy = account;
		audit.Add(account, "契約容量與警戒門檻", before, AuditView(c));
		await db.SaveChangesAsync();
		return ToDto(c);
	}

	/// <summary>
	/// 目前 15 分鐘區塊的即時需量：每顆電表「區塊起點到最新讀值」的平均；區塊剛開始或缺起點時改用瞬時 kW。
	/// 最新讀值超過 3 分鐘的電表視為斷線，不計入。
	/// </summary>
	public async Task<RealtimeDemandDto> GetRealtimeAsync(DateTimeOffset now)
	{
		var contract = await GetContractAsync();
		var block = DemandCalculator.FloorBlock(now);
		var since = block - DemandCalculator.Window;
		var meters = await db.Meters.AsNoTracking().Where(m => m.IsEnabled).Select(m => new { m.Id, m.Category }).ToListAsync();
		var readings = (await db.MeterReadings.AsNoTracking()
				.Where(r => r.SampledAt >= since)
				.OrderBy(r => r.SampledAt)
				.Select(r => new { r.MeterId, r.SampledAt, r.KwhTotal, r.Kw })
				.ToListAsync())
			.GroupBy(r => r.MeterId)
			.ToDictionary(g => g.Key, g => g.ToList());

		decimal load = 0, solar = 0, instLoad = 0, instSolar = 0;
		var online = 0;
		foreach (var m in meters)
		{
			if (!readings.TryGetValue(m.Id, out var rs) || now - rs[^1].SampledAt > MeterQueryService.OfflineAfter) continue;
			online++;
			var inst = (decimal)rs[^1].Kw;
			var avg = DemandCalculator.CurrentBlockKw(rs.Select(r => new KwhReading(r.SampledAt, r.KwhTotal)).ToList(), now) ?? inst;
			if (m.Category == "solar") { solar += avg; instSolar += inst; }
			else { load += avg; instLoad += inst; }
		}

		var tou = TouCalendar.Of(block, Scheme);
		var cap = ToContract(contract).CapacityOf(tou);
		var net = load - solar;
		var level = LevelOf(net, cap, contract);
		var status = level == null ? "normal" : level == DemandLevels.OverContract ? "over" : "warn";

		return new RealtimeDemandDto(now, block, (int)(now - block).TotalSeconds,
			Math.Round(load, 1), Math.Round(solar, 1), Math.Round(net, 1), Math.Round(instLoad - instSolar, 1),
			tou, cap, status, level, online, meters.Count, ToDto(contract));
	}

	/// <summary>
	/// 某天（台北）96 個區塊；還沒算到的區塊回 null。
	/// 今天：彙總要整點過 20 分鐘才定案，定案之後到目前已結束的區塊直接從原始讀值算（同一套 DemandCalculator），曲線才會到「現在」
	/// </summary>
	public async Task<List<DemandBlockDto>> GetDayAsync(DateOnly date, DateTimeOffset now)
	{
		var start = AggregationService.LocalDayStart(date);
		var end = start.AddDays(1);
		var rows = await db.CampusDemands.AsNoTracking()
			.Where(d => d.BlockStart >= start && d.BlockStart < end)
			.ToDictionaryAsync(d => d.BlockStart);
		var scheme = Scheme;

		var liveFrom = AggregationService.FinalizedUntil(now);
		var liveTo = DemandCalculator.FloorBlock(now);
		if (liveFrom < end && liveTo > start && liveTo > liveFrom)
		{
			if (liveFrom < start) liveFrom = start;
			foreach (var (block, sum) in await LiveBlocksAsync(liveFrom, liveTo))
				rows.TryAdd(block, sum);
		}
		return Enumerable.Range(0, 96).Select(i =>
		{
			var b = start.AddMinutes(15 * i);
			return rows.TryGetValue(b, out var d)
				? new DemandBlockDto(b, AggregationService.ParseTou(d.Tou), d.LoadKw, d.SolarKw, d.NetKw, d.MetersOk, d.MetersTotal)
				: new DemandBlockDto(b, TouCalendar.Of(b, scheme), null, null, null, 0, 0);
		}).ToList();
	}

	/// <summary>[from, to) 每個區塊的全校需量，直接從原始讀值算（尚未定案的區塊）</summary>
	private async Task<Dictionary<DateTimeOffset, CampusDemand15m>> LiveBlocksAsync(DateTimeOffset from, DateTimeOffset to)
	{
		var meters = await db.Meters.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Category);
		var lo = from - DemandCalculator.Window;
		var hi = to + DemandCalculator.Window;
		var readings = await db.MeterReadings.AsNoTracking()
			.Where(r => r.SampledAt >= lo && r.SampledAt <= hi)
			.OrderBy(r => r.SampledAt)
			.Select(r => new { r.MeterId, r.SampledAt, r.KwhTotal })
			.ToListAsync();
		var result = new Dictionary<DateTimeOffset, CampusDemand15m>();
		foreach (var g in readings.GroupBy(r => r.MeterId))
		{
			var solar = meters.GetValueOrDefault(g.Key) == "solar";
			foreach (var b in DemandCalculator.Compute(g.Select(r => new KwhReading(r.SampledAt, r.KwhTotal)).ToList(), from, to))
			{
				if (!result.TryGetValue(b.BlockStart, out var d))
					result[b.BlockStart] = d = new CampusDemand15m { BlockStart = b.BlockStart, Tou = AggregationService.TouName(TouCalendar.Of(b.BlockStart, Scheme)), MetersTotal = meters.Count };
				if (solar) d.SolarKw += b.Kw;
				else d.LoadKw += b.Kw;
				d.NetKw = Math.Round(d.LoadKw - d.SolarKw, 2);
				d.MetersOk++;
			}
		}
		return result;
	}

	/// <summary>最近 N 個月（含本月）各時段最高淨需量與超約</summary>
	public async Task<List<MonthlyDemandDto>> GetMonthlyAsync(int months, DateOnly today)
	{
		if (months is < 1 or > 36) throw new ArgumentException("months 必須在 1–36 之間");
		var contract = ToContract(await GetContractAsync());
		var firstMonth = new DateOnly(today.Year, today.Month, 1).AddMonths(1 - months);
		var from = AggregationService.LocalDayStart(firstMonth);
		var rows = await db.CampusDemands.AsNoTracking()
			.Where(d => d.BlockStart >= from)
			.Select(d => new { d.BlockStart, d.NetKw, d.Tou })
			.ToListAsync();

		return rows
			.GroupBy(r => { var d = AggregationService.LocalDate(r.BlockStart); return new DateOnly(d.Year, d.Month, 1); })
			.OrderBy(g => g.Key)
			.Select(g =>
			{
				var byTou = g.GroupBy(r => AggregationService.ParseTou(r.Tou)).OrderBy(t => t.Key).Select(t =>
				{
					var max = t.MaxBy(r => r.NetKw)!;
					var cap = contract.CapacityOf(t.Key);
					return new TouMaxDto(t.Key, max.NetKw, max.BlockStart, cap, Math.Max(0, max.NetKw - cap));
				}).ToList();
				var top = g.MaxBy(r => r.NetKw)!;
				var overBlocks = g.Count(r => r.NetKw > contract.CapacityOf(AggregationService.ParseTou(r.Tou)));
				return new MonthlyDemandDto(g.Key, byTou, top.NetKw, top.BlockStart, byTou.Count == 0 ? 0 : byTou.Max(t => t.OverKw), overBlocks);
			}).ToList();
	}

	public Task<List<DemandEventDto>> GetEventsAsync(DateTimeOffset from, DateTimeOffset to) =>
		db.DemandEvents.AsNoTracking()
			.Where(e => e.OccurredAt >= from && e.OccurredAt < to)
			.OrderByDescending(e => e.OccurredAt)
			.Take(500)
			.Select(e => new DemandEventDto(e.Id, e.OccurredAt, e.BlockStart, e.Level, e.DemandKw, e.ThresholdKw, e.ContractKw, e.Action, e.Source))
			.ToListAsync();

	/// <summary>即時需量跨過的每個等級各記一筆（同區塊同等級只記一次）</summary>
	public async Task<int> RecordLiveEventsAsync(RealtimeDemandDto rt)
	{
		var contract = await GetContractAsync();
		var crossed = Thresholds(contract).Where(t => rt.NetKw >= t.ThresholdKw).ToList();
		if (rt.NetKw >= rt.CapacityKw) crossed.Add((DemandLevels.OverContract, rt.CapacityKw, "超過契約容量"));

		var inserted = 0;
		foreach (var (level, threshold, action) in crossed)
			inserted += await db.Database.ExecuteSqlInterpolatedAsync($"""
				INSERT INTO demand_events (occurred_at, block_start, level, demand_kw, threshold_kw, contract_kw, action, source)
				VALUES ({rt.At.ToUniversalTime()}, {rt.BlockStart.ToUniversalTime()}, {level}, {rt.NetKw}, {threshold}, {contract.RegularKw}, {action}, 'live')
				ON CONFLICT (block_start, level) DO NOTHING
				""");
		return inserted;
	}
}

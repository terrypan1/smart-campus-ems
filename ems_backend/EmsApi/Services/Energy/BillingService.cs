using EmsApi.Dtos;
using EmsApi.Models;
using EmsApi.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>
/// 把資料庫的彙總組成 BillCalculator 的輸入（docs/實作/03 §3.4、§5.3）：
/// - 時段度數用日彙總（才分得出夏月／非夏月），各時段＝用電電表－太陽能電表（自發自用）
/// - 未分時段（C 方案的缺口）依當天已量測的時段比例分攤；當天都沒有已量測時改用全月比例
/// - 最高需量用 campus_demand_15m 的淨需量
/// - 功率因數用當月用電電表讀值 Σkw、Σkvar 估算
/// </summary>
public class BillingService(EmsDbContext db, DemandService demand, RuntimeSettings settings)
{
	public const string SchemeName = "高壓電力 三段式（尖峰時間固定）幼兒園、大學（專）及社福團體適用";

	public async Task<TariffDto> GetTariffAsync()
	{
		var rows = await db.TariffRates.AsNoTracking()
			.Where(r => r.Scheme == TariffItems.SchemeHv3Univ)
			.OrderBy(r => r.Item).ThenByDescending(r => r.Season)
			.Select(r => new TariffRateDto(r.Season, r.Item, r.Rate, r.Source))
			.ToListAsync();
		return new TariffDto(TariffItems.SchemeHv3Univ, SchemeName, rows);
	}

	/// <param name="through">只算到這一天（含）：同期比較用；基本電費仍以整月計</param>
	public async Task<BillEstimateDto> EstimateAsync(DateOnly month, DateOnly today, DateOnly? through = null)
	{
		// 費率表只有高壓三段式（大學適用）；二段式的時段跟費率對不上，不提供試算
		if (settings.TouScheme != TouSchemes.HighVoltage3)
			throw new ArgumentException("目前時段方案是二段式，電費試算只支援高壓三段式（大學適用）費率");
		month = new DateOnly(month.Year, month.Month, 1);
		var next = month.AddMonths(1);
		var lastDay = through is { } t && t < next ? t.AddDays(1) : next;
		var contract = await demand.GetContractAsync();
		var rates = await db.TariffRates.AsNoTracking().Where(r => r.Scheme == TariffItems.SchemeHv3Univ)
			.ToDictionaryAsync(r => (r.Season, r.Item), r => r.Rate);
		decimal Rate(string season, string item) =>
			rates.TryGetValue((season, item), out var r) ? r : throw new InvalidOperationException($"電價表缺少 {season}／{item}");

		// 日彙總：用電與太陽能分開加總
		var dayRows = await (
			from s in db.MeterEnergySummaries.AsNoTracking()
			join m in db.Meters.AsNoTracking() on s.MeterId equals m.Id
			where s.PeriodType == PeriodTypes.Day && s.PeriodStart >= month && s.PeriodStart < lastDay
			group s by new { s.PeriodStart, Solar = m.Category == "solar" } into g
			select new
			{
				g.Key.PeriodStart, g.Key.Solar,
				Peak = g.Sum(s => s.PeakKwh), Half = g.Sum(s => s.HalfKwh), SatHalf = g.Sum(s => s.SatHalfKwh), Off = g.Sum(s => s.OffKwh),
				Unassigned = g.Sum(s => s.UnassignedKwh ?? 0),
			}).ToListAsync();

		var load = dayRows.Where(r => !r.Solar).ToDictionary(r => r.PeriodStart, r => new DayEnergy(r.PeriodStart, r.Peak, r.Half, r.SatHalf, r.Off));
		var loadUnassigned = dayRows.Where(r => !r.Solar).ToDictionary(r => r.PeriodStart, r => r.Unassigned);
		var solar = dayRows.Where(r => r.Solar).ToDictionary(r => r.PeriodStart, r => new DayEnergy(r.PeriodStart, r.Peak, r.Half, r.SatHalf, r.Off));
		var solarUnassigned = dayRows.Where(r => r.Solar).ToDictionary(r => r.PeriodStart, r => r.Unassigned);

		var dates = load.Keys.Order().ToList();
		var netDays = dates.Select(d => Subtract(load[d], solar.GetValueOrDefault(d))).ToList();
		var grossDays = dates.Select(d => load[d]).ToList();
		var netUnassigned = dates.Select(d => loadUnassigned[d] - solarUnassigned.GetValueOrDefault(d)).ToList();
		var grossUnassigned = dates.Select(d => loadUnassigned[d]).ToList();
		var netBilled = Allocate(netDays, netUnassigned);
		var grossBilled = Allocate(grossDays, grossUnassigned);

		// 15 分鐘需量：各時段最高（淨需量＝實際；用電需量＝沒有太陽能時）
		var from = AggregationService.LocalDayStart(month);
		var to = AggregationService.LocalDayStart(lastDay);
		var blocks = await db.CampusDemands.AsNoTracking()
			.Where(d => d.BlockStart >= from && d.BlockStart < to)
			.Select(d => new { d.BlockStart, d.Tou, d.NetKw, d.LoadKw })
			.ToListAsync();
		var netMax = blocks.GroupBy(b => b.Tou).Select(g => g.MaxBy(b => b.NetKw)!)
			.Select(b => new TouMaxDemand(AggregationService.ParseTou(b.Tou), b.NetKw, AggregationService.LocalDate(b.BlockStart))).OrderBy(m => m.Tou).ToList();
		var grossMax = blocks.GroupBy(b => b.Tou).Select(g => g.MaxBy(b => b.LoadKw)!)
			.Select(b => new TouMaxDemand(AggregationService.ParseTou(b.Tou), b.LoadKw, AggregationService.LocalDate(b.BlockStart))).ToList();

		var pf = await AvgPowerFactorAsync(from, to);
		var c = DemandService.ToContract(contract);
		var bill = BillCalculator.Compute(month, netBilled, netMax, c, Rate, pf);
		var withoutSolar = BillCalculator.Compute(month, grossBilled, grossMax, c, Rate, pf);

		return new BillEstimateDto(
			month, today < next, netDays.Count, TariffItems.SchemeHv3Univ,
			netBilled.Sum(d => d.PeakKwh), netBilled.Sum(d => d.HalfKwh), netBilled.Sum(d => d.SatHalfKwh), netBilled.Sum(d => d.OffKwh),
			netBilled.Sum(d => d.PeakKwh + d.HalfKwh + d.SatHalfKwh + d.OffKwh), netUnassigned.Sum(),
			solar.Values.Sum(d => d.PeakKwh + d.HalfKwh + d.SatHalfKwh + d.OffKwh) + solarUnassigned.Values.Sum(),
			DemandService.ToDto(contract), netMax, pf, bill, withoutSolar.Total - bill.Total);
	}

	public async Task<List<BillHistoryDto>> HistoryAsync(int months, DateOnly today)
	{
		if (months is < 1 or > 36) throw new ArgumentException("months 必須在 1–36 之間");
		var first = await db.MeterEnergySummaries.AsNoTracking().Where(s => s.PeriodType == PeriodTypes.Day).MinAsync(s => (DateOnly?)s.PeriodStart);
		var result = new List<BillHistoryDto>();
		if (first == null) return result;
		var thisMonth = new DateOnly(today.Year, today.Month, 1);
		for (var m = thisMonth.AddMonths(1 - months); m <= thisMonth; m = m.AddMonths(1))
		{
			// 資料還沒開始的月份不算（否則只有基本電費，會誤導）
			if (m.AddMonths(1) <= first.Value) continue;
			var e = await EstimateAsync(m, today);
			result.Add(new BillHistoryDto(m, e.IsPartial, e.TotalKwh, e.Bill.BasicTotal, e.Bill.PowerFactorAdjustment, e.Bill.OverageTotal,
				e.Bill.EnergyTotal, e.Bill.Total, e.Bill.TaxIncluded, e.SolarSaving));
		}
		return result;
	}

	/// <summary>月平均功率因數 ≈ Σkw ÷ √(Σkw² + Σkvar²)（讀值等間隔取樣時，Σkw、Σkvar 正比於 kWh、kvarh）</summary>
	private async Task<double?> AvgPowerFactorAsync(DateTimeOffset start, DateTimeOffset end)
	{
		var sums = await (
			from r in db.MeterReadings.AsNoTracking()
			join m in db.Meters.AsNoTracking() on r.MeterId equals m.Id
			where m.Category != "solar" && r.SampledAt >= start && r.SampledAt < end
			group r by 1 into g
			select new { Kw = g.Sum(r => (double)r.Kw), Kvar = g.Sum(r => (double)r.Kvar) }).ToListAsync();
		if (sums.Count == 0 || sums[0].Kw <= 0) return null;
		var (kw, kvar) = (sums[0].Kw, sums[0].Kvar);
		return kw / Math.Sqrt(kw * kw + kvar * kvar);
	}

	private static DayEnergy Subtract(DayEnergy a, DayEnergy? b) => b == null ? a
		: new DayEnergy(a.Date, a.PeakKwh - b.PeakKwh, a.HalfKwh - b.HalfKwh, a.SatHalfKwh - b.SatHalfKwh, a.OffKwh - b.OffKwh);

	/// <summary>未分時段度數依當天已量測的時段比例分攤；當天沒有已量測時用全月比例</summary>
	private static List<DayEnergy> Allocate(List<DayEnergy> days, List<decimal> unassigned)
	{
		var month = new DayEnergy(default, days.Sum(d => d.PeakKwh), days.Sum(d => d.HalfKwh), days.Sum(d => d.SatHalfKwh), days.Sum(d => d.OffKwh));
		return days.Select((d, i) =>
		{
			var u = unassigned[i];
			if (u == 0) return d;
			var basis = Measured(d) > 0 ? d : month;
			var sum = Measured(basis);
			if (sum <= 0) return d with { OffKwh = d.OffKwh + u };
			return new DayEnergy(d.Date,
				Share(d.PeakKwh, basis.PeakKwh), Share(d.HalfKwh, basis.HalfKwh), Share(d.SatHalfKwh, basis.SatHalfKwh), Share(d.OffKwh, basis.OffKwh));

			// 分攤後四捨五入到 0.01 度（跟電表解析度一致）
			decimal Share(decimal kwh, decimal part) => Math.Round(kwh + u * part / sum, 2, MidpointRounding.AwayFromZero);
		}).ToList();

		static decimal Measured(DayEnergy d) => d.PeakKwh + d.HalfKwh + d.SatHalfKwh + d.OffKwh;
	}
}

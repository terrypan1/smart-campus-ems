using EmsApi.Dtos;
using EmsApi.Models;
using EmsApi.Services.Settings;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>首頁摘要（docs/實作/04 §3）：只彙整 02、03 已有的資料，不新增計算規則</summary>
public class OverviewService(EmsDbContext db, DemandService demand, BillingService billing, MeterQueryService meters, CarbonService carbon, AlarmService alarms, RuntimeSettings settings)
{
	public async Task<OverviewDto> GetAsync(DateTimeOffset now)
	{
		var today = AggregationService.LocalDate(now);
		var monthStart = new DateOnly(today.Year, today.Month, 1);
		var lastYearMonth = monthStart.AddYears(-1);
		var dayStart = AggregationService.LocalDayStart(today);
		var categories = await db.Meters.AsNoTracking().ToDictionaryAsync(m => m.Id, m => m.Category);

		// ---------- 今天的需量 ----------
		var contract = await demand.GetContractAsync();
		var c = DemandService.ToContract(contract);
		// 跟需量曲線同一份資料：已定案的區塊＋定案後到現在、直接從讀值算的區塊
		var blocks = (await demand.GetDayAsync(today, now)).Where(b => b.NetKw != null).ToList();
		var max = blocks.MaxBy(b => b.NetKw);
		var events = await db.DemandEvents.AsNoTracking()
			.Where(e => e.OccurredAt >= dayStart && e.OccurredAt < dayStart.AddDays(1))
			.GroupBy(e => e.Level).Select(g => new { g.Key, Count = g.Count() })
			.ToDictionaryAsync(g => g.Key, g => g.Count);
		var todayDemand = new TodayDemandDto(max?.NetKw, max?.BlockStart, max?.Tou, contract.RegularKw,
			blocks.Count(b => b.NetKw > c.CapacityOf(b.Tou)), blocks.Count, events);

		// ---------- 用電與去年同期（頭尾累積電度相減） ----------
		var timeOfDay = now - dayStart;
		var lyDay = AggregationService.LocalDayStart(today.AddDays(-364));
		var todayEnergy = await CompareAsync(categories, dayStart, now, lyDay, lyDay + timeOfDay);
		var mStart = AggregationService.LocalDayStart(monthStart);
		var lyMStart = AggregationService.LocalDayStart(lastYearMonth);
		var lyMEnd = AggregationService.LocalDayStart(lastYearMonth.AddDays(today.Day - 1)) + timeOfDay;
		var monthEnergy = await CompareAsync(categories, mStart, now, lyMStart, lyMEnd);

		// ---------- 本月最高需量（去年同月整月） ----------
		var monthly = await demand.GetMonthlyAsync(13, today);
		var thisM = monthly.FirstOrDefault(m => m.Month == monthStart);
		var lastM = monthly.FirstOrDefault(m => m.Month == lastYearMonth);
		var monthMax = new MaxCompareDto(thisM?.MaxKw, thisM?.MaxAt, thisM?.OverKw ?? 0, lastM?.MaxKw, lastM?.MaxAt);

		// ---------- 電費試算（去年同月 1 號到同一天） ----------
		BillEstimateDto? bill = null;
		decimal? lastBill = null;
		if (settings.TouScheme == TouSchemes.HighVoltage3)
		{
			bill = await billing.EstimateAsync(monthStart, today, today);
			var lastDay = lastYearMonth.AddDays(Math.Min(today.Day, DateTime.DaysInMonth(lastYearMonth.Year, lastYearMonth.Month)) - 1);
			if (await db.MeterEnergySummaries.AnyAsync(s => s.PeriodType == PeriodTypes.Day && s.PeriodStart == lastYearMonth))
				lastBill = (await billing.EstimateAsync(lastYearMonth, today, lastDay)).Bill.Total;
		}

		// ---------- 碳排：本月淨用電 × 今年係數；去年同期 × 去年係數（docs/實作/05 §2） ----------
		var factors = await carbon.FactorsAsync();
		var f = CarbonService.Resolve(factors, today.Year);
		var lf = CarbonService.Resolve(factors, today.Year - 1);
		var carbonCmp = new CarbonCompareDto(CarbonService.Ton(monthEnergy.Current ?? 0, f.Factor),
			monthEnergy.LastYear is { } ly ? CarbonService.Ton(ly, lf.Factor) : null, f.Factor, f.FactorYear, f.Provisional);

		// ---------- 太陽能、系統狀態 ----------
		var solarToday = await SolarKwhAsync(categories, dayStart, now);
		var latest = await meters.GetLatestAsync();
		var monthRows = await db.MeterEnergySummaries.AsNoTracking()
			.Where(s => s.PeriodType == PeriodTypes.Month && s.PeriodStart == monthStart)
			.Select(s => new { s.OkHours, s.TotalHours }).ToListAsync();
		var totalHours = monthRows.Sum(r => r.TotalHours);
		var alarmSummary = await alarms.SummaryAsync();

		return new OverviewDto(now, todayDemand, todayEnergy, monthEnergy, monthMax,
			bill == null ? null : new BillCompareDto(bill.Bill.Total, lastBill, today),
			carbonCmp,
			new SolarTodayDto(solarToday, bill?.SolarKwh ?? 0, bill?.SolarSaving ?? 0),
			new SystemStatusDto(latest.Count(l => !l.Online), latest.Count, totalHours == 0 ? 0 : Math.Round((double)monthRows.Sum(r => r.OkHours) / totalHours, 4),
				alarmSummary.Unacknowledged, alarmSummary.ActiveByType.Values.Sum()));
	}

	private async Task<EnergyCompareDto> CompareAsync(Dictionary<int, string> categories, DateTimeOffset from, DateTimeOffset to, DateTimeOffset lyFrom, DateTimeOffset lyTo)
	{
		var (cur, missing) = await NetKwhAsync(categories, from, to);
		var (last, lyMissing) = await NetKwhAsync(categories, lyFrom, lyTo);
		// 一半以上的電表頭尾讀值找不到（例：期間起點斷線、歷史資料還沒開始）→ 視為無法計算
		var half = categories.Count / 2;
		return new EnergyCompareDto(missing > half ? null : cur, lyMissing > half ? null : last, from, to, lyFrom, lyTo, missing);
	}

	/// <summary>淨用電＝Σ用電電表(尾−頭) − Σ太陽能電表(尾−頭)；頭或尾找不到、或電度倒退的電表不計入</summary>
	private async Task<(decimal Kwh, int Missing)> NetKwhAsync(Dictionary<int, string> categories, DateTimeOffset from, DateTimeOffset to)
	{
		var start = await AggregationService.BoundariesAsync(db, from, CancellationToken.None);
		var end = await AggregationService.BoundariesAsync(db, to, CancellationToken.None);
		decimal net = 0;
		var missing = 0;
		foreach (var (id, cat) in categories)
		{
			if (start.GetValueOrDefault(id) is not { } a || end.GetValueOrDefault(id) is not { } b || b.Kwh < a.Kwh) { missing++; continue; }
			net += cat == "solar" ? -(b.Kwh - a.Kwh) : b.Kwh - a.Kwh;
		}
		return (Math.Round(net, 2), missing);
	}

	private async Task<decimal?> SolarKwhAsync(Dictionary<int, string> categories, DateTimeOffset from, DateTimeOffset to)
	{
		var start = await AggregationService.BoundariesAsync(db, from, CancellationToken.None);
		var end = await AggregationService.BoundariesAsync(db, to, CancellationToken.None);
		var ok = categories.Where(x => x.Value == "solar")
			.Select(x => start.GetValueOrDefault(x.Key) is { } a && end.GetValueOrDefault(x.Key) is { } b && b.Kwh >= a.Kwh ? b.Kwh - a.Kwh : (decimal?)null)
			.Where(v => v != null).ToList();
		return ok.Count == 0 ? null : ok.Sum();
	}
}

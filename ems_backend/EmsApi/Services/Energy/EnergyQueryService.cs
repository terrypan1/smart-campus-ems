using EmsApi.Dtos;
using EmsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

public class EnergyQueryService(EmsDbContext db)
{
	/// <summary>
	/// 全校或某棟，每個期間的用電（C 方案）。太陽能電表是發電，另外列，不計入用電。
	/// 某顆電表總用電算不出來（期間邊界斷線）時，該表以已量測計入，並記在 IncompleteMeters。
	/// </summary>
	public async Task<List<EnergyPeriodDto>> GetSummaryAsync(string level, DateOnly fromDate, DateOnly toDate, int? buildingId)
	{
		if (level != PeriodTypes.Day && level != PeriodTypes.Month) throw new ArgumentException("level 只能是 day 或 month");
		if (toDate < fromDate) throw new ArgumentException("to 不可早於 from");

		var rows = await (
			from s in db.MeterEnergySummaries.AsNoTracking()
			join m in db.Meters.AsNoTracking() on s.MeterId equals m.Id
			where s.PeriodType == level && s.PeriodStart >= fromDate && s.PeriodStart <= toDate && (buildingId == null || m.BuildingId == buildingId)
			select new { s, m.Category }).ToListAsync();

		return rows.GroupBy(r => r.s.PeriodStart).OrderBy(g => g.Key).Select(g =>
		{
			var load = g.Where(r => r.Category != "solar").Select(r => r.s).ToList();
			var solar = g.Where(r => r.Category == "solar").Select(r => r.s).ToList();
			var okHours = load.Sum(s => s.OkHours);
			var totalHours = load.Sum(s => s.TotalHours);
			return new EnergyPeriodDto(
				g.Key,
				load.Sum(s => s.TotalKwh ?? s.MeasuredKwh),
				load.Sum(s => s.MeasuredKwh),
				load.Sum(s => s.PeakKwh),
				load.Sum(s => s.HalfKwh),
				load.Sum(s => s.SatHalfKwh),
				load.Sum(s => s.OffKwh),
				load.Sum(s => s.UnassignedKwh ?? 0),
				totalHours == 0 ? 0 : Math.Round((double)okHours / totalHours, 4),
				load.Count(s => s.TotalKwh == null),
				load.Count,
				solar.Sum(s => s.TotalKwh ?? s.MeasuredKwh));
		}).ToList();
	}

	/// <summary>某顆電表某一天（台北）24 小時；還沒算到的小時回 pending</summary>
	public async Task<List<HourlyEnergyDto>> GetHourlyAsync(int meterId, DateOnly date)
	{
		var start = AggregationService.LocalDayStart(date);
		var end = start.AddDays(1);
		var rows = await db.MeterHourlyEnergies.AsNoTracking()
			.Where(h => h.MeterId == meterId && h.HourStart >= start && h.HourStart < end)
			.ToDictionaryAsync(h => h.HourStart);

		return Enumerable.Range(0, 24).Select(i =>
		{
			var hour = start.AddHours(i);
			return rows.TryGetValue(hour, out var h)
				? new HourlyEnergyDto(hour, i, h.Kwh, h.Status, h.Tou, h.StartAt, h.EndAt)
				: new HourlyEnergyDto(hour, i, null, "pending", "", null, null);
		}).ToList();
	}
}

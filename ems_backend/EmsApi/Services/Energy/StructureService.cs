using EmsApi.Dtos;
using EmsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>
/// 用電結構（docs/實作/05 §2.3）：依空間類型／大樓／用途／電表分組加總 meter_energy_summary。
/// 每顆電表用總用電（C 方案；期間頭尾斷線的表以已量測計），太陽能不列入、另外回 SolarKwh。
/// </summary>
public class StructureService(EmsDbContext db)
{
	public static readonly string[] GroupBys = ["spaceType", "building", "category", "meter"];

	public async Task<StructureDto> GetAsync(string level, DateOnly from, DateOnly to, string groupBy, int? buildingId)
	{
		if (level != PeriodTypes.Day && level != PeriodTypes.Month) throw new ArgumentException("level 只能是 day 或 month");
		if (!GroupBys.Contains(groupBy)) throw new ArgumentException($"groupBy 只能是 {string.Join("／", GroupBys)}");
		if (to < from) throw new ArgumentException("to 不可早於 from");
		if (level == PeriodTypes.Month) (from, to) = (new DateOnly(from.Year, from.Month, 1), new DateOnly(to.Year, to.Month, 1));

		var meters = await db.Meters.AsNoTracking().Include(m => m.Building)
			.Where(m => buildingId == null || m.BuildingId == buildingId)
			.ToListAsync();
		var cur = await KwhByMeterAsync(level, from, to);
		var last = await KwhByMeterAsync(level, from.AddYears(-1), to.AddYears(-1));
		var hasLastYear = last.Count > 0;

		var load = meters.Where(m => m.Category != "solar").ToList();
		var total = load.Sum(m => cur.GetValueOrDefault(m.Id));
		var groups = load.GroupBy(m => groupBy switch
		{
			"spaceType" => (Key: m.Building!.SpaceType, Label: m.Building.SpaceType, Space: m.Building.SpaceType, Bid: (int?)null),
			"building" => (Key: m.BuildingId.ToString(), Label: m.Building!.Name, Space: m.Building.SpaceType, Bid: (int?)m.BuildingId),
			"category" => (Key: m.Category, Label: m.Category, Space: (string?)null, Bid: (int?)null),
			_ => (Key: m.Id.ToString(), Label: m.Name, Space: m.Building!.SpaceType, Bid: (int?)m.BuildingId),
		});

		var rows = groups.Select(g =>
		{
			var kwh = g.Sum(m => cur.GetValueOrDefault(m.Id));
			return new
			{
				g.Key,
				Kwh = kwh,
				Last = hasLastYear ? g.Sum(m => last.GetValueOrDefault(m.Id)) : (decimal?)null,
				ByCategory = g.GroupBy(m => m.Category).ToDictionary(c => c.Key, c => c.Sum(m => cur.GetValueOrDefault(m.Id))),
				Count = g.Count(),
			};
		})
		.OrderByDescending(x => x.Kwh)
		.Select((x, i) => new StructureRowDto(x.Key.Key, x.Key.Label, x.Key.Space, x.Key.Bid, x.Kwh, x.Last,
			total == 0 ? 0 : Math.Round((double)(x.Kwh / total), 4), i + 1, x.ByCategory, x.Count))
		.ToList();

		return new StructureDto(level, from, to, from.AddYears(-1), to.AddYears(-1), groupBy, total,
			hasLastYear ? load.Sum(m => last.GetValueOrDefault(m.Id)) : null,
			meters.Where(m => m.Category == "solar").Sum(m => cur.GetValueOrDefault(m.Id)), rows);
	}

	private Task<Dictionary<int, decimal>> KwhByMeterAsync(string level, DateOnly from, DateOnly to) =>
		db.MeterEnergySummaries.AsNoTracking()
			.Where(s => s.PeriodType == level && s.PeriodStart >= from && s.PeriodStart <= to)
			.GroupBy(s => s.MeterId)
			.Select(g => new { g.Key, Kwh = g.Sum(s => s.TotalKwh ?? s.MeasuredKwh) })
			.ToDictionaryAsync(x => x.Key, x => x.Kwh);
}

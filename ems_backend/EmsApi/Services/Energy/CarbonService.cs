using ClosedXML.Excel;
using EmsApi.Dtos;
using EmsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>
/// 碳排（docs/實作/05 §2）：只算外購電力的範疇二、地點基準。
/// 排放＝淨用電（用電－太陽能）× 該月所屬年度的係數；太陽能減碳＝發電 × 係數。跨年度逐月套係數。
/// </summary>
public class CarbonService(EmsDbContext db)
{
	/// <summary>某年度用哪個係數：有公告用公告值；沒有就暫用最近一個較早的年度（都沒有就用最早的）</summary>
	public static FactorUseDto Resolve(IReadOnlyList<EmissionFactor> factors, int year)
	{
		if (factors.Count == 0) throw new InvalidOperationException("emission_factors 沒有資料");
		var exact = factors.FirstOrDefault(f => f.Year == year);
		if (exact != null) return new FactorUseDto(year, exact.Factor, year, exact.IsProvisional, exact.Source);
		var use = factors.Where(f => f.Year < year).MaxBy(f => f.Year) ?? factors.MinBy(f => f.Year)!;
		return new FactorUseDto(year, use.Factor, use.Year, true, use.Source);
	}

	public static decimal Ton(decimal kwh, decimal factor) => Math.Round(kwh * factor / 1000, 3);

	public Task<List<EmissionFactor>> FactorsAsync() => db.EmissionFactors.AsNoTracking().OrderBy(f => f.Year).ToListAsync();

	public async Task<List<CarbonMonthDto>> MonthlyAsync(int months, DateOnly today)
	{
		if (months is < 1 or > 36) throw new ArgumentException("months 必須在 1–36 之間");
		var factors = await FactorsAsync();
		var thisMonth = new DateOnly(today.Year, today.Month, 1);
		var first = thisMonth.AddMonths(1 - months);

		var rows = await (
			from s in db.MeterEnergySummaries.AsNoTracking()
			join m in db.Meters.AsNoTracking() on s.MeterId equals m.Id
			where s.PeriodType == PeriodTypes.Month && s.PeriodStart >= first && s.PeriodStart <= thisMonth
			group s by new { s.PeriodStart, Solar = m.Category == "solar" } into g
			select new { g.Key.PeriodStart, g.Key.Solar, Kwh = g.Sum(s => s.TotalKwh ?? s.MeasuredKwh) }).ToListAsync();

		return rows.GroupBy(r => r.PeriodStart).OrderBy(g => g.Key).Select(g =>
		{
			var load = g.Where(r => !r.Solar).Sum(r => r.Kwh);
			var solar = g.Where(r => r.Solar).Sum(r => r.Kwh);
			var f = Resolve(factors, g.Key.Year);
			return new CarbonMonthDto(g.Key, g.Key == thisMonth, load, solar, load - solar, f.Factor, f.FactorYear, f.Provisional,
				Ton(load - solar, f.Factor), Ton(solar, f.Factor));
		}).ToList();
	}

	/// <summary>今年 1/1 到今天，對去年 1/1 到同一天（用日彙總；去年同期資料不完整時標出資料起點）</summary>
	public async Task<CarbonSummaryDto> SummaryAsync(int year, DateOnly today)
	{
		var factors = await FactorsAsync();
		var from = new DateOnly(year, 1, 1);
		var to = year == today.Year ? today : new DateOnly(year, 12, 31);
		var lyFrom = from.AddYears(-1);
		var lyTo = to.AddYears(-1);

		var cur = await NetAsync(from, to);
		var last = await NetAsync(lyFrom, lyTo);
		var f = Resolve(factors, year);
		var lf = Resolve(factors, year - 1);
		return new CarbonSummaryDto(year, from, to, cur.Net, cur.Solar, Ton(cur.Net, f.Factor), Ton(cur.Solar, f.Factor), f,
			lyFrom, lyTo, last.DataFrom, last.DataFrom == null ? null : Ton(last.Net, lf.Factor), lf);
	}

	private async Task<(decimal Net, decimal Solar, DateOnly? DataFrom)> NetAsync(DateOnly start, DateOnly end)
	{
		var rows = await (
			from s in db.MeterEnergySummaries.AsNoTracking()
			join m in db.Meters.AsNoTracking() on s.MeterId equals m.Id
			where s.PeriodType == PeriodTypes.Day && s.PeriodStart >= start && s.PeriodStart <= end
			group s by m.Category == "solar" into g
			select new { Solar = g.Key, Kwh = g.Sum(s => s.TotalKwh ?? s.MeasuredKwh), First = g.Min(s => s.PeriodStart) }).ToListAsync();
		var load = rows.Where(r => !r.Solar).Sum(r => r.Kwh);
		var solar = rows.Where(r => r.Solar).Sum(r => r.Kwh);
		return (load - solar, solar, rows.Count == 0 ? null : rows.Min(r => r.First));
	}

	/// <summary>每月明細 Excel（ESG 報告用）：欄位與碳排放頁的表格一致</summary>
	public async Task<byte[]> ExportAsync(int months, DateOnly today)
	{
		var data = await MonthlyAsync(months, today);
		using var wb = new XLWorkbook();
		var ws = wb.Worksheets.Add("每月碳排");
		ws.Cell(1, 1).Value = "示範大學主校區 用電碳排（範疇二、地點基準）";
		ws.Cell(2, 1).Value = "排放＝向台電買的電（用電量－太陽能發電）× 經濟部能源署電力排碳係數 ÷ 1,000。只算向台電買的電（範疇二），不含燃料（範疇一）與其他間接排放（範疇三），不能取代正式溫室氣體盤查。歷史資料為 Demo 模擬。";
		string[] head = ["月份", "用電量（度）", "太陽能發電（度）", "向台電買的電（度）", "排碳係數（公斤 CO2e／度）", "係數年度", "暫用", "排放（公噸 CO2e）", "太陽能少排（公噸 CO2e）", "備註"];
		for (var i = 0; i < head.Length; i++) ws.Cell(4, i + 1).Value = head[i];
		var r = 5;
		foreach (var m in data)
		{
			ws.Cell(r, 1).Value = m.Month.ToString("yyyy-MM");
			ws.Cell(r, 2).Value = m.LoadKwh;
			ws.Cell(r, 3).Value = m.SolarKwh;
			ws.Cell(r, 4).Value = m.NetKwh;
			ws.Cell(r, 5).Value = m.Factor;
			ws.Cell(r, 6).Value = $"{m.FactorYear - 1911} 年度";
			ws.Cell(r, 7).Value = m.Provisional ? "是" : "";
			ws.Cell(r, 8).Value = m.EmissionTon;
			ws.Cell(r, 9).Value = m.ReductionTon;
			ws.Cell(r, 10).Value = m.IsPartial ? "到目前（月份還沒結束）" : "";
			r++;
		}
		ws.Cell(r, 1).Value = "合計";
		foreach (var c in new[] { 2, 3, 4, 8, 9 }) ws.Cell(r, c).FormulaA1 = $"SUM({ws.Cell(5, c).Address}:{ws.Cell(r - 1, c).Address})";
		ws.Range(4, 1, 4, head.Length).Style.Font.Bold = true;
		ws.Range(r, 1, r, head.Length).Style.Font.Bold = true;
		ws.Range(5, 2, r, 4).Style.NumberFormat.Format = "#,##0.00";
		ws.Range(5, 8, r, 9).Style.NumberFormat.Format = "#,##0.000";
		ws.Columns().AdjustToContents(4, r);
		using var ms = new MemoryStream();
		wb.SaveAs(ms);
		return ms.ToArray();
	}
}

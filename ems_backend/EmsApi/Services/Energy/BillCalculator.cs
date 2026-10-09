namespace EmsApi.Services.Energy;

public static class Seasons
{
	public const string Summer = "summer";
	public const string NonSummer = "nonsummer";
	public static string Of(DateOnly d) => TouCalendar.IsHighVoltageSummer(d.ToDateTime(TimeOnly.MinValue)) ? Summer : NonSummer;
}

public static class TariffItems
{
	/// <summary>高壓電力 三段式（尖峰時間固定）幼兒園、大學（專）及社福團體適用</summary>
	public const string SchemeHv3Univ = "HV3_UNIV";

	public const string BasicRegular = "basic_regular";
	public const string BasicHalf = "basic_half";
	public const string BasicSatHalf = "basic_sat_half";
	public const string BasicOff = "basic_off";
	public const string EnergyPeak = "energy_peak";
	public const string EnergyHalf = "energy_half";
	public const string EnergySatHalf = "energy_sat_half";
	public const string EnergyOff = "energy_off";

	public static string EnergyOf(Tou t) => t switch
	{
		Tou.Peak => EnergyPeak, Tou.Half => EnergyHalf, Tou.SatHalf => EnergySatHalf, _ => EnergyOff,
	};

	/// <summary>超約時各時段用的契約電價（docs/實作/03 §3.4 解讀）</summary>
	public static string BasicOf(Tou t) => t switch
	{
		Tou.Peak => BasicRegular, Tou.Half => BasicHalf, Tou.SatHalf => BasicSatHalf, _ => BasicOff,
	};
}

/// <summary>一天各時段的計費度數（已扣太陽能、已分攤未分時段）</summary>
public sealed record DayEnergy(DateOnly Date, decimal PeakKwh, decimal HalfKwh, decimal SatHalfKwh, decimal OffKwh)
{
	public decimal Of(Tou t) => t switch { Tou.Peak => PeakKwh, Tou.Half => HalfKwh, Tou.SatHalf => SatHalfKwh, _ => OffKwh };
}

/// <summary>某時段當月最高需量與發生日</summary>
public sealed record TouMaxDemand(Tou Tou, decimal Kw, DateOnly Date);

public sealed record Contract(decimal RegularKw, decimal HalfPeakKw, decimal SatHalfKw, decimal OffPeakKw)
{
	/// <summary>各時段的累計契約容量（詳細電價表第五章七(一)2）：尖峰＝經常；半尖峰＝經常＋半尖峰；週六半尖峰再加週六；離峰＝全部</summary>
	public decimal CapacityOf(Tou t) => t switch
	{
		Tou.Peak => RegularKw,
		Tou.Half => RegularKw + HalfPeakKw,
		Tou.SatHalf => RegularKw + HalfPeakKw + SatHalfKw,
		_ => RegularKw + HalfPeakKw + SatHalfKw + OffPeakKw,
	};
}

public sealed record EnergyLine(string Season, Tou Tou, decimal Kwh, decimal Rate, decimal Amount);
public sealed record BasicLine(string Season, string Item, decimal Kw, decimal Rate, decimal DayFraction, decimal Amount);
public sealed record OverageLine(Tou Tou, decimal MaxDemandKw, decimal CapacityKw, decimal OverKw, decimal Within10Kw, decimal Beyond10Kw, decimal Rate, decimal Amount);

public sealed record BillResult(
	int SummerDays, int NonSummerDays,
	List<BasicLine> Basic, decimal BasicTotal,
	int? PowerFactorPct, decimal PowerFactorAdjustPct, decimal PowerFactorAdjustment,
	List<OverageLine> Overage, decimal OverageTotal,
	List<EnergyLine> Energy, decimal EnergyTotal,
	decimal Total, decimal TaxIncluded);

/// <summary>
/// 高壓三段式電費（依據：data/台電電價/詳細電價表 第五章五～八）。純函式，方便手算對照。
/// 電價已內含 5% 營業稅（八(一)），TaxIncluded 只是從總額拆出來顯示。
/// </summary>
public static class BillCalculator
{
	public static BillResult Compute(DateOnly month, IReadOnlyList<DayEnergy> days, IReadOnlyList<TouMaxDemand> maxDemands,
		Contract c, Func<string, string, decimal> rate, double? avgPowerFactor)
	{
		var daysInMonth = DateTime.DaysInMonth(month.Year, month.Month);
		var summerDays = Enumerable.Range(1, daysInMonth).Count(d => Seasons.Of(new DateOnly(month.Year, month.Month, d)) == Seasons.Summer);
		var nonSummerDays = daysInMonth - summerDays;

		// 基本電費（五(一)2）：夏月／非夏月跨月時依日數比例拆分（§3.4 解讀）
		var basic = new List<BasicLine>();
		foreach (var (season, n) in new[] { (Seasons.Summer, summerDays), (Seasons.NonSummer, nonSummerDays) })
		{
			if (n == 0) continue;
			var frac = (decimal)n / daysInMonth;
			var satOff = Math.Max(0, c.SatHalfKw + c.OffPeakKw - (c.RegularKw + c.HalfPeakKw) * 0.5m);
			basic.Add(Line(season, TariffItems.BasicRegular, c.RegularKw));
			if (c.HalfPeakKw > 0) basic.Add(Line(season, TariffItems.BasicHalf, c.HalfPeakKw));
			if (satOff > 0) basic.Add(Line(season, TariffItems.BasicSatHalf, satOff));

			BasicLine Line(string s, string item, decimal kw)
			{
				var r = rate(s, item);
				return new BasicLine(s, item, kw, r, frac, Round(kw * r * frac));
			}
		}
		var basicTotal = basic.Sum(b => b.Amount);

		// 功率因數（六）：以 80% 為準，每 1% 增減 0.1%，95% 以上不再扣減；套在契約內的基本電費（§3.4 解讀）
		int? pfPct = avgPowerFactor == null ? null : (int)Math.Round(avgPowerFactor.Value * 100, MidpointRounding.AwayFromZero);
		var pfAdjustPct = pfPct == null ? 0m : pfPct < 80 ? (80 - pfPct.Value) * 0.1m : -(Math.Min(pfPct.Value, 95) - 80) * 0.1m;
		var pfAdjustment = Round(basicTotal * pfAdjustPct / 100);

		// 超約（七）：各時段的累計契約容量；超出部分不重複計算；10% 以下 2 倍、超過 3 倍
		var overage = new List<OverageLine>();
		decimal previousMax = 0;
		foreach (var tou in new[] { Tou.Peak, Tou.Half, Tou.SatHalf, Tou.Off })
		{
			var md = maxDemands.FirstOrDefault(m => m.Tou == tou);
			var cap = c.CapacityOf(tou);
			if (md == null || cap <= 0) continue;
			var raw = Math.Max(0, md.Kw - cap);
			var over = Math.Max(0, raw - previousMax);
			previousMax = Math.Max(previousMax, raw);
			if (over <= 0) continue;
			var within = Math.Min(over, cap * 0.1m);
			var beyond = over - within;
			var r = rate(Seasons.Of(md.Date), TariffItems.BasicOf(tou));
			overage.Add(new OverageLine(tou, md.Kw, cap, over, within, beyond, r, Round(within * r * 2 + beyond * r * 3)));
		}
		var overageTotal = overage.Sum(o => o.Amount);

		// 流動電費：依當天的季節與時段計價
		var energy = days
			.SelectMany(d => new[] { Tou.Peak, Tou.Half, Tou.SatHalf, Tou.Off }.Select(t => (Season: Seasons.Of(d.Date), Tou: t, Kwh: d.Of(t))))
			.Where(x => x.Kwh != 0)
			.GroupBy(x => (x.Season, x.Tou))
			.Select(g =>
			{
				var kwh = g.Sum(x => x.Kwh);
				var r = rate(g.Key.Season, TariffItems.EnergyOf(g.Key.Tou));
				return new EnergyLine(g.Key.Season, g.Key.Tou, kwh, r, Round(kwh * r));
			})
			.OrderBy(e => e.Season).ThenBy(e => e.Tou)
			.ToList();
		var energyTotal = energy.Sum(e => e.Amount);

		var total = basicTotal + pfAdjustment + overageTotal + energyTotal;
		return new BillResult(summerDays, nonSummerDays, basic, basicTotal, pfPct, pfAdjustPct, pfAdjustment,
			overage, overageTotal, energy, energyTotal, total, TaxOf(total));
	}

	/// <summary>電價已內含 5% 營業稅：營業稅＝總額 ÷ 1.05 × 0.05</summary>
	public static decimal TaxOf(decimal total) => Round(total / 1.05m * 0.05m);

	private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}

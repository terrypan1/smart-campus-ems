using EmsApi.Services.Energy;

namespace EmsApi.Tests;

/// <summary>docs/實作/03 §9 的驗證案例</summary>
public class DemandBillingTests
{
	// ---------- 離峰日（詳細電價表第五章四 註1） ----------

	[Fact]
	public void 國定假日全日離峰_國慶與春節()
	{
		// 2026-10-10 國慶（週六，平常會是週六半尖峰）、2026-02-17 春節初一（週二）
		Assert.Equal(Tou.Off, TouCalendar.OfLocal(new DateTime(2026, 10, 10, 18, 0, 0), TouSchemes.HighVoltage3));
		Assert.Equal(Tou.Off, TouCalendar.OfLocal(new DateTime(2026, 2, 17, 10, 0, 0), TouSchemes.HighVoltage3));
		// 除夕前 1 日（2/15）到初五（2/21）都是；初六（2/22 週日本來就離峰）、2/23 週一恢復
		Assert.True(TouCalendar.IsOffPeakDay(new DateOnly(2026, 2, 15)));
		Assert.True(TouCalendar.IsOffPeakDay(new DateOnly(2026, 2, 21)));
		Assert.False(TouCalendar.IsOffPeakDay(new DateOnly(2026, 2, 14)));
		Assert.Equal(Tou.Half, TouCalendar.OfLocal(new DateTime(2026, 2, 23, 10, 0, 0), TouSchemes.HighVoltage3));
	}

	[Fact]
	public void 新增的紀念日_928_1025_1225也是離峰日()
	{
		Assert.True(TouCalendar.IsOffPeakDay(new DateOnly(2026, 9, 28)));
		Assert.True(TouCalendar.IsOffPeakDay(new DateOnly(2025, 10, 25)));
		Assert.True(TouCalendar.IsOffPeakDay(new DateOnly(2025, 12, 25)));
		Assert.False(TouCalendar.IsOffPeakDay(new DateOnly(2026, 10, 9)));
	}

	// ---------- 電費 ----------

	private static readonly Dictionary<(string, string), decimal> Rates = new()
	{
		[(Seasons.Summer, TariffItems.BasicRegular)] = 223.60m, [(Seasons.NonSummer, TariffItems.BasicRegular)] = 166.90m,
		[(Seasons.Summer, TariffItems.BasicHalf)] = 166.90m, [(Seasons.NonSummer, TariffItems.BasicHalf)] = 166.90m,
		[(Seasons.Summer, TariffItems.BasicSatHalf)] = 44.70m, [(Seasons.NonSummer, TariffItems.BasicSatHalf)] = 33.30m,
		[(Seasons.Summer, TariffItems.BasicOff)] = 44.70m, [(Seasons.NonSummer, TariffItems.BasicOff)] = 33.30m,
		[(Seasons.Summer, TariffItems.EnergyPeak)] = 5.80m,
		[(Seasons.Summer, TariffItems.EnergyHalf)] = 3.63m, [(Seasons.NonSummer, TariffItems.EnergyHalf)] = 3.40m,
		[(Seasons.Summer, TariffItems.EnergySatHalf)] = 1.78m, [(Seasons.NonSummer, TariffItems.EnergySatHalf)] = 1.65m,
		[(Seasons.Summer, TariffItems.EnergyOff)] = 1.58m, [(Seasons.NonSummer, TariffItems.EnergyOff)] = 1.45m,
	};

	private static decimal Rate(string season, string item) => Rates[(season, item)];
	private static readonly DateOnly July = new(2026, 7, 1);
	private static readonly DateOnly JulyDay = new(2026, 7, 8);

	private static BillResult Bill(Contract c, TouMaxDemand[]? max = null, DayEnergy[]? days = null, double? pf = null, DateOnly? month = null) =>
		BillCalculator.Compute(month ?? July, days ?? [], max ?? [], c, Rate, pf);

	[Fact]
	public void 基本電費_只有經常契約()
	{
		var b = Bill(new Contract(1900, 0, 0, 0));
		Assert.Equal(1900 * 223.60m, b.BasicTotal);
		Assert.Single(b.Basic);
	}

	[Fact]
	public void 基本電費_週六離峰契約扣除項為負按0_為正才計()
	{
		// (300 + 100) − (1000 + 0) × 0.5 = −100 → 0
		Assert.Equal(1000 * 223.60m, Bill(new Contract(1000, 0, 300, 100)).BasicTotal);
		// (600 + 100) − 500 = 200 kW × 44.70
		Assert.Equal(1000 * 223.60m + 200 * 44.70m, Bill(new Contract(1000, 0, 600, 100)).BasicTotal);
	}

	[Fact]
	public void 基本電費_跨夏月依日數比例拆分()
	{
		// 2026-05：1–15 非夏月、16–31 夏月
		var b = Bill(new Contract(1000, 0, 0, 0), month: new DateOnly(2026, 5, 1));
		Assert.Equal(16, b.SummerDays);
		Assert.Equal(15, b.NonSummerDays);
		Assert.Equal(Math.Round(1000 * 223.60m * 16 / 31, 2) + Math.Round(1000 * 166.90m * 15 / 31, 2), b.BasicTotal);
	}

	[Fact]
	public void 超約_10percent以內2倍()
	{
		var b = Bill(new Contract(1000, 0, 0, 0), [new TouMaxDemand(Tou.Peak, 1050, JulyDay)]);
		var o = Assert.Single(b.Overage);
		Assert.Equal(50, o.OverKw);
		Assert.Equal(50 * 223.60m * 2, o.Amount);
	}

	[Fact]
	public void 超約_超過10percent的部分3倍()
	{
		var b = Bill(new Contract(1000, 0, 0, 0), [new TouMaxDemand(Tou.Peak, 1150, JulyDay)]);
		var o = Assert.Single(b.Overage);
		Assert.Equal(100, o.Within10Kw);
		Assert.Equal(50, o.Beyond10Kw);
		Assert.Equal(100 * 223.60m * 2 + 50 * 223.60m * 3, o.Amount);
	}

	[Fact]
	public void 超約_不重複計算_後面時段扣掉前面超出的較大者()
	{
		// 尖峰超 80 kW、半尖峰超 100 kW → 半尖峰只計 20 kW；週六只超 60 kW → 不計
		var b = Bill(new Contract(1000, 0, 0, 0),
			[new TouMaxDemand(Tou.Peak, 1080, JulyDay), new TouMaxDemand(Tou.Half, 1100, JulyDay), new TouMaxDemand(Tou.SatHalf, 1060, JulyDay)]);
		Assert.Equal(2, b.Overage.Count);
		Assert.Equal(80, b.Overage[0].OverKw);
		Assert.Equal(Tou.Half, b.Overage[1].Tou);
		Assert.Equal(20, b.Overage[1].OverKw);
		Assert.Equal(20 * 166.90m * 2, b.Overage[1].Amount);
	}

	[Theory]
	[InlineData(0.75, 0.5)]
	[InlineData(0.90, -1.0)]
	[InlineData(0.98, -1.5)]
	[InlineData(0.80, 0.0)]
	public void 功率因數調整(double pf, double expectedPct)
	{
		var b = Bill(new Contract(1000, 0, 0, 0), pf: pf);
		Assert.Equal((decimal)expectedPct, b.PowerFactorAdjustPct);
		Assert.Equal(Math.Round(1000 * 223.60m * (decimal)expectedPct / 100, 2), b.PowerFactorAdjustment);
	}

	[Fact]
	public void 營業稅從總額拆出_總額除以105乘以005()
	{
		Assert.Equal(5m, BillCalculator.TaxOf(105m));
		Assert.Equal(Math.Round(424840m / 1.05m * 0.05m, 2), BillCalculator.TaxOf(424840m));
	}

	[Fact]
	public void 流動電費依季節與時段計價_總額是各項相加()
	{
		var days = new[]
		{
			new DayEnergy(new DateOnly(2026, 7, 8), 100, 200, 0, 300),
			new DayEnergy(new DateOnly(2026, 7, 11), 0, 0, 50, 10),
		};
		var b = Bill(new Contract(1000, 0, 0, 0), days: days, pf: 0.8);
		Assert.Equal(100 * 5.80m + 200 * 3.63m + 50 * 1.78m + 310 * 1.58m, b.EnergyTotal);
		Assert.Equal(b.BasicTotal + b.EnergyTotal, b.Total);
		Assert.Equal(BillCalculator.TaxOf(b.Total), b.TaxIncluded);
	}

	// ---------- 15 分鐘需量 ----------

	private static readonly DateTimeOffset Block0 = new(2026, 7, 8, 10, 0, 0, TimeSpan.FromHours(8));

	[Fact]
	public void 十五分鐘需量是區塊用電乘以4()
	{
		// 每分鐘一筆、每分鐘 0.5 度 → 區塊 7.5 度 → 30 kW
		var readings = Enumerable.Range(0, 31).Select(i => new KwhReading(Block0.AddMinutes(i).AddSeconds(3), 1000m + i * 0.5m)).ToList();
		var blocks = DemandCalculator.Compute(readings, Block0, Block0.AddMinutes(30));
		Assert.Equal(2, blocks.Count);
		Assert.All(blocks, b => Assert.Equal(30m, b.Kw));
	}

	[Fact]
	public void 區塊邊界5分鐘內沒有讀值就不算()
	{
		var readings = new List<KwhReading> { new(Block0, 0m), new(Block0.AddMinutes(15), 10m), new(Block0.AddMinutes(36), 20m) };
		var blocks = DemandCalculator.Compute(readings, Block0, Block0.AddMinutes(30));
		Assert.Equal(Block0, Assert.Single(blocks).BlockStart);
	}

	[Fact]
	public void 目前區塊即時需量_已用電度除以已過時間()
	{
		// 區塊起點到 06 分用了 3 度 → 3 ÷ 0.1 h = 30 kW
		var readings = new List<KwhReading> { new(Block0, 100m), new(Block0.AddMinutes(6), 103m) };
		Assert.Equal(30m, DemandCalculator.CurrentBlockKw(readings, Block0.AddMinutes(6).AddSeconds(10)));
		// 不到 2 分鐘不穩定 → null
		Assert.Null(DemandCalculator.CurrentBlockKw([new(Block0, 100m), new(Block0.AddMinutes(1), 100.5m)], Block0.AddMinutes(1)));
	}

	// ---------- 需量燈號（docs/實作/04 §8 #1） ----------

	[Theory]
	[InlineData(1000, null)]
	[InlineData(1330, "warn")]
	[InlineData(1615, "shed_aircon")]
	[InlineData(1710, "shed_fan")]
	[InlineData(1805, "shed_light")]
	[InlineData(1900, "over_contract")]
	[InlineData(2100, "over_contract")]
	public void 需量等級依經常契約百分比(int kw, string? expected)
	{
		var c = new EmsApi.Models.ContractSetting { RegularKw = 1900, WarnPct = 70, ShedAirconPct = 85, ShedFanPct = 90, ShedLightPct = 95 };
		Assert.Equal(expected, DemandService.LevelOf(kw, 1900, c));
	}
}

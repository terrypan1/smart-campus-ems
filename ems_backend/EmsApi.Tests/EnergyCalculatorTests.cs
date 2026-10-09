using EmsApi.Services.Energy;

namespace EmsApi.Tests;

public class EnergyCalculatorTests
{
	private static readonly TimeSpan Tw = TimeSpan.FromHours(8);
	private static DateTimeOffset T(int h, int m, int s = 0, int day = 5) => new(2026, 10, day, h, m, s, Tw);
	private static KwhReading R(int h, int m, int s, decimal kwh) => new(T(h, m, s), kwh);

	// ---------- 小時邊界規則 ----------

	[Fact]
	public void 邊界規則範例_08時可算_09時找不到10點邊界不可算()
	{
		// 08:00、09:00 附近都有讀值；10:00 ±20 分鐘內沒有
		var readings = new[] { R(7, 59, 50, 100m), R(9, 0, 1, 130m), R(9, 10, 1, 133m), R(9, 39, 0, 140m) };
		var hours = HourlyEnergyCalculator.Compute(readings, T(8, 0), T(10, 0));

		Assert.Equal(HourStatus.Ok, hours[0].Status);
		Assert.Equal(30m, hours[0].Kwh);
		Assert.Equal(HourStatus.NoEnd, hours[1].Status);
		Assert.Null(hours[1].Kwh);
	}

	[Fact]
	public void 取最接近的讀值_090001優先於091001()
	{
		var readings = new[] { R(9, 0, 1, 130m), R(9, 10, 1, 133m) };
		Assert.Equal(T(9, 0, 1), BoundaryPicker.Pick(readings, T(9, 0))!.Value.At);
	}

	[Fact]
	public void 只剩091001時仍可當邊界_在20分鐘內()
	{
		var readings = new[] { R(9, 10, 1, 133m) };
		Assert.Equal(133m, BoundaryPicker.Pick(readings, T(9, 0))!.Value.Kwh);
	}

	[Fact]
	public void 超過20分鐘不可當邊界_剛好20分鐘可以()
	{
		Assert.Null(BoundaryPicker.Pick([R(9, 20, 1, 1m)], T(9, 0)));
		Assert.Null(BoundaryPicker.Pick([R(8, 39, 59, 1m)], T(9, 0)));
		Assert.NotNull(BoundaryPicker.Pick([R(9, 20, 0, 1m)], T(9, 0)));
		Assert.NotNull(BoundaryPicker.Pick([R(8, 40, 0, 1m)], T(9, 0)));
	}

	[Fact]
	public void 距離相同取較早的一筆()
	{
		var readings = new[] { R(8, 55, 0, 10m), R(9, 5, 0, 20m) };
		Assert.Equal(10m, BoundaryPicker.Pick(readings, T(9, 0))!.Value.Kwh);
	}

	// ---------- 小時計算 ----------

	[Fact]
	public void 連續小時相加等於頭尾相減()
	{
		var readings = Enumerable.Range(0, 25).Select(i => new KwhReading(T(0, 0).AddHours(i).AddSeconds(i % 7), 1000m + i * i * 1.5m)).ToArray();
		var hours = HourlyEnergyCalculator.Compute(readings, T(0, 0), T(0, 0).AddHours(24));

		Assert.All(hours, h => Assert.Equal(HourStatus.Ok, h.Status));
		Assert.Equal(readings[^1].Kwh - readings[0].Kwh, hours.Sum(h => h.Kwh!.Value));
	}

	[Fact]
	public void 電度倒退標為counter_decrease()
	{
		var readings = new[] { R(9, 0, 0, 500m), R(10, 0, 0, 10m) };
		var h = HourlyEnergyCalculator.Compute(readings, T(9, 0), T(10, 0)).Single();
		Assert.Equal(HourStatus.CounterDecrease, h.Status);
		Assert.Null(h.Kwh);
	}

	[Fact]
	public void 起點終點都沒有標為no_both_只缺起點標no_start()
	{
		var readings = new[] { R(11, 0, 0, 50m) };
		var hours = HourlyEnergyCalculator.Compute(readings, T(9, 0), T(11, 0));
		Assert.Equal(HourStatus.NoBoth, hours[0].Status);
		Assert.Equal(HourStatus.NoStart, hours[1].Status);
	}

	// ---------- 期間（C 方案） ----------

	[Fact]
	public void 中間斷線_總用電不變_差額進未分時段()
	{
		// 00:00–06:00，每小時 10 度；02:00、03:00 的讀值遺失 → 1–3 時不可計算
		var readings = new[] { 0, 1, 4, 5, 6 }.Select(h => new KwhReading(T(h, 0), 100m + h * 10)).ToArray();
		var hours = HourlyEnergyCalculator.Compute(readings, T(0, 0), T(6, 0));
		var p = PeriodEnergyCalculator.Compute(readings[0], readings[^1], hours, totalHours: 6, _ => Tou.Off);

		Assert.Equal(60m, p.TotalKwh);
		Assert.Equal(30m, p.MeasuredKwh); // 0、4、5 時
		Assert.Equal(30m, p.UnassignedKwh);
		Assert.Equal(3, p.OkHours);
	}

	[Fact]
	public void 期間邊界斷線_總用電為null_只有已量測()
	{
		var readings = new[] { 1, 2, 3 }.Select(h => new KwhReading(T(h, 0), 100m + h * 10)).ToArray();
		var hours = HourlyEnergyCalculator.Compute(readings, T(0, 0), T(3, 0));
		var p = PeriodEnergyCalculator.Compute(BoundaryPicker.Pick(readings, T(0, 0)), BoundaryPicker.Pick(readings, T(3, 0)), hours, 3, _ => Tou.Off);

		Assert.Null(p.TotalKwh);
		Assert.Null(p.UnassignedKwh);
		Assert.Equal(20m, p.MeasuredKwh);
	}

	[Fact]
	public void 已量測依時段分開加總()
	{
		var readings = new[] { 0, 1, 2 }.Select(h => new KwhReading(T(h, 0), h * 10m)).ToArray();
		var hours = HourlyEnergyCalculator.Compute(readings, T(0, 0), T(2, 0));
		var p = PeriodEnergyCalculator.Compute(readings[0], readings[^1], hours, 2, h => h == T(0, 0) ? Tou.Peak : Tou.Half);
		Assert.Equal(10m, p.PeakKwh);
		Assert.Equal(10m, p.HalfKwh);
		Assert.Equal(0m, p.OffKwh);
	}

	// ---------- 時段（台電 112/1/1 新時間帶） ----------

	private static Tou Hv3(int month, int day, int hour) => TouCalendar.OfLocal(new DateTime(2026, month, day, hour, 0, 0), TouSchemes.HighVoltage3);
	private static Tou Hv2(int month, int day, int hour) => TouCalendar.OfLocal(new DateTime(2026, month, day, hour, 0, 0), TouSchemes.HighVoltage2);

	[Fact]
	public void 高壓夏月是5月16日到10月15日()
	{
		Assert.False(TouCalendar.IsHighVoltageSummer(new DateTime(2026, 5, 15)));
		Assert.True(TouCalendar.IsHighVoltageSummer(new DateTime(2026, 5, 16)));
		Assert.True(TouCalendar.IsHighVoltageSummer(new DateTime(2026, 10, 15)));
		Assert.False(TouCalendar.IsHighVoltageSummer(new DateTime(2026, 10, 16)));
	}

	[Fact]
	public void 三段式_夏月平日()
	{
		// 2026-07-08 週三
		Assert.Equal(Tou.Off, Hv3(7, 8, 8));
		Assert.Equal(Tou.Half, Hv3(7, 8, 9));
		Assert.Equal(Tou.Half, Hv3(7, 8, 15));
		Assert.Equal(Tou.Peak, Hv3(7, 8, 16));
		Assert.Equal(Tou.Peak, Hv3(7, 8, 21));
		Assert.Equal(Tou.Half, Hv3(7, 8, 22));
		Assert.Equal(Tou.Half, Hv3(7, 8, 23));
	}

	[Fact]
	public void 三段式_非夏月平日沒有尖峰()
	{
		// 2026-11-04 週三：11 月平日的「平日半尖峰」
		Assert.Equal(Tou.Off, Hv3(11, 4, 5));
		Assert.Equal(Tou.Half, Hv3(11, 4, 6));
		Assert.Equal(Tou.Off, Hv3(11, 4, 12));
		Assert.Equal(Tou.Half, Hv3(11, 4, 14));
		Assert.Equal(Tou.Half, Hv3(11, 4, 18));
	}

	[Fact]
	public void 週六半尖峰_週日全日離峰()
	{
		// 2026-07-11 週六、07-12 週日；2026-11-07 週六
		Assert.Equal(Tou.Off, Hv3(7, 11, 8));
		Assert.Equal(Tou.SatHalf, Hv3(7, 11, 18));
		Assert.Equal(Tou.SatHalf, Hv3(11, 7, 7));
		Assert.Equal(Tou.Off, Hv3(11, 7, 12));
		Assert.Equal(Tou.Off, Hv3(7, 12, 18));
	}

	[Fact]
	public void 二段式_平日有電價時段全是尖峰()
	{
		Assert.Equal(Tou.Peak, Hv2(7, 8, 10));
		Assert.Equal(Tou.Off, Hv2(7, 8, 8));
		Assert.Equal(Tou.Peak, Hv2(11, 4, 7));
		Assert.Equal(Tou.Off, Hv2(11, 4, 12));
		Assert.Equal(Tou.SatHalf, Hv2(11, 7, 7));
	}

	[Fact]
	public void UTC小時起點轉台北時間判斷時段()
	{
		// 2026-07-08 08:00 UTC = 16:00 台北 → 尖峰
		Assert.Equal(Tou.Peak, TouCalendar.Of(new DateTimeOffset(2026, 7, 8, 8, 0, 0, TimeSpan.Zero), TouSchemes.HighVoltage3));
	}
}

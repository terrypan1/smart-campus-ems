namespace EmsSimulator;

/// <summary>
/// 依時間算出一顆電表「應該」的負載（kW），不含雜訊。
/// 形狀來源：校園電表 2025/11 的平日／週末平均曲線，以平日尖峰 371 kW 正規化。
/// </summary>
public static class LoadModel
{
	// 校區平日、週末逐時平均 kW ÷ 371
	private static readonly double[] Weekday =
	[
		0.35, 0.33, 0.30, 0.30, 0.30, 0.33, 0.41, 0.57, 0.76, 0.83, 0.95, 0.96,
		1.00, 0.98, 0.92, 0.90, 0.91, 0.87, 0.85, 0.73, 0.58, 0.53, 0.45, 0.43,
	];
	private static readonly double[] Weekend =
	[
		0.36, 0.34, 0.32, 0.31, 0.32, 0.33, 0.36, 0.47, 0.48, 0.50, 0.54, 0.54,
		0.58, 0.57, 0.61, 0.58, 0.57, 0.54, 0.54, 0.54, 0.52, 0.50, 0.50, 0.42,
	];
	// 宿舍：白天人少、晚上到半夜最高
	private static readonly double[] Dormitory =
	[
		0.70, 0.60, 0.50, 0.45, 0.42, 0.42, 0.50, 0.60, 0.50, 0.40, 0.38, 0.38,
		0.42, 0.40, 0.38, 0.40, 0.45, 0.60, 0.80, 0.90, 0.95, 1.00, 0.95, 0.85,
	];

	/// <summary>localTime 為台北時間</summary>
	public static double TargetKw(MeterDef meter, string spaceType, DateTime localTime)
	{
		var h = localTime.Hour + localTime.Minute / 60.0;
		var weekend = localTime.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

		if (meter.Category == "solar") return meter.RatedKw * Solar(localTime);

		double shape = spaceType switch
		{
			"dormitory" => Interp(Dormitory, h),
			"library" => Interp(weekend ? Weekend : Weekday, h) * (h is >= 8 and < 22 ? 1.05 : 0.6),
			_ => Interp(weekend ? Weekend : Weekday, h),
		};

		return meter.Category switch
		{
			// 空調：跟著季節走，夜間幾乎不開（宿舍例外，晚上才是尖峰）
			"aircon" => meter.RatedKw * AirconSeason(localTime.Month) * Math.Max(0.04, (shape - 0.3) / 0.7),
			// 動力：有基載（實驗室設備、泵浦）
			"power" => meter.RatedKw * (0.35 + 0.55 * shape),
			// 照明插座、其他：跟著校區曲線，保留待機基載
			_ => meter.RatedKw * Math.Max(0.2, shape),
		};
	}

	/// <summary>空調季節係數：夏月 6–9 月全開，5、10 月半開，其餘低</summary>
	public static double AirconSeason(int month) => month switch
	{
		>= 6 and <= 9 => 1.0,
		5 or 10 => 0.6,
		4 or 11 => 0.35,
		_ => 0.2,
	};

	/// <summary>太陽能：06:00–18:00 半弦波，夏季較強；雲量每天固定（以日期為種子），同一天重算結果一致</summary>
	public static double Solar(DateTime localTime)
	{
		var h = localTime.Hour + localTime.Minute / 60.0;
		if (h <= 6 || h >= 18) return 0;
		var season = 0.75 + 0.25 * Math.Cos((localTime.DayOfYear - 172) / 365.0 * 2 * Math.PI);
		var cloud = 0.55 + 0.45 * new Random(localTime.Year * 1000 + localTime.DayOfYear).NextDouble();
		return Math.Sin(Math.PI * (h - 6) / 12) * season * cloud * 0.85;
	}

	private static double Interp(double[] hourly, double h)
	{
		var i = (int)Math.Floor(h) % 24;
		var t = h - Math.Floor(h);
		return hourly[i] * (1 - t) + hourly[(i + 1) % 24] * t;
	}
}

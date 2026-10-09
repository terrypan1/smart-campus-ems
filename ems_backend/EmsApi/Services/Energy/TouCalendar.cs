namespace EmsApi.Services.Energy;

/// <summary>時段：週六半尖峰的流動電費與超約計算都跟平日半尖峰不同，所以獨立一個值</summary>
public enum Tou { Peak, Half, SatHalf, Off }

public static class TouSchemes
{
	/// <summary>高壓三段式（尖峰時間固定）</summary>
	public const string HighVoltage3 = "HV3";
	/// <summary>高壓二段式</summary>
	public const string HighVoltage2 = "HV2";
}

/// <summary>
/// 台電時間電價時段。依據（data/台電電價/）：
/// - 時段與高壓夏月 5/16–10/15：〈112 年時間電價新時間帶及高壓以上擴大夏月期間說明〉、詳細電價表第五章四
/// - 離峰日（全日離峰）：詳細電價表第五章四 註1
/// 所有切換點都在整點，所以每個小時剛好屬於一個時段。
/// </summary>
public static class TouCalendar
{
	public static readonly TimeSpan TaipeiOffset = TimeSpan.FromHours(8);

	public static bool IsHighVoltageSummer(DateTime local)
	{
		var md = local.Month * 100 + local.Day;
		return md >= 516 && md <= 1015;
	}

	/// <summary>國曆固定日期的離峰日（月×100＋日）：開國紀念日、和平紀念日、兒童節、勞動節、教師節、國慶日、臺灣光復紀念日、行憲紀念日</summary>
	private static readonly HashSet<int> FixedOffPeakDays = [101, 228, 404, 501, 928, 1010, 1025, 1225];

	/// <summary>
	/// 內建的農曆與節氣離峰日：春節（農曆除夕前 1 日至正月初五）、民族掃墓節、端午節、中秋節（2025–2026，歷史資料涵蓋的年份）。
	/// 這份清單是 off_peak_days 資料表的初始值；之後的年份在系統設定新增（每年依行政院人事行政總處公告的日期）。
	/// </summary>
	public static readonly IReadOnlyList<(DateOnly Date, string Name)> BuiltInLunarDays =
	[
		// 2025：除夕 1/28 → 1/27–2/2；清明 4/4；端午 5/31；中秋 10/6
		.. Range(new DateOnly(2025, 1, 27), new DateOnly(2025, 2, 2)).Select(d => (d, "春節")),
		(new DateOnly(2025, 4, 4), "民族掃墓節"), (new DateOnly(2025, 5, 31), "端午節"), (new DateOnly(2025, 10, 6), "中秋節"),
		// 2026：除夕 2/16 → 2/15–2/21；清明 4/5；端午 6/19；中秋 9/25
		.. Range(new DateOnly(2026, 2, 15), new DateOnly(2026, 2, 21)).Select(d => (d, "春節")),
		(new DateOnly(2026, 4, 5), "民族掃墓節"), (new DateOnly(2026, 6, 19), "端午節"), (new DateOnly(2026, 9, 25), "中秋節"),
	];

	/// <summary>目前使用的農曆／補充離峰日：啟動時與系統設定修改後由 off_peak_days 載入（SetOffPeakDays）</summary>
	private static volatile HashSet<DateOnly> _lunarOffPeakDays = [.. BuiltInLunarDays.Select(x => x.Date)];

	public static void SetOffPeakDays(IEnumerable<DateOnly> days) => _lunarOffPeakDays = [.. days];

	/// <summary>國曆固定日期離峰日的名稱（顯示用）</summary>
	public static readonly IReadOnlyList<(int MonthDay, string Name)> FixedOffPeakNames =
	[
		(101, "開國紀念日"), (228, "和平紀念日"), (404, "兒童節"), (501, "勞動節"),
		(928, "教師節"), (1010, "國慶日"), (1025, "臺灣光復紀念日"), (1225, "行憲紀念日"),
	];

	public static bool IsOffPeakDay(DateOnly date) =>
		FixedOffPeakDays.Contains(date.Month * 100 + date.Day) || _lunarOffPeakDays.Contains(date);

	/// <summary>hourStartUtc：小時起點（UTC）</summary>
	public static Tou Of(DateTimeOffset hourStartUtc, string scheme) => OfLocal(hourStartUtc.ToOffset(TaipeiOffset).DateTime, scheme);

	public static Tou OfLocal(DateTime local, string scheme)
	{
		var h = local.Hour;
		if (local.DayOfWeek == DayOfWeek.Sunday || IsOffPeakDay(DateOnly.FromDateTime(local))) return Tou.Off;

		var summer = IsHighVoltageSummer(local);
		// 夏月：09–24 有電價時段；非夏月：06–11、14–24 有電價時段（兩種方案、平日週六都一樣）
		var priced = summer ? h >= 9 : (h >= 6 && h < 11) || h >= 14;
		if (!priced) return Tou.Off;

		if (local.DayOfWeek == DayOfWeek.Saturday) return Tou.SatHalf;

		return scheme switch
		{
			// 二段式平日：有電價時段全部是尖峰
			TouSchemes.HighVoltage2 => Tou.Peak,
			// 三段式平日：只有夏月 16–22 是尖峰，其餘半尖峰（非夏月沒有尖峰）
			_ => summer && h >= 16 && h < 22 ? Tou.Peak : Tou.Half,
		};
	}

	private static IEnumerable<DateOnly> Range(DateOnly from, DateOnly to)
	{
		for (var d = from; d <= to; d = d.AddDays(1)) yield return d;
	}
}

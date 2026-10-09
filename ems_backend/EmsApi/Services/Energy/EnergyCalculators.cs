namespace EmsApi.Services.Energy;

/// <summary>一筆累積電度讀值</summary>
public readonly record struct KwhReading(DateTimeOffset At, decimal Kwh);

public static class HourStatus
{
	public const string Ok = "ok";
	public const string NoStart = "no_start";
	public const string NoEnd = "no_end";
	public const string NoBoth = "no_both";
	/// <summary>終點讀值小於起點（換表、歸零或資料錯誤）</summary>
	public const string CounterDecrease = "counter_decrease";
}

public sealed record HourResult(DateTimeOffset HourStart, decimal? Kwh, string Status, KwhReading? Start, KwhReading? End);

public sealed record PeriodResult(
	decimal? TotalKwh, decimal MeasuredKwh, decimal PeakKwh, decimal HalfKwh, decimal SatHalfKwh, decimal OffKwh, decimal? UnassignedKwh, int OkHours, int TotalHours);

/// <summary>
/// 小時邊界規則：邊界時間 ±20 分鐘內取「時間最接近」的一筆累積電度，距離相同取較早的一筆；找不到就是沒有邊界。
/// 「08:00 這小時的終點不允許用 09:10:01」解讀為：有更接近的 09:00:01 時必須取最接近的（見 docs/實作/02 §2.1）。
/// </summary>
public static class BoundaryPicker
{
	public static readonly TimeSpan Window = TimeSpan.FromMinutes(20);

	/// <param name="sorted">依時間遞增排序的讀值</param>
	public static KwhReading? Pick(IReadOnlyList<KwhReading> sorted, DateTimeOffset boundary) => Pick(sorted, boundary, Window);

	/// <param name="window">容許時間窗（小時邊界用 ±20 分鐘；15 分鐘需量區塊用 ±5 分鐘）</param>
	public static KwhReading? Pick(IReadOnlyList<KwhReading> sorted, DateTimeOffset boundary, TimeSpan window)
	{
		var lo = boundary - window;
		var hi = boundary + window;
		var i = LowerBound(sorted, lo);
		KwhReading? best = null;
		var bestDiff = TimeSpan.MaxValue;
		for (; i < sorted.Count && sorted[i].At <= hi; i++)
		{
			var diff = (sorted[i].At - boundary).Duration();
			// 嚴格小於：距離相同時保留先遇到的（較早的）那一筆
			if (diff < bestDiff)
			{
				best = sorted[i];
				bestDiff = diff;
			}
		}
		return best;
	}

	/// <summary>第一筆 At ≥ t 的索引</summary>
	private static int LowerBound(IReadOnlyList<KwhReading> sorted, DateTimeOffset t)
	{
		int lo = 0, hi = sorted.Count;
		while (lo < hi)
		{
			var mid = (lo + hi) / 2;
			if (sorted[mid].At < t) lo = mid + 1;
			else hi = mid;
		}
		return lo;
	}
}

public static class HourlyEnergyCalculator
{
	/// <summary>計算 [fromHour, toHour) 每個整點小時的用電與狀態</summary>
	public static List<HourResult> Compute(IReadOnlyList<KwhReading> sorted, DateTimeOffset fromHour, DateTimeOffset toHour)
	{
		var results = new List<HourResult>();
		if (toHour <= fromHour) return results;

		// 每個邊界只找一次：H 的終點就是 H+1 的起點，連續小時相加剛好等於頭尾相減
		var start = BoundaryPicker.Pick(sorted, fromHour);
		for (var h = fromHour; h < toHour; h = h.AddHours(1))
		{
			var end = BoundaryPicker.Pick(sorted, h.AddHours(1));
			results.Add(Evaluate(h, start, end));
			start = end;
		}
		return results;
	}

	private static HourResult Evaluate(DateTimeOffset hour, KwhReading? start, KwhReading? end)
	{
		if (start == null || end == null)
		{
			var status = start == null && end == null ? HourStatus.NoBoth : start == null ? HourStatus.NoStart : HourStatus.NoEnd;
			return new HourResult(hour, null, status, start, end);
		}
		var kwh = end.Value.Kwh - start.Value.Kwh;
		return kwh < 0
			? new HourResult(hour, null, HourStatus.CounterDecrease, start, end)
			: new HourResult(hour, kwh, HourStatus.Ok, start, end);
	}
}

/// <summary>
/// 日／月／年（C 方案，使用者 2026-10-05 確認）：
/// 總用電＝期間頭尾邊界讀值相減（中間斷線也算得出來）；已量測＝可計算小時加總；未分時段＝總用電−已量測。
/// </summary>
public static class PeriodEnergyCalculator
{
	/// <param name="hours">該期間內的小時結果（只會加總 ok 的小時）</param>
	/// <param name="touOf">小時（UTC 起點）→ 時段</param>
	public static PeriodResult Compute(KwhReading? startBoundary, KwhReading? endBoundary, IEnumerable<HourResult> hours, int totalHours, Func<DateTimeOffset, Tou> touOf)
	{
		decimal peak = 0, half = 0, satHalf = 0, off = 0;
		var okHours = 0;
		foreach (var h in hours)
		{
			if (h.Status != HourStatus.Ok) continue;
			okHours++;
			switch (touOf(h.HourStart))
			{
				case Tou.Peak: peak += h.Kwh!.Value; break;
				case Tou.Half: half += h.Kwh!.Value; break;
				case Tou.SatHalf: satHalf += h.Kwh!.Value; break;
				default: off += h.Kwh!.Value; break;
			}
		}
		var measured = peak + half + satHalf + off;

		decimal? total = startBoundary != null && endBoundary != null && endBoundary.Value.Kwh >= startBoundary.Value.Kwh
			? endBoundary.Value.Kwh - startBoundary.Value.Kwh
			: null;
		// 理論上 total ≥ measured（可計算小時是總區間的子集）；資料異常時不讓未分時段變負數
		decimal? unassigned = total == null ? null : Math.Max(0, total.Value - measured);

		return new PeriodResult(total, measured, peak, half, satHalf, off, unassigned, okHours, totalHours);
	}
}

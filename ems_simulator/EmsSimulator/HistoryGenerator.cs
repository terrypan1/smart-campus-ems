namespace EmsSimulator;

/// <summary>刻意安排的斷線事件（台北時間），用來展示「不可計算的小時」與「未分時段電量」（docs/實作/02 §3.2）</summary>
public sealed record OutageEvent(string Name, DateTime FromLocal, DateTime ToLocal, Func<MeterDef, bool> Applies);

public readonly record struct HistoryRow(DateTimeOffset SampledAt, double KwhTotal, MeterValues Values);

/// <summary>
/// Demo 用的歷史讀值（每 15 分鐘一筆）。用跟即時模擬同一套 LoadModel；
/// 累積電度從「第一筆即時讀值」往回推，所以歷史跟即時在交界處是連續的。
/// </summary>
public static class HistoryGenerator
{
	public static readonly TimeSpan Taipei = TimeSpan.FromHours(8);
	public static readonly TimeSpan Slot = TimeSpan.FromMinutes(15);

	public static readonly OutageEvent[] Outages =
	[
		new("R 棟網路中斷", new DateTime(2026, 3, 12, 13, 5, 0), new DateTime(2026, 3, 12, 16, 10, 0), m => m.Building == "R"),
		new("全校網路閃斷", new DateTime(2026, 7, 8, 9, 35, 0), new DateTime(2026, 7, 8, 10, 25, 0), _ => true),
		new("圖書館空調1 離線", new DateTime(2026, 9, 20, 18, 0, 0), new DateTime(2026, 9, 21, 23, 30, 0), m => m.UnitId == 34),
	];

	/// <summary>
	/// 產生 [startUtc, anchorAt − 1 分鐘] 之間的讀值。anchorKwh＝anchorAt 當下的累積電度（第一筆即時讀值）。
	/// </summary>
	public static List<HistoryRow> Generate(MeterDef def, string spaceType, DateTimeOffset startUtc, DateTimeOffset anchorAt, double anchorKwh, int seed,
		double dropRate = 0.02, double delayRate = 0.005)
	{
		var rng = new Random(seed);
		var lastSlot = anchorAt - TimeSpan.FromMinutes(1);

		// 1) 每個時槽的 kW（緩慢漂移＋小雜訊，跟即時模擬同一個形狀）
		var slots = new List<(DateTimeOffset At, double Kw)>();
		var drift = 1.0;
		for (var t = startUtc; t <= lastSlot; t += Slot)
		{
			drift = Math.Clamp(drift + (rng.NextDouble() - 0.5) * 0.04, 0.9, 1.1);
			var kw = Math.Max(0, LoadModel.TargetKw(def, spaceType, t.ToOffset(Taipei).DateTime) * drift * (1 + (rng.NextDouble() - 0.5) * 0.04));
			slots.Add((t, kw));
		}

		// 2) 從 anchor 往回推每個時槽起點的累積電度
		var kwhAt = new double[slots.Count];
		var e = anchorKwh;
		for (var k = slots.Count - 1; k >= 0; k--)
		{
			var next = k + 1 < slots.Count ? slots[k + 1].At : anchorAt;
			e -= slots[k].Kw * (next - slots[k].At).TotalHours;
			kwhAt[k] = e;
		}

		// 3) 採集抖動、隨機遺失、斷線事件
		var outages = Outages.Where(o => o.Applies(def)).ToArray();
		var rows = new List<HistoryRow>(slots.Count);
		for (var k = 0; k < slots.Count; k++)
		{
			var (at, kw) = slots[k];
			var dropped = rng.NextDouble() < dropRate;
			var delayed = rng.NextDouble() < delayRate;
			var jitterSec = delayed ? 60 + rng.NextDouble() * 120 : rng.NextDouble() * 8;
			if (at.AddSeconds(jitterSec) >= anchorAt) jitterSec = 0;
			var sampledAt = at.AddSeconds(jitterSec);

			var local = sampledAt.ToOffset(Taipei).DateTime;
			if (dropped || outages.Any(o => local >= o.FromLocal && local < o.ToLocal)) continue;

			var values = new MeterValues();
			SimMeter.FillElectrical(values, def, kw, rng);
			values.DemandKw = (float)kw;
			var kwh = Math.Round(kwhAt[k] + kw * jitterSec / 3600, 2);
			values.KwhTotal = kwh;
			rows.Add(new HistoryRow(sampledAt, kwh, values));
		}
		return rows;
	}

	/// <summary>anchor 所在月份往前 months 個月的 1 號 00:00（台北）</summary>
	public static DateTimeOffset StartOf(DateTimeOffset anchorAt, int months)
	{
		var local = anchorAt.ToOffset(Taipei);
		return new DateTimeOffset(local.Year, local.Month, 1, 0, 0, 0, Taipei).AddMonths(-months);
	}
}

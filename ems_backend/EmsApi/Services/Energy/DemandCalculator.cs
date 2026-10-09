namespace EmsApi.Services.Energy;

/// <summary>一顆電表一個 15 分鐘區塊的平均需量</summary>
public readonly record struct BlockDemand(DateTimeOffset BlockStart, decimal Kw);

/// <summary>
/// 15 分鐘需量：區塊用電 × 4。區塊頭尾的累積電度用 ±5 分鐘內最接近的讀值（docs/實作/03 §3.4，台電條文未定義算法）。
/// 頭或尾找不到、或電度倒退 → 這個區塊沒有需量（不補值）。
/// </summary>
public static class DemandCalculator
{
	public static readonly TimeSpan Block = TimeSpan.FromMinutes(15);
	public static readonly TimeSpan Window = TimeSpan.FromMinutes(5);

	public static DateTimeOffset FloorBlock(DateTimeOffset t)
	{
		var u = t.ToUniversalTime();
		return new DateTimeOffset(u.Year, u.Month, u.Day, u.Hour, u.Minute / 15 * 15, 0, TimeSpan.Zero);
	}

	/// <summary>[from, to) 之間每個區塊的需量（from、to 都要是整 15 分鐘）</summary>
	public static List<BlockDemand> Compute(IReadOnlyList<KwhReading> sorted, DateTimeOffset from, DateTimeOffset to)
	{
		var result = new List<BlockDemand>();
		var start = BoundaryPicker.Pick(sorted, from, Window);
		for (var b = from; b < to; b += Block)
		{
			var end = BoundaryPicker.Pick(sorted, b + Block, Window);
			if (start != null && end != null && end.Value.Kwh >= start.Value.Kwh)
				result.Add(new BlockDemand(b, (end.Value.Kwh - start.Value.Kwh) * 4));
			start = end;
		}
		return result;
	}

	/// <summary>
	/// 目前區塊的即時需量：區塊起點到最新讀值的平均（已用電度 ÷ 已過時間）。
	/// 區塊剛開始（不到 2 分鐘）時平均不穩定，回 null，呼叫端改用瞬時 kW。
	/// </summary>
	public static decimal? CurrentBlockKw(IReadOnlyList<KwhReading> sorted, DateTimeOffset now)
	{
		var b = FloorBlock(now);
		var start = BoundaryPicker.Pick(sorted, b, Window);
		if (start == null || sorted.Count == 0) return null;
		var last = sorted[^1];
		var hours = (decimal)(last.At - start.Value.At).TotalHours;
		if (hours < 2m / 60 || last.Kwh < start.Value.Kwh) return null;
		return (last.Kwh - start.Value.Kwh) / hours;
	}
}

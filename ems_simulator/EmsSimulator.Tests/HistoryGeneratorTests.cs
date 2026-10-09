using EmsSimulator;

namespace EmsSimulator.Tests;

public class HistoryGeneratorTests
{
	private static readonly TimeSpan Tw = TimeSpan.FromHours(8);
	private static readonly MeterDef Aircon = new(34, "N", "圖書館 空調1", "aircon", 90);
	private static readonly MeterDef RLight = new(42, "R", "第一教學大樓 照明總盤", "lighting_socket", 45);

	private static List<HistoryRow> Gen(MeterDef def, DateTimeOffset anchor, double anchorKwh = 2_000_000, double drop = 0.02) =>
		HistoryGenerator.Generate(def, "teaching", HistoryGenerator.StartOf(anchor, 13), anchor, anchorKwh, seed: 1, dropRate: drop);

	[Fact]
	public void 從13個月前的月初開始_結束在錨點之前()
	{
		var anchor = new DateTimeOffset(2026, 10, 5, 20, 13, 0, Tw);
		var rows = Gen(RLight, anchor);
		Assert.Equal(new DateTimeOffset(2025, 9, 1, 0, 0, 0, Tw), HistoryGenerator.StartOf(anchor, 13));
		Assert.True(rows[0].SampledAt >= new DateTimeOffset(2025, 9, 1, 0, 0, 0, Tw));
		Assert.True(rows[^1].SampledAt < anchor);
	}

	[Fact]
	public void 累積電度遞增_且跟錨點接得上()
	{
		var anchor = new DateTimeOffset(2026, 10, 5, 20, 13, 0, Tw);
		var rows = Gen(RLight, anchor, anchorKwh: 692_315.37, drop: 0);
		for (var i = 1; i < rows.Count; i++)
		{
			Assert.True(rows[i].KwhTotal >= rows[i - 1].KwhTotal);
			Assert.True(rows[i].SampledAt > rows[i - 1].SampledAt);
		}
		// 最後一筆（20:00 左右）到錨點 20:13 只差十幾分鐘的用電，幾度以內
		var gap = 692_315.37 - rows[^1].KwhTotal;
		Assert.InRange(gap, 0, RLight.RatedKw * 0.25);
	}

	[Fact]
	public void 斷線事件期間沒有讀值()
	{
		var anchor = new DateTimeOffset(2026, 10, 5, 20, 13, 0, Tw);
		var r = Gen(RLight, anchor, drop: 0);
		Assert.DoesNotContain(r, x => InLocal(x, new DateTime(2026, 3, 12, 13, 5, 0), new DateTime(2026, 3, 12, 16, 10, 0)));
		Assert.Contains(r, x => InLocal(x, new DateTime(2026, 3, 12, 12, 0, 0), new DateTime(2026, 3, 12, 13, 0, 0)));

		var lib = Gen(Aircon, anchor, drop: 0);
		Assert.DoesNotContain(lib, x => InLocal(x, new DateTime(2026, 9, 20, 18, 0, 0), new DateTime(2026, 9, 21, 23, 30, 0)));
		// R 棟的斷線不影響圖書館
		Assert.Contains(lib, x => InLocal(x, new DateTime(2026, 3, 12, 14, 0, 0), new DateTime(2026, 3, 12, 15, 0, 0)));
	}

	[Fact]
	public void 隨機遺失大約2百分比()
	{
		var anchor = new DateTimeOffset(2026, 10, 5, 20, 13, 0, Tw);
		var all = Gen(Aircon, anchor, drop: 0).Count;
		var dropped = Gen(Aircon, anchor, drop: 0.02).Count;
		Assert.InRange(1 - (double)dropped / all, 0.01, 0.035);
	}

	private static bool InLocal(HistoryRow r, DateTime from, DateTime to)
	{
		var local = r.SampledAt.ToOffset(Tw).DateTime;
		return local >= from && local < to;
	}
}

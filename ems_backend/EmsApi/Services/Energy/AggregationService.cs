using EmsApi.Models;
using EmsApi.Services.Settings;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace EmsApi.Services.Energy;

/// <summary>
/// 從原始讀值重算每小時用電、日／月／年彙總（docs/實作/02 §4）與全校 15 分鐘需量（docs/實作/03 §5.3）。
/// 重算範圍內「先刪後寫」，可重複執行；用 COPY 批次寫入。同一時間只跑一個重算。
/// </summary>
public class AggregationService(IServiceScopeFactory scopeFactory, RuntimeSettings settings, ILogger<AggregationService> logger)
{
	private readonly SemaphoreSlim _lock = new(1, 1);

	private string Scheme => settings.TouScheme;

	/// <summary>小時要等「終點 + 20 分鐘」過了才定案：回傳第一個還沒定案的小時起點</summary>
	public static DateTimeOffset FinalizedUntil(DateTimeOffset now)
	{
		var t = now.ToUniversalTime() - BoundaryPicker.Window;
		return new DateTimeOffset(t.Year, t.Month, t.Day, t.Hour, 0, 0, TimeSpan.Zero);
	}

	/// <summary>台北日期的 00:00（UTC）</summary>
	public static DateTimeOffset LocalDayStart(DateOnly date) =>
		new DateTimeOffset(date.ToDateTime(TimeOnly.MinValue), TouCalendar.TaipeiOffset).ToUniversalTime();

	public static DateOnly LocalDate(DateTimeOffset t) => DateOnly.FromDateTime(t.ToOffset(TouCalendar.TaipeiOffset).DateTime);

	/// <summary>全部重算（啟動時彙總表是空的、或 backfill 之後）</summary>
	public async Task RecomputeAllAsync(CancellationToken ct)
	{
		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
		var first = await db.MeterReadings.MinAsync(r => (DateTimeOffset?)r.SampledAt, ct);
		if (first == null) return;
		await RecomputeFromAsync(LocalDate(first.Value), ct);
	}

	/// <summary>例行重算：昨天 00:00 起（涵蓋最近 26 小時以上）</summary>
	public Task RecomputeRecentAsync(CancellationToken ct) =>
		RecomputeFromAsync(LocalDate(DateTimeOffset.UtcNow).AddDays(-1), ct);

	private async Task RecomputeFromAsync(DateOnly fromDate, CancellationToken ct)
	{
		await _lock.WaitAsync(ct);
		try
		{
			var from = LocalDayStart(fromDate);
			var until = FinalizedUntil(DateTimeOffset.UtcNow);
			if (until <= from) return;
			var started = DateTime.UtcNow;

			using var scope = scopeFactory.CreateScope();
			var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
			var meters = await db.Meters.AsNoTracking().OrderBy(m => m.Id).Select(m => new { m.Id, m.Category }).ToListAsync(ct);

			var demand = new SortedDictionary<DateTimeOffset, BlockSum>();
			foreach (var m in meters)
			{
				var blocks = await RecomputeMeterHoursAndDaysAsync(db, m.Id, from, until, ct);
				foreach (var b in blocks)
				{
					if (!demand.TryGetValue(b.BlockStart, out var sum)) demand[b.BlockStart] = sum = new BlockSum();
					if (m.Category == "solar") sum.Solar += b.Kw;
					else sum.Load += b.Kw;
					sum.MetersOk++;
				}
			}

			await RecomputePeriodsAsync(db, PeriodTypes.Month, fromDate, until, ct);
			await RecomputePeriodsAsync(db, PeriodTypes.Year, fromDate, until, ct);
			await WriteCampusDemandAsync(db, demand, meters.Count, from, ct);

			logger.LogInformation("彙總重算完成：{From:yyyy-MM-dd} 起至 {Until:yyyy-MM-dd HH:mm}（台北），{Meters} 顆電表，耗時 {Sec:F1}s",
				fromDate, until.ToOffset(TouCalendar.TaipeiOffset), meters.Count, (DateTime.UtcNow - started).TotalSeconds);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <returns>這顆電表 [from, until) 每個 15 分鐘區塊的需量</returns>
	private async Task<List<BlockDemand>> RecomputeMeterHoursAndDaysAsync(EmsDbContext db, int meterId, DateTimeOffset from, DateTimeOffset until, CancellationToken ct)
	{
		var lo = from - BoundaryPicker.Window;
		var hi = until + BoundaryPicker.Window;
		var readings = await db.MeterReadings.AsNoTracking()
			.Where(r => r.MeterId == meterId && r.SampledAt >= lo && r.SampledAt <= hi)
			.OrderBy(r => r.SampledAt)
			.Select(r => new KwhReading(r.SampledAt, r.KwhTotal))
			.ToListAsync(ct);

		var hours = HourlyEnergyCalculator.Compute(readings, from, until);
		var scheme = Scheme;

		var days = new List<(DateOnly Date, PeriodResult Result)>();
		for (var dayStart = from; dayStart < until; dayStart = dayStart.AddDays(1))
		{
			var dayEnd = dayStart.AddDays(1) < until ? dayStart.AddDays(1) : until;
			var inDay = hours.Where(h => h.HourStart >= dayStart && h.HourStart < dayEnd);
			var result = PeriodEnergyCalculator.Compute(
				BoundaryPicker.Pick(readings, dayStart), BoundaryPicker.Pick(readings, dayEnd), inDay,
				(int)(dayEnd - dayStart).TotalHours, h => TouCalendar.Of(h, scheme));
			days.Add((LocalDate(dayStart), result));
		}

		var conn = await OpenAsync(db, ct);
		await using var tx = await conn.BeginTransactionAsync(ct);
		await ExecAsync(conn, tx, "DELETE FROM meter_hourly_energy WHERE meter_id = @m AND hour_start >= @from", ct, ("m", meterId), ("from", from));
		await ExecAsync(conn, tx, "DELETE FROM meter_energy_summary WHERE meter_id = @m AND period_type = 'day' AND period_start >= @d", ct, ("m", meterId), ("d", LocalDate(from)));

		await using (var w = await conn.BeginBinaryImportAsync(
			"COPY meter_hourly_energy (meter_id, hour_start, kwh, status, tou, start_at, start_kwh, end_at, end_kwh) FROM STDIN (FORMAT BINARY)", ct))
		{
			foreach (var h in hours)
			{
				await w.StartRowAsync(ct);
				await w.WriteAsync(meterId, NpgsqlDbType.Integer, ct);
				await w.WriteAsync(h.HourStart.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
				await WriteNullableAsync(w, h.Kwh, NpgsqlDbType.Numeric, ct);
				await w.WriteAsync(h.Status, NpgsqlDbType.Varchar, ct);
				await w.WriteAsync(TouName(TouCalendar.Of(h.HourStart, scheme)), NpgsqlDbType.Varchar, ct);
				await WriteNullableAsync(w, h.Start?.At.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
				await WriteNullableAsync(w, h.Start?.Kwh, NpgsqlDbType.Numeric, ct);
				await WriteNullableAsync(w, h.End?.At.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
				await WriteNullableAsync(w, h.End?.Kwh, NpgsqlDbType.Numeric, ct);
			}
			await w.CompleteAsync(ct);
		}

		await WriteSummariesAsync(conn, days.Select(d => (meterId, PeriodTypes.Day, d.Date, d.Result)), ct);
		await tx.CommitAsync(ct);

		return DemandCalculator.Compute(readings, from, until);
	}

	private sealed class BlockSum
	{
		public decimal Load;
		public decimal Solar;
		public int MetersOk;
	}

	/// <summary>全校 15 分鐘需量：先刪後寫 from 之後的區塊，再補記歷史超約事件</summary>
	private async Task WriteCampusDemandAsync(EmsDbContext db, SortedDictionary<DateTimeOffset, BlockSum> demand, int metersTotal, DateTimeOffset from, CancellationToken ct)
	{
		var scheme = Scheme;
		var conn = await OpenAsync(db, ct);
		await using var tx = await conn.BeginTransactionAsync(ct);
		await ExecAsync(conn, tx, "DELETE FROM campus_demand_15m WHERE block_start >= @from", ct, ("from", from));
		await using (var w = await conn.BeginBinaryImportAsync(
			"COPY campus_demand_15m (block_start, load_kw, solar_kw, net_kw, tou, meters_ok, meters_total) FROM STDIN (FORMAT BINARY)", ct))
		{
			foreach (var (block, sum) in demand)
			{
				await w.StartRowAsync(ct);
				await w.WriteAsync(block.ToUniversalTime(), NpgsqlDbType.TimestampTz, ct);
				await w.WriteAsync(Math.Round(sum.Load, 2), NpgsqlDbType.Numeric, ct);
				await w.WriteAsync(Math.Round(sum.Solar, 2), NpgsqlDbType.Numeric, ct);
				await w.WriteAsync(Math.Round(sum.Load - sum.Solar, 2), NpgsqlDbType.Numeric, ct);
				// 切換點都在整點，區塊所在小時的時段就是區塊的時段
				await w.WriteAsync(TouName(TouCalendar.Of(block, scheme)), NpgsqlDbType.Varchar, ct);
				await w.WriteAsync(sum.MetersOk, NpgsqlDbType.Integer, ct);
				await w.WriteAsync(metersTotal, NpgsqlDbType.Integer, ct);
			}
			await w.CompleteAsync(ct);
		}
		await RebuildHistoryEventsAsync(conn, tx, from, ct);
		await tx.CommitAsync(ct);
	}

	/// <summary>契約容量改了之後，重新補記全部歷史超約事件</summary>
	public async Task RebuildHistoryEventsAsync(CancellationToken ct)
	{
		await _lock.WaitAsync(ct);
		try
		{
			using var scope = scopeFactory.CreateScope();
			var conn = await OpenAsync(scope.ServiceProvider.GetRequiredService<EmsDbContext>(), ct);
			await using var tx = await conn.BeginTransactionAsync(ct);
			await RebuildHistoryEventsAsync(conn, tx, new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero), ct);
			await tx.CommitAsync(ct);
		}
		finally
		{
			_lock.Release();
		}
	}

	/// <summary>
	/// 歷史超約（source=history）：淨需量超過該時段的累計契約容量（尖峰＝經常；半尖峰＝經常＋半尖峰；…）。
	/// 同區塊已有即時偵測（live）的超約事件就不重複記。
	/// </summary>
	private static async Task RebuildHistoryEventsAsync(NpgsqlConnection conn, NpgsqlTransaction tx, DateTimeOffset from, CancellationToken ct)
	{
		await ExecAsync(conn, tx, "DELETE FROM demand_events WHERE source = 'history' AND block_start >= @from", ct, ("from", from));
		await ExecAsync(conn, tx, $"""
			INSERT INTO demand_events (occurred_at, block_start, level, demand_kw, threshold_kw, contract_kw, action, source)
			SELECT d.block_start + interval '15 minutes', d.block_start, '{DemandLevels.OverContract}', d.net_kw, x.cap, c.regular_kw, '超過契約容量（歷史資料重算補記）', 'history'
			FROM campus_demand_15m d
			CROSS JOIN (SELECT * FROM contract_settings ORDER BY id LIMIT 1) c
			CROSS JOIN LATERAL (SELECT CASE d.tou
				WHEN 'peak' THEN c.regular_kw
				WHEN 'half' THEN c.regular_kw + c.half_peak_kw
				WHEN 'sat_half' THEN c.regular_kw + c.half_peak_kw + c.sat_half_kw
				ELSE c.regular_kw + c.half_peak_kw + c.sat_half_kw + c.off_peak_kw END AS cap) x
			WHERE d.block_start >= @from AND d.net_kw > x.cap
			ON CONFLICT (block_start, level) DO NOTHING
			""", ct, ("from", from));
	}

	/// <summary>月／年：已量測、分時段、小時數由日彙總加總；總用電由期間頭尾邊界讀值相減</summary>
	private async Task RecomputePeriodsAsync(EmsDbContext db, string periodType, DateOnly fromDate, DateTimeOffset until, CancellationToken ct)
	{
		var untilDate = LocalDate(until);
		var periods = new List<DateOnly>();
		var p = periodType == PeriodTypes.Month ? new DateOnly(fromDate.Year, fromDate.Month, 1) : new DateOnly(fromDate.Year, 1, 1);
		for (; p <= untilDate; p = periodType == PeriodTypes.Month ? p.AddMonths(1) : p.AddYears(1)) periods.Add(p);

		var conn = await OpenAsync(db, ct);
		foreach (var periodStart in periods)
		{
			var nextStart = periodType == PeriodTypes.Month ? periodStart.AddMonths(1) : periodStart.AddYears(1);
			var startAt = LocalDayStart(periodStart);
			var endAt = LocalDayStart(nextStart) < until ? LocalDayStart(nextStart) : until;

			var sums = await db.MeterEnergySummaries.AsNoTracking()
				.Where(s => s.PeriodType == PeriodTypes.Day && s.PeriodStart >= periodStart && s.PeriodStart < nextStart)
				.GroupBy(s => s.MeterId)
				.Select(g => new
				{
					MeterId = g.Key,
					Measured = g.Sum(s => s.MeasuredKwh),
					Peak = g.Sum(s => s.PeakKwh),
					Half = g.Sum(s => s.HalfKwh),
					SatHalf = g.Sum(s => s.SatHalfKwh),
					Off = g.Sum(s => s.OffKwh),
					OkHours = g.Sum(s => s.OkHours),
					TotalHours = g.Sum(s => s.TotalHours),
				})
				.ToListAsync(ct);

			var startBoundaries = await BoundariesAsync(db, startAt, ct);
			var endBoundaries = await BoundariesAsync(db, endAt, ct);

			var rows = sums.Select(s =>
			{
				startBoundaries.TryGetValue(s.MeterId, out var b0);
				endBoundaries.TryGetValue(s.MeterId, out var b1);
				decimal? total = b0 != null && b1 != null && b1.Value.Kwh >= b0.Value.Kwh ? b1.Value.Kwh - b0.Value.Kwh : null;
				var result = new PeriodResult(total, s.Measured, s.Peak, s.Half, s.SatHalf, s.Off,
					total == null ? null : Math.Max(0, total.Value - s.Measured), s.OkHours, s.TotalHours);
				return (s.MeterId, periodType, periodStart, result);
			}).ToList();

			await using var tx = await conn.BeginTransactionAsync(ct);
			await ExecAsync(conn, tx, "DELETE FROM meter_energy_summary WHERE period_type = @t AND period_start = @d", ct, ("t", periodType), ("d", periodStart));
			await WriteSummariesAsync(conn, rows, ct);
			await tx.CommitAsync(ct);
		}
	}

	/// <summary>所有電表在某個邊界時間的邊界讀值（一次查詢）</summary>
	public static async Task<Dictionary<int, KwhReading?>> BoundariesAsync(EmsDbContext db, DateTimeOffset boundary, CancellationToken ct)
	{
		var lo = boundary - BoundaryPicker.Window;
		var hi = boundary + BoundaryPicker.Window;
		var rows = await db.MeterReadings.AsNoTracking()
			.Where(r => r.SampledAt >= lo && r.SampledAt <= hi)
			.OrderBy(r => r.MeterId).ThenBy(r => r.SampledAt)
			.Select(r => new { r.MeterId, r.SampledAt, r.KwhTotal })
			.ToListAsync(ct);
		return rows.GroupBy(r => r.MeterId)
			.ToDictionary(g => g.Key, g => BoundaryPicker.Pick(g.Select(r => new KwhReading(r.SampledAt, r.KwhTotal)).ToList(), boundary));
	}

	private static async Task WriteSummariesAsync(NpgsqlConnection conn, IEnumerable<(int MeterId, string Type, DateOnly Start, PeriodResult R)> rows, CancellationToken ct)
	{
		await using var w = await conn.BeginBinaryImportAsync(
			"COPY meter_energy_summary (meter_id, period_type, period_start, total_kwh, measured_kwh, peak_kwh, half_kwh, sat_half_kwh, off_kwh, unassigned_kwh, ok_hours, total_hours) FROM STDIN (FORMAT BINARY)", ct);
		foreach (var (meterId, type, start, r) in rows)
		{
			await w.StartRowAsync(ct);
			await w.WriteAsync(meterId, NpgsqlDbType.Integer, ct);
			await w.WriteAsync(type, NpgsqlDbType.Varchar, ct);
			await w.WriteAsync(start, NpgsqlDbType.Date, ct);
			await WriteNullableAsync(w, r.TotalKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.MeasuredKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.PeakKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.HalfKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.SatHalfKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.OffKwh, NpgsqlDbType.Numeric, ct);
			await WriteNullableAsync(w, r.UnassignedKwh, NpgsqlDbType.Numeric, ct);
			await w.WriteAsync(r.OkHours, NpgsqlDbType.Integer, ct);
			await w.WriteAsync(r.TotalHours, NpgsqlDbType.Integer, ct);
		}
		await w.CompleteAsync(ct);
	}

	public static string TouName(Tou t) => t switch { Tou.Peak => "peak", Tou.Half => "half", Tou.SatHalf => "sat_half", _ => "off" };
	public static Tou ParseTou(string s) => s switch { "peak" => Tou.Peak, "half" => Tou.Half, "sat_half" => Tou.SatHalf, _ => Tou.Off };

	private static async Task<NpgsqlConnection> OpenAsync(EmsDbContext db, CancellationToken ct)
	{
		var conn = (NpgsqlConnection)db.Database.GetDbConnection();
		if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
		return conn;
	}

	private static async Task ExecAsync(NpgsqlConnection conn, NpgsqlTransaction tx, string sql, CancellationToken ct, params (string Name, object Value)[] args)
	{
		await using var cmd = new NpgsqlCommand(sql, conn, tx);
		foreach (var (name, value) in args) cmd.Parameters.AddWithValue(name, value);
		await cmd.ExecuteNonQueryAsync(ct);
	}

	private static Task WriteNullableAsync<T>(NpgsqlBinaryImporter w, T? value, NpgsqlDbType type, CancellationToken ct) where T : struct =>
		value.HasValue ? w.WriteAsync(value.Value, type, ct) : w.WriteNullAsync(ct);
}

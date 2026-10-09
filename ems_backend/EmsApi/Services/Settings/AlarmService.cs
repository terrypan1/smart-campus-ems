using System.Text.Json;
using EmsApi.Dtos;
using EmsApi.Models;
using EmsApi.Services.Energy;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace EmsApi.Services.Settings;

/// <summary>
/// 告警（docs/實作/06 §2）。原則：同一件事只有一筆（有開始、有結束），不每分鐘洗版。
/// 開啟用 INSERT … ON CONFLICT (type, subject) WHERE ended_at IS NULL：已經有未結束的就只更新 last_seen_at。
/// </summary>
public class AlarmService(EmsDbContext db)
{
	public const string HistoryAck = "系統（歷史補記）";
	/// <summary>歷史讀值缺口超過這個長度才補記斷線告警（歷史讀值每 15 分鐘一筆，連續漏 3 筆以上）</summary>
	public static readonly TimeSpan HistoryGap = TimeSpan.FromMinutes(55);

	private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
	{
		Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
	};

	/// <summary>開啟告警；已有同類型同對象未結束的告警時只更新 last_seen_at</summary>
	public Task<int> OpenAsync(string type, string level, string subject, int? meterId, DateTimeOffset startedAt, DateTimeOffset now, string message, object detail) =>
		db.Database.ExecuteSqlInterpolatedAsync($"""
			INSERT INTO alarms (type, level, subject, meter_id, started_at, last_seen_at, message, detail, source)
			VALUES ({type}, {level}, {subject}, {meterId}, {startedAt.ToUniversalTime()}, {now.ToUniversalTime()}, {message}, {JsonSerializer.Serialize(detail, Json)}::jsonb, 'live')
			ON CONFLICT (type, subject) WHERE ended_at IS NULL DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at
			""");

	/// <summary>結束某類型中「不在 activeSubjects 裡」的未結束告警（狀態已恢復）</summary>
	public Task<int> CloseExceptAsync(string type, IReadOnlyCollection<string> activeSubjects, DateTimeOffset endedAt) =>
		db.Database.ExecuteSqlInterpolatedAsync($"""
			UPDATE alarms SET ended_at = {endedAt.ToUniversalTime()}
			WHERE type = {type} AND ended_at IS NULL AND NOT (subject = ANY({activeSubjects.ToArray()}))
			""");

	/// <summary>
	/// 需量類：條件已解除，而且最後一次成立的那個 15 分鐘區塊已經結束 → 結束時間＝該區塊結束
	/// </summary>
	public Task<int> CloseDemandAsync(string type, DateTimeOffset currentBlock) =>
		db.Database.ExecuteSqlInterpolatedAsync($"""
			UPDATE alarms SET ended_at = date_bin('15 minutes', last_seen_at, TIMESTAMPTZ '2000-01-01') + interval '15 minutes'
			WHERE type = {type} AND subject = 'campus' AND ended_at IS NULL
			  AND date_bin('15 minutes', last_seen_at, TIMESTAMPTZ '2000-01-01') < {currentBlock.ToUniversalTime()}
			""");

	public async Task<int> AckAsync(IReadOnlyCollection<long> ids, string account)
	{
		var now = DateTimeOffset.UtcNow;
		return await db.Alarms.Where(a => ids.Contains(a.Id) && a.AcknowledgedAt == null)
			.ExecuteUpdateAsync(s => s.SetProperty(a => a.AcknowledgedAt, now).SetProperty(a => a.AcknowledgedBy, account));
	}

	public async Task<List<AlarmDto>> ListAsync(string status, string? type, string? level, DateTimeOffset? from, DateTimeOffset? to, int limit)
	{
		var q = db.Alarms.AsNoTracking().AsQueryable();
		q = status switch
		{
			// 進行中＝會自動結束、還沒結束的；電度倒退與資料缺漏不算（看「未確認」）
			"active" => q.Where(a => a.EndedAt == null && AlarmTypes.AutoEnd.Contains(a.Type)),
			"unack" => q.Where(a => a.AcknowledgedAt == null),
			"all" => q,
			_ => throw new ArgumentException("status 只能是 active／unack／all"),
		};
		if (!string.IsNullOrEmpty(type)) q = q.Where(a => a.Type == type);
		if (!string.IsNullOrEmpty(level)) q = q.Where(a => a.Level == level);
		if (from != null) q = q.Where(a => a.EndedAt == null || a.EndedAt >= from);
		if (to != null) q = q.Where(a => a.StartedAt < to);
		var rows = await q.OrderByDescending(a => a.StartedAt).Take(Math.Clamp(limit, 1, 2000))
			.GroupJoin(db.Meters.AsNoTracking(), a => a.MeterId, m => m.Id, (a, ms) => new { a, ms })
			.SelectMany(x => x.ms.DefaultIfEmpty(), (x, m) => new { x.a, MeterCode = m == null ? null : m.Code, MeterName = m == null ? null : m.Name })
			.ToListAsync();
		return rows.Select(r => new AlarmDto(r.a.Id, r.a.Type, r.a.Level, r.a.Subject, r.a.MeterId, r.MeterCode, r.MeterName, r.a.StartedAt, r.a.EndedAt,
			r.a.LastSeenAt, r.a.Message, r.a.Detail, r.a.AcknowledgedAt, r.a.AcknowledgedBy, r.a.Source)).ToList();
	}

	public async Task<AlarmSummaryDto> SummaryAsync()
	{
		var unack = await db.Alarms.AsNoTracking().Where(a => a.AcknowledgedAt == null)
			.GroupBy(a => a.Level).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
		var active = await db.Alarms.AsNoTracking().Where(a => a.EndedAt == null && AlarmTypes.AutoEnd.Contains(a.Type))
			.GroupBy(a => a.Type).Select(g => new { g.Key, Count = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.Count);
		var latest = await ListAsync("unack", null, null, null, null, 10);
		return new AlarmSummaryDto(unack.Values.Sum(), unack, active, latest);
	}

	/// <summary>
	/// 歷史告警（source=history，docs/實作/06 §2.2）：先刪後寫，可重複執行。都標成系統已確認，鈴鐺只數即時發生的。
	/// - 電表斷線：讀值缺口 ≥ 55 分鐘（02 的 3 個斷線事件都在內）
	/// - 超約：連續的超約 15 分鐘區塊合併成一筆
	/// - 資料缺漏：某電表某天覆蓋率 &lt; 95%；電度倒退：小時狀態 counter_decrease
	/// 跟即時告警（live）重疊的不重複記。
	/// </summary>
	public async Task<int> RebuildHistoryAsync(CancellationToken ct)
	{
		var conn = (NpgsqlConnection)db.Database.GetDbConnection();
		if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
		await using var tx = await conn.BeginTransactionAsync(ct);
		var total = 0;
		async Task Exec(string sql, params (string, object)[] args)
		{
			await using var cmd = new NpgsqlCommand(sql, conn, tx) { CommandTimeout = 300 };
			foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v);
			total += Math.Max(0, await cmd.ExecuteNonQueryAsync(ct));
		}

		await Exec("DELETE FROM alarms WHERE source = 'history'");
		await Exec($"""
			INSERT INTO alarms (type, level, subject, meter_id, started_at, ended_at, last_seen_at, message, detail, acknowledged_at, acknowledged_by, source)
			SELECT '{AlarmTypes.MeterOffline}', '{AlarmLevels.Error}', 'hist:meter:' || g.meter_id || ':' || extract(epoch from g.prev)::bigint, g.meter_id,
			       g.prev, g.sampled_at, g.sampled_at,
			       m.code || ' ' || m.name || ' 讀值中斷 ' || round(extract(epoch from g.gap) / 60) || ' 分鐘',
			       jsonb_build_object('lastReadingAt', g.prev, 'nextReadingAt', g.sampled_at, 'gapMinutes', round(extract(epoch from g.gap) / 60)),
			       g.sampled_at, @ack, 'history'
			FROM (SELECT meter_id, sampled_at, lag(sampled_at) OVER w AS prev, sampled_at - lag(sampled_at) OVER w AS gap
			      FROM meter_readings WINDOW w AS (PARTITION BY meter_id ORDER BY sampled_at)) g
			JOIN meters m ON m.id = g.meter_id
			WHERE g.gap >= @gap
			  AND NOT EXISTS (SELECT 1 FROM alarms a WHERE a.type = '{AlarmTypes.MeterOffline}' AND a.meter_id = g.meter_id AND a.source = 'live'
			                  AND a.started_at < g.sampled_at AND coalesce(a.ended_at, now()) > g.prev)
			""", ("ack", HistoryAck), ("gap", HistoryGap));
		// 超約：同一段連續的超約區塊（gaps and islands）合併成一筆
		await Exec($"""
			INSERT INTO alarms (type, level, subject, started_at, ended_at, last_seen_at, message, detail, acknowledged_at, acknowledged_by, source)
			SELECT '{AlarmTypes.OverContract}', '{AlarmLevels.Critical}', 'hist:campus:' || extract(epoch from min(block_start))::bigint,
			       min(block_start), max(block_start) + interval '15 minutes', max(block_start),
			       '15 分鐘需量超過契約容量，最高 ' || round(max(demand_kw)) || ' kW（契約 ' || round(max(threshold_kw)) || ' kW），共 ' || count(*) || ' 個區塊',
			       jsonb_build_object('maxKw', max(demand_kw), 'capacityKw', max(threshold_kw), 'blocks', count(*)),
			       max(block_start) + interval '15 minutes', @ack, 'history'
			FROM (SELECT block_start, demand_kw, threshold_kw,
			             block_start - (row_number() OVER (ORDER BY block_start)) * interval '15 minutes' AS island
			      FROM demand_events WHERE level = '{DemandLevels.OverContract}') e
			GROUP BY island
			HAVING NOT EXISTS (SELECT 1 FROM alarms a WHERE a.type = '{AlarmTypes.OverContract}' AND a.source = 'live'
			                   AND a.started_at < max(e.block_start) + interval '15 minutes' AND coalesce(a.ended_at, now()) > min(e.block_start))
			""", ("ack", HistoryAck));
		await Exec(DataQualitySql("history"), ("ack", HistoryAck), ("since", new DateOnly(2000, 1, 1)));
		await tx.CommitAsync(ct);
		return total;
	}

	/// <summary>即時（每 5 分鐘）：近兩天的資料缺漏與電度倒退；不會自動結束，要人工確認</summary>
	public async Task ScanDataQualityAsync(DateOnly since, CancellationToken ct)
	{
		await db.Database.ExecuteSqlRawAsync(DataQualitySql("live"),
			[new NpgsqlParameter("ack", NpgsqlTypes.NpgsqlDbType.Varchar) { Value = DBNull.Value }, new NpgsqlParameter("since", since)], ct);
	}

	/// <summary>資料缺漏（某天覆蓋率 &lt; 95%，只看整天已定案的）＋電度倒退；Subject 帶日期／小時，同一件事只記一次</summary>
	private static string DataQualitySql(string source) => $"""
		INSERT INTO alarms (type, level, subject, meter_id, started_at, last_seen_at, message, detail, acknowledged_at, acknowledged_by, source)
		SELECT '{AlarmTypes.DataGap}', '{AlarmLevels.Info}', 'meter:' || s.meter_id || ':day:' || s.period_start, s.meter_id,
		       (s.period_start::timestamp AT TIME ZONE 'Asia/Taipei'), now(),
		       m.code || ' ' || m.name || ' ' || to_char(s.period_start, 'YYYY-MM-DD') || ' 資料覆蓋率 ' || round(100.0 * s.ok_hours / s.total_hours, 1) || '%（' || s.ok_hours || '/' || s.total_hours || ' 小時可計算）',
		       jsonb_build_object('date', s.period_start, 'okHours', s.ok_hours, 'totalHours', s.total_hours),
		       {(source == "history" ? "now()" : "NULL")}, @ack, '{source}'
		FROM meter_energy_summary s JOIN meters m ON m.id = s.meter_id
		WHERE s.period_type = 'day' AND s.total_hours = 24 AND s.ok_hours < s.total_hours * 0.95 AND s.period_start >= @since
		ON CONFLICT (type, subject) WHERE ended_at IS NULL DO NOTHING;
		INSERT INTO alarms (type, level, subject, meter_id, started_at, last_seen_at, message, detail, acknowledged_at, acknowledged_by, source)
		SELECT '{AlarmTypes.CounterDecrease}', '{AlarmLevels.Error}', 'meter:' || h.meter_id || ':hour:' || extract(epoch from h.hour_start)::bigint, h.meter_id,
		       h.hour_start, now(),
		       m.code || ' ' || m.name || ' 累積電度倒退（' || h.start_kwh || ' → ' || h.end_kwh || ' kWh），可能換表或資料異常',
		       jsonb_build_object('startKwh', h.start_kwh, 'endKwh', h.end_kwh, 'startAt', h.start_at, 'endAt', h.end_at),
		       {(source == "history" ? "now()" : "NULL")}, @ack, '{source}'
		FROM meter_hourly_energy h JOIN meters m ON m.id = h.meter_id
		WHERE h.status = 'counter_decrease' AND h.hour_start >= (@since::timestamp AT TIME ZONE 'Asia/Taipei')
		ON CONFLICT (type, subject) WHERE ended_at IS NULL DO NOTHING;
		""";
}

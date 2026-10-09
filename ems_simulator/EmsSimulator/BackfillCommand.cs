using Npgsql;
using NpgsqlTypes;

namespace EmsSimulator;

/// <summary>
/// Demo 專用：把模擬歷史讀值直接寫進 ems 資料庫（COPY 批次寫入，source='backfill'）。
///   EmsSimulator backfill [--connection "&lt;連線字串&gt;"] [--months 13] [--force]
/// 連線字串沒給時讀環境變數 ConnectionStrings__DefaultConnection（部署機的 /etc/ems/ems.env 同一組）。
/// 跑完之後要重算彙總（deploy/wsl/backfill.sh 會處理）。
/// </summary>
public static class BackfillCommand
{
	public static async Task<int> RunAsync(string[] args)
	{
		Console.OutputEncoding = System.Text.Encoding.UTF8;
		var connection = Arg(args, "--connection") ?? Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
		var months = int.TryParse(Arg(args, "--months"), out var m) ? m : 13;
		var force = args.Contains("--force");
		var metersFile = Arg(args, "--meters-file") ?? "data/meters.json";
		if (string.IsNullOrWhiteSpace(connection))
		{
			Console.Error.WriteLine("缺少連線字串：--connection 或環境變數 ConnectionStrings__DefaultConnection");
			return 1;
		}

		var catalog = MeterCatalog.Load(metersFile);
		await using var db = new NpgsqlConnection(connection);
		await db.OpenAsync();

		var meterIds = new Dictionary<int, int>();
		await using (var cmd = new NpgsqlCommand("SELECT unit_id, id FROM meters", db))
		await using (var r = await cmd.ExecuteReaderAsync())
			while (await r.ReadAsync()) meterIds[r.GetInt32(0)] = r.GetInt32(1);

		var started = DateTime.UtcNow;
		long total = 0;
		foreach (var def in catalog.Meters)
		{
			if (!meterIds.TryGetValue(def.UnitId, out var meterId))
			{
				Console.WriteLine($"略過 unit {def.UnitId}：資料庫沒有這顆電表");
				continue;
			}

			var existing = (long)(await Scalar(db, "SELECT count(*) FROM meter_readings WHERE meter_id = @m AND source = 'backfill'", meterId))!;
			if (existing > 0 && !force)
			{
				Console.WriteLine($"略過 {def.Name}：已有 {existing} 筆 backfill（要重產請加 --force）");
				continue;
			}
			if (existing > 0) await Exec(db, "DELETE FROM meter_readings WHERE meter_id = @m AND source = 'backfill'", meterId);

			// 錨點：第一筆即時讀值；還沒有即時讀值就用現在＋初始電度
			DateTimeOffset anchorAt = DateTimeOffset.UtcNow;
			double anchorKwh = ModbusSimulatorService.InitialKwh(def);
			await using (var cmd = new NpgsqlCommand("SELECT sampled_at, kwh_total FROM meter_readings WHERE meter_id = @m AND source = 'poll' ORDER BY sampled_at LIMIT 1", db))
			{
				cmd.Parameters.AddWithValue("m", meterId);
				await using var r = await cmd.ExecuteReaderAsync();
				if (await r.ReadAsync())
				{
					anchorAt = r.GetFieldValue<DateTimeOffset>(0);
					anchorKwh = (double)r.GetDecimal(1);
				}
			}

			var rows = HistoryGenerator.Generate(def, catalog.SpaceTypeOf(def), HistoryGenerator.StartOf(anchorAt, months), anchorAt, anchorKwh, seed: def.UnitId * 104729);
			await CopyAsync(db, meterId, rows);
			total += rows.Count;
			Console.WriteLine($"{def.Name}：{rows.Count} 筆");
		}

		Console.WriteLine($"完成：共 {total:N0} 筆，耗時 {(DateTime.UtcNow - started).TotalSeconds:F1}s");
		Console.WriteLine("斷線事件：" + string.Join("；", HistoryGenerator.Outages.Select(o => $"{o.Name} {o.FromLocal:yyyy-MM-dd HH:mm}–{o.ToLocal:MM-dd HH:mm}")));
		return 0;
	}

	private static async Task CopyAsync(NpgsqlConnection db, int meterId, List<HistoryRow> rows)
	{
		await using var w = await db.BeginBinaryImportAsync(
			"COPY meter_readings (meter_id, sampled_at, vrs, vst, vtr, i_r, i_s, i_t, kw, kvar, kva, pf, hz, demand_kw, kwh_total, source) FROM STDIN (FORMAT BINARY)");
		foreach (var row in rows)
		{
			var v = row.Values;
			await w.StartRowAsync();
			await w.WriteAsync(meterId, NpgsqlDbType.Integer);
			await w.WriteAsync(row.SampledAt.ToUniversalTime(), NpgsqlDbType.TimestampTz);
			foreach (var f in new[] { v.Vrs, v.Vst, v.Vtr, v.Ir, v.Is, v.It, v.Kw, v.Kvar, v.Kva, v.Pf, v.Hz, v.DemandKw })
				await w.WriteAsync(f, NpgsqlDbType.Real);
			await w.WriteAsync((decimal)row.KwhTotal, NpgsqlDbType.Numeric);
			await w.WriteAsync("backfill", NpgsqlDbType.Varchar);
		}
		await w.CompleteAsync();
	}

	private static string? Arg(string[] args, string name)
	{
		var i = Array.IndexOf(args, name);
		return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
	}

	private static async Task<object?> Scalar(NpgsqlConnection db, string sql, int meterId)
	{
		await using var cmd = new NpgsqlCommand(sql, db);
		cmd.Parameters.AddWithValue("m", meterId);
		return await cmd.ExecuteScalarAsync();
	}

	private static async Task Exec(NpgsqlConnection db, string sql, int meterId)
	{
		await using var cmd = new NpgsqlCommand(sql, db);
		cmd.Parameters.AddWithValue("m", meterId);
		await cmd.ExecuteNonQueryAsync();
	}
}

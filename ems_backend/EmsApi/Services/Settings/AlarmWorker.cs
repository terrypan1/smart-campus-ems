using EmsApi.Models;
using EmsApi.Services.Energy;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Settings;

/// <summary>
/// 每分鐘：電表斷線、Modbus 連線、需量警戒／超約；每 5 分鐘：資料缺漏、電度倒退（docs/實作/06 §2.2）。
/// 啟動時若還沒有歷史告警就補記一次。
/// </summary>
public class AlarmWorker(IServiceScopeFactory scopeFactory, CollectorStatus collector, ILogger<AlarmWorker> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			// 等彙總 Worker 先跑（啟動時可能正在全部重算）、採集先輪詢一次
			await Task.Delay(TimeSpan.FromSeconds(90), stoppingToken);
			using (var scope = scopeFactory.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
				if (!await db.Alarms.AnyAsync(a => a.Source == "history", stoppingToken))
				{
					var n = await scope.ServiceProvider.GetRequiredService<AlarmService>().RebuildHistoryAsync(stoppingToken);
					logger.LogInformation("已補記歷史告警（{N} 列異動）", n);
				}
			}

			var tick = 0;
			using var timer = new PeriodicTimer(Interval);
			do
			{
				try
				{
					await CheckAsync(tick++ % 5 == 0, stoppingToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					logger.LogError(ex, "告警檢查失敗");
				}
			} while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException)
		{
			// 正常停止
		}
	}

	private async Task CheckAsync(bool dataQuality, CancellationToken ct)
	{
		using var scope = scopeFactory.CreateScope();
		var sp = scope.ServiceProvider;
		var db = sp.GetRequiredService<EmsDbContext>();
		var alarms = sp.GetRequiredService<AlarmService>();
		var now = DateTimeOffset.UtcNow;

		// ---------- 電表斷線：啟用中的電表超過 3 分鐘沒有讀值 ----------
		var meters = await db.Meters.AsNoTracking().Where(m => m.IsEnabled).ToDictionaryAsync(m => m.Id, ct);
		var latest = await sp.GetRequiredService<MeterQueryService>().GetLatestAsync();
		var offline = new List<string>();
		foreach (var l in latest.Where(l => !l.Online && meters.ContainsKey(l.MeterId)))
		{
			var m = meters[l.MeterId];
			var subject = $"meter:{m.Id}";
			offline.Add(subject);
			var since = l.SampledAt ?? now;
			await alarms.OpenAsync(AlarmTypes.MeterOffline, AlarmLevels.Error, subject, m.Id, since, now,
				$"{m.Code} {m.Name} 超過 3 分鐘沒有讀值", new { lastReadingAt = l.SampledAt, host = $"{m.ModbusHost}:{m.ModbusPort}", unitId = m.UnitId });
		}
		await alarms.CloseExceptAsync(AlarmTypes.MeterOffline, offline, now);

		// ---------- Modbus 連線中斷 ----------
		var down = new List<string>();
		foreach (var (endpoint, st) in collector.Endpoints.Where(e => !e.Value.Ok))
		{
			var subject = $"modbus:{endpoint}";
			down.Add(subject);
			await alarms.OpenAsync(AlarmTypes.ModbusDown, AlarmLevels.Critical, subject, null, st.Since, now,
				$"Modbus 連線中斷 {endpoint}", new { endpoint, error = st.Error });
		}
		await alarms.CloseExceptAsync(AlarmTypes.ModbusDown, down, now);

		// ---------- 需量：警戒（含卸載）與超約 ----------
		var rt = await sp.GetRequiredService<DemandService>().GetRealtimeAsync(now);
		if (rt.MetersOnline > 0)
		{
			var detail = new { netKw = rt.NetKw, capacityKw = rt.CapacityKw, level = rt.Level, block = rt.BlockStart };
			if (rt.Level != null)
				await alarms.OpenAsync(AlarmTypes.DemandWarn, AlarmLevels.Warning, "campus", null, rt.BlockStart, now,
					$"需量 {rt.NetKw:N0} kW 達警戒門檻（{LevelName(rt.Level)}），契約 {rt.Contract.RegularKw:N0} kW", detail);
			if (rt.Level == DemandLevels.OverContract)
				await alarms.OpenAsync(AlarmTypes.OverContract, AlarmLevels.Critical, "campus", null, rt.BlockStart, now,
					$"需量 {rt.NetKw:N0} kW 超過契約容量 {rt.CapacityKw:N0} kW", detail);
			await alarms.CloseDemandAsync(AlarmTypes.DemandWarn, rt.BlockStart);
			await alarms.CloseDemandAsync(AlarmTypes.OverContract, rt.BlockStart);
		}

		if (dataQuality)
			await alarms.ScanDataQualityAsync(AggregationService.LocalDate(now).AddDays(-2), ct);
	}

	private static string LevelName(string level) => level switch
	{
		DemandLevels.Warn => "警示",
		DemandLevels.ShedAircon => "模擬卸載：關空調",
		DemandLevels.ShedFan => "模擬卸載：關進排風",
		DemandLevels.ShedLight => "模擬卸載：關照明",
		_ => "超約",
	};
}

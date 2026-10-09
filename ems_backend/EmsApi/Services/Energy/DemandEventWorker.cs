namespace EmsApi.Services.Energy;

/// <summary>每分鐘檢查即時需量，跨過警戒門檻或超約就記一筆事件（同區塊同等級只記一次；只記錄、不控制設備）</summary>
public class DemandEventWorker(IServiceScopeFactory scopeFactory, ILogger<DemandEventWorker> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			using var timer = new PeriodicTimer(Interval);
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				try
				{
					using var scope = scopeFactory.CreateScope();
					var demand = scope.ServiceProvider.GetRequiredService<DemandService>();
					var rt = await demand.GetRealtimeAsync(DateTimeOffset.UtcNow);
					if (rt.MetersOnline == 0) continue;
					var inserted = await demand.RecordLiveEventsAsync(rt);
					if (inserted > 0)
						logger.LogInformation("需量事件：{Level}，即時需量 {Kw} kW（區塊 {Block:HH:mm}）", rt.Level, rt.NetKw, rt.BlockStart.ToOffset(TouCalendar.TaipeiOffset));
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					logger.LogError(ex, "需量事件檢查失敗");
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 正常停止
		}
	}
}

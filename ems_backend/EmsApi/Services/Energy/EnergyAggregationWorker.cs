using EmsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Energy;

/// <summary>啟動時彙總表是空的就全部重算；之後每 5 分鐘重算昨天起的資料</summary>
public class EnergyAggregationWorker(AggregationService aggregation, IServiceScopeFactory scopeFactory, ILogger<EnergyAggregationWorker> logger) : BackgroundService
{
	private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		try
		{
			using (var scope = scopeFactory.CreateScope())
			{
				var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
				if (!await db.MeterEnergySummaries.AnyAsync(stoppingToken))
				{
					logger.LogInformation("彙總表是空的，開始全部重算");
					await aggregation.RecomputeAllAsync(stoppingToken);
				}
			}

			using var timer = new PeriodicTimer(Interval);
			do
			{
				try
				{
					await aggregation.RecomputeRecentAsync(stoppingToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					logger.LogError(ex, "例行彙總重算失敗");
				}
			} while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException)
		{
			// 正常停止
		}
	}
}

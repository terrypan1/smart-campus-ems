using System.Net;
using System.Net.Sockets;
using EmsApi.Models;
using EmsApi.Services.Settings;
using FluentModbus;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services.Collector;

/// <summary>
/// 每分鐘（對齊整分鐘）輪詢所有啟用的電表，寫入原始讀值。
/// 同一個 host:port 共用一條 Modbus TCP 連線；讀失敗只記 log、不寫假值，下一輪重連。
/// </summary>
public class ModbusCollectorService(IServiceScopeFactory scopeFactory, IConfiguration config, CollectorStatus status, ILogger<ModbusCollectorService> logger) : BackgroundService
{
	private readonly Dictionary<string, ModbusTcpClient> _clients = [];
	private readonly HashSet<int> _failing = [];
	private readonly HashSet<string> _failingEndpoints = [];

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		if (!config.GetValue("Collector:Enabled", true))
		{
			logger.LogInformation("採集已停用（Collector:Enabled=false）");
			return;
		}

		var interval = TimeSpan.FromSeconds(config.GetValue("Collector:IntervalSeconds", 60));
		// 對齊到下一個整分鐘（interval 的整數倍）再開始
		var now = DateTimeOffset.UtcNow;
		var wait = TimeSpan.FromTicks(interval.Ticks - now.Ticks % interval.Ticks);
		logger.LogInformation("採集啟動，間隔 {Interval}s，{Wait:F1}s 後第一次輪詢", interval.TotalSeconds, wait.TotalSeconds);

		try
		{
			await Task.Delay(wait, stoppingToken);
			using var timer = new PeriodicTimer(interval);
			do
			{
				try
				{
					await PollOnceAsync(stoppingToken);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					logger.LogError(ex, "本輪採集失敗");
				}
			} while (await timer.WaitForNextTickAsync(stoppingToken));
		}
		catch (OperationCanceledException)
		{
			// 正常停止
		}
		finally
		{
			foreach (var c in _clients.Values) c.Dispose();
		}
	}

	private async Task PollOnceAsync(CancellationToken ct)
	{
		using var scope = scopeFactory.CreateScope();
		var db = scope.ServiceProvider.GetRequiredService<EmsDbContext>();
		var meters = await db.Meters.AsNoTracking().Where(m => m.IsEnabled).OrderBy(m => m.ModbusHost).ThenBy(m => m.ModbusPort).ThenBy(m => m.UnitId).ToListAsync(ct);
		var timeoutMs = config.GetValue("Collector:TimeoutMs", 2000);

		var readings = new List<MeterReading>(meters.Count);
		status.Retain(meters.Select(m => $"{m.ModbusHost}:{m.ModbusPort}"));
		foreach (var group in meters.GroupBy(m => (m.ModbusHost, m.ModbusPort)))
		{
			var client = GetConnectedClient(group.Key.ModbusHost, group.Key.ModbusPort, timeoutMs);
			if (client == null) continue;

			foreach (var m in group)
			{
				try
				{
					var data = await client.ReadHoldingRegistersAsync((byte)m.UnitId, MeterRegisterDecoder.StartAddress, MeterRegisterDecoder.RegisterCount, ct);
					readings.Add(MeterRegisterDecoder.Decode(data.Span, m.Id, DateTimeOffset.UtcNow));
					if (_failing.Remove(m.Id)) logger.LogInformation("電表 {Code} 恢復通訊", m.Code);
				}
				catch (Exception ex) when (ex is not OperationCanceledException)
				{
					if (_failing.Add(m.Id)) logger.LogWarning("電表 {Code}（{Host}:{Port} unit {Unit}）讀取失敗：{Error}", m.Code, m.ModbusHost, m.ModbusPort, m.UnitId, ex.Message);
					// ModbusException＝這顆表回了錯誤碼，連線還能用；其他（連線被關閉、逾時…）丟掉這條連線，同組其餘電表等下一輪重連。
					// FluentModbus 連線被對方關掉時丟的是 InvalidOperationException（2026-10-05 20:40 實際遇到，當時整輪沒寫入）
					if (ex is ModbusException) continue;
					DropClient(group.Key.ModbusHost, group.Key.ModbusPort);
					break;
				}
			}
		}

		db.MeterReadings.AddRange(readings);
		await db.SaveChangesAsync(ct);
		logger.LogDebug("本輪採集 {Ok}/{Total} 顆", readings.Count, meters.Count);
	}

	private ModbusTcpClient? GetConnectedClient(string host, int port, int timeoutMs)
	{
		var key = $"{host}:{port}";
		if (_clients.TryGetValue(key, out var existing) && existing.IsConnected) return existing;
		DropClient(host, port);

		var client = new ModbusTcpClient { ConnectTimeout = timeoutMs, ReadTimeout = timeoutMs, WriteTimeout = timeoutMs };
		try
		{
			var address = IPAddress.TryParse(host, out var ip) ? ip : Dns.GetHostAddresses(host).First(a => a.AddressFamily == AddressFamily.InterNetwork);
			client.Connect(new IPEndPoint(address, port), ModbusEndianness.BigEndian);
			_clients[key] = client;
			if (_failingEndpoints.Remove(key)) logger.LogInformation("Modbus 連線恢復 {Key}", key);
			status.Report(key, true);
			return client;
		}
		catch (Exception ex)
		{
			client.Dispose();
			if (_failingEndpoints.Add(key)) logger.LogWarning("Modbus 連線失敗 {Key}：{Error}", key, ex.Message);
			status.Report(key, false, ex.Message);
			return null;
		}
	}

	private void DropClient(string host, int port)
	{
		var key = $"{host}:{port}";
		if (_clients.Remove(key, out var c)) c.Dispose();
	}
}

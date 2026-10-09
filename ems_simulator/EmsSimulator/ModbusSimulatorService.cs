using System.Net;
using FluentModbus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EmsSimulator;

public sealed class SimulatorOptions
{
	public string MetersFile { get; set; } = "data/meters.json";
	public string StateFile { get; set; } = "state/meter_state.json";
	public string BindAddress { get; set; } = "0.0.0.0";
	public int Port { get; set; } = 1502;
}

/// <summary>一個 Modbus TCP server，每顆電表一個 Unit ID；每秒更新暫存器、每分鐘存一次累積電度。</summary>
public sealed class ModbusSimulatorService(IOptions<SimulatorOptions> options, ILogger<ModbusSimulatorService> logger) : BackgroundService
{
	private static readonly TimeZoneInfo Taipei = TimeZoneInfo.FindSystemTimeZoneById("Asia/Taipei");
	private readonly SimulatorOptions _opt = options.Value;
	private List<SimMeter> _meters = [];
	private MeterStateStore? _store;

	internal static double InitialKwh(MeterDef m) =>
		Math.Round(m.RatedKw * (9000 + new Random(m.UnitId * 7919).NextDouble() * 12000), 2);

	protected override async Task ExecuteAsync(CancellationToken stoppingToken)
	{
		var catalog = MeterCatalog.Load(_opt.MetersFile);
		_store = new MeterStateStore(_opt.StateFile);
		var saved = _store.Load();

		// 沒有存檔的電表給一個「已運轉數年」的初始值（每顆依 unitId 固定亂數，數字才不會都很整齊）
		_meters = catalog.Meters
			.Select(m => new SimMeter(m, catalog.SpaceTypeOf(m), saved.GetValueOrDefault(m.UnitId, InitialKwh(m)), seed: m.UnitId))
			.ToList();

		using var server = new ModbusTcpServer(logger);
		foreach (var m in _meters) server.AddUnit((byte)m.Def.UnitId);
		server.Start(new IPEndPoint(IPAddress.Parse(_opt.BindAddress), _opt.Port));
		logger.LogInformation("Modbus TCP 模擬器啟動 {Bind}:{Port}，{Count} 顆電表（Unit ID {Min}–{Max}），累積電度存檔 {State}",
			_opt.BindAddress, _opt.Port, _meters.Count, _meters.Min(m => m.Def.UnitId), _meters.Max(m => m.Def.UnitId), Path.GetFullPath(_opt.StateFile));

		var last = DateTime.UtcNow;
		var lastSave = last;
		using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
		try
		{
			while (await timer.WaitForNextTickAsync(stoppingToken))
			{
				var now = DateTime.UtcNow;
				var dt = (now - last).TotalSeconds;
				last = now;
				var local = TimeZoneInfo.ConvertTimeFromUtc(now, Taipei);

				lock (server.Lock)
				{
					foreach (var m in _meters)
					{
						m.Step(local, dt);
						RegisterMap.Encode(m.Values, server.GetHoldingRegisterBuffer((byte)m.Def.UnitId));
					}
				}

				if ((now - lastSave).TotalSeconds >= 60)
				{
					_store.Save(_meters);
					lastSave = now;
				}
			}
		}
		catch (OperationCanceledException)
		{
			// 正常停止
		}
		finally
		{
			_store.Save(_meters);
			server.Stop();
			logger.LogInformation("模擬器停止，累積電度已存檔");
		}
	}
}

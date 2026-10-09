using System.Collections.Concurrent;

namespace EmsApi.Services.Settings;

/// <summary>
/// 執行中可改的設定（singleton）：時段方案。啟動時由 app_settings 載入（沒有就用 appsettings 的 Tariff:Scheme），
/// 系統設定修改後立即更新；時段變了要重算彙總才會反映到歷史資料。
/// </summary>
public class RuntimeSettings(IConfiguration config)
{
	public const string TouSchemeKey = "tou_scheme";

	private volatile string? _scheme;

	public string TouScheme
	{
		get => _scheme ?? config["Tariff:Scheme"] ?? Energy.TouSchemes.HighVoltage3;
		set => _scheme = value;
	}
}

/// <summary>採集器各 Modbus 連線的狀態（singleton）：告警 Worker 用它判斷「Modbus 連線中斷」</summary>
public class CollectorStatus
{
	public sealed record EndpointState(bool Ok, DateTimeOffset Since, string? Error);

	private readonly ConcurrentDictionary<string, EndpointState> _endpoints = new();

	public IReadOnlyDictionary<string, EndpointState> Endpoints => _endpoints;

	public void Report(string endpoint, bool ok, string? error = null)
	{
		_endpoints.AddOrUpdate(endpoint, _ => new EndpointState(ok, DateTimeOffset.UtcNow, error),
			(_, old) => old.Ok == ok ? old with { Error = error ?? old.Error } : new EndpointState(ok, DateTimeOffset.UtcNow, error));
	}

	/// <summary>已經沒有啟用電表的連線不再追蹤</summary>
	public void Retain(IEnumerable<string> endpoints)
	{
		var keep = endpoints.ToHashSet();
		foreach (var k in _endpoints.Keys.Where(k => !keep.Contains(k))) _endpoints.TryRemove(k, out _);
	}
}

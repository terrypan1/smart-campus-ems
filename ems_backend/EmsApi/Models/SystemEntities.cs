namespace EmsApi.Models;

public static class AlarmTypes
{
	public const string DemandWarn = "demand_warn";
	public const string OverContract = "over_contract";
	public const string MeterOffline = "meter_offline";
	public const string ModbusDown = "modbus_down";
	public const string CounterDecrease = "counter_decrease";
	public const string DataGap = "data_gap";

	/// <summary>狀態恢復時會自動結束的類型；電度倒退、資料缺漏不會結束，只能人工確認</summary>
	public static readonly string[] AutoEnd = [DemandWarn, OverContract, MeterOffline, ModbusDown];
}

public static class AlarmLevels
{
	public const string Info = "info";
	public const string Warning = "warning";
	public const string Error = "error";
	public const string Critical = "critical";
}

/// <summary>
/// 告警（docs/實作/06 §2）。同一 Type＋Subject 同時只有一筆未結束（ended_at 為 null）的告警：
/// Subject 例：meter:12、modbus:127.0.0.1:1502、campus；不會自動結束的類型（電度倒退、資料缺漏）Subject 帶小時／日期。
/// LastSeenAt＝最後一次確認條件仍成立的時間（需量類用它判斷區塊是否結束）
/// </summary>
public class Alarm
{
	public long Id { get; set; }
	public string Type { get; set; } = "";
	public string Level { get; set; } = "";
	public string Subject { get; set; } = "";
	public int? MeterId { get; set; }
	public DateTimeOffset StartedAt { get; set; }
	public DateTimeOffset? EndedAt { get; set; }
	public DateTimeOffset LastSeenAt { get; set; }
	public string Message { get; set; } = "";
	/// <summary>JSON：當時數值、門檻</summary>
	public string Detail { get; set; } = "{}";
	public DateTimeOffset? AcknowledgedAt { get; set; }
	public string? AcknowledgedBy { get; set; }
	/// <summary>live＝即時偵測；history＝重算歷史資料時補記</summary>
	public string Source { get; set; } = "live";
}

/// <summary>設定類修改的稽核紀錄（docs/實作/06 §4）</summary>
public class AuditLog
{
	public long Id { get; set; }
	public DateTimeOffset At { get; set; }
	public string Account { get; set; } = "";
	/// <summary>例：電價、契約容量、電表 MAIN-05、使用者 viewer</summary>
	public string Item { get; set; } = "";
	public string? Before { get; set; }
	public string? After { get; set; }
}

/// <summary>農曆與補充的離峰日（全日離峰）；國曆固定日期的離峰日寫在 TouCalendar（電價表條文固定）</summary>
public class OffPeakDay
{
	public int Id { get; set; }
	public DateOnly Date { get; set; }
	public string Name { get; set; } = "";
}

/// <summary>系統參數（key-value），例：tou_scheme＝HV3／HV2</summary>
public class AppSetting
{
	public string Key { get; set; } = "";
	public string Value { get; set; } = "";
	public DateTimeOffset UpdatedAt { get; set; }
	public string UpdatedBy { get; set; } = "";
}

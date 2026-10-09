namespace EmsApi.Models;

/// <summary>
/// 全校每 15 分鐘需量（kW）。需量＝區塊用電 × 4；太陽能自發自用，台電看到的是淨需量＝用電－太陽能。
/// MetersOk &lt; MetersTotal 表示有電表缺資料，需量會偏低（畫面標「部分資料」）。
/// </summary>
public class CampusDemand15m
{
	public long Id { get; set; }
	/// <summary>區塊起點（UTC，整 15 分鐘）</summary>
	public DateTimeOffset BlockStart { get; set; }
	public decimal LoadKw { get; set; }
	public decimal SolarKw { get; set; }
	public decimal NetKw { get; set; }
	/// <summary>peak／half／sat_half／off</summary>
	public string Tou { get; set; } = "";
	public int MetersOk { get; set; }
	public int MetersTotal { get; set; }
}

/// <summary>契約容量與需量警戒門檻（只有一筆）。門檻是經常契約容量的百分比（參考現行電力監控系統畫面）</summary>
public class ContractSetting
{
	public int Id { get; set; }
	public decimal RegularKw { get; set; }
	public decimal HalfPeakKw { get; set; }
	public decimal SatHalfKw { get; set; }
	public decimal OffPeakKw { get; set; }
	public decimal WarnPct { get; set; }
	public decimal ShedAirconPct { get; set; }
	public decimal ShedFanPct { get; set; }
	public decimal ShedLightPct { get; set; }
	public DateTimeOffset UpdatedAt { get; set; }
	public string UpdatedBy { get; set; } = "";
}

/// <summary>電價費率。Item：basic_regular／basic_half／basic_sat_half／basic_off（元/kW/月）、energy_peak／energy_half／energy_sat_half／energy_off（元/度）</summary>
public class TariffRate
{
	public int Id { get; set; }
	/// <summary>例：HV3_UNIV＝高壓三段式（尖峰時間固定）大學（專）適用</summary>
	public string Scheme { get; set; } = "";
	/// <summary>summer／nonsummer</summary>
	public string Season { get; set; } = "";
	public string Item { get; set; } = "";
	public decimal Rate { get; set; }
	/// <summary>台電公告值（「還原台電公告值」用）；Rate ≠ OfficialRate＝已手動修改</summary>
	public decimal OfficialRate { get; set; }
	/// <summary>來源文件與頁碼</summary>
	public string Source { get; set; } = "";
	public DateTimeOffset? UpdatedAt { get; set; }
	public string? UpdatedBy { get; set; }
}

public static class DemandLevels
{
	public const string Warn = "warn";
	public const string ShedAircon = "shed_aircon";
	public const string ShedFan = "shed_fan";
	public const string ShedLight = "shed_light";
	public const string OverContract = "over_contract";
}

/// <summary>需量事件：跨過警戒門檻或超約。卸載只記錄、不實際控制設備（Demo）</summary>
public class DemandEvent
{
	public long Id { get; set; }
	public DateTimeOffset OccurredAt { get; set; }
	public DateTimeOffset BlockStart { get; set; }
	public string Level { get; set; } = "";
	public decimal DemandKw { get; set; }
	public decimal ThresholdKw { get; set; }
	public decimal ContractKw { get; set; }
	/// <summary>模擬動作說明，例：模擬卸載：關閉空調（不實際控制）</summary>
	public string Action { get; set; } = "";
	/// <summary>live＝即時偵測；history＝重算歷史 15 分鐘需量時補記的超約</summary>
	public string Source { get; set; } = "live";
}

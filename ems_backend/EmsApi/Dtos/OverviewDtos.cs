using EmsApi.Services.Energy;

namespace EmsApi.Dtos;

/// <summary>
/// 一段期間跟去年同期的比較。Current／LastYear 是淨用電（用電－太陽能）kWh，用期間頭尾累積電度相減（C 方案）。
/// 去年同期：今日＝往前 364 天（同星期幾）同一時間；本月＝去年同月 1 號到同一天的同一時間。MetersMissing＝頭尾讀值找不到、沒算進去的電表數；
/// 超過一半電表找不到時視為無法計算（null），不顯示偏低的數字
/// </summary>
public record EnergyCompareDto(decimal? Current, decimal? LastYear, DateTimeOffset From, DateTimeOffset To, DateTimeOffset LastYearFrom, DateTimeOffset LastYearTo, int MetersMissing);

/// <summary>今天的需量：最高淨需量、超過契約的 15 分鐘區塊數（00:00 到最近一個已結束的區塊）、各等級事件數</summary>
public record TodayDemandDto(decimal? MaxKw, DateTimeOffset? MaxAt, Tou? MaxTou, decimal CapacityKw, int OverBlocks, int BlocksDone, Dictionary<string, int> Events);

public record MaxCompareDto(decimal? Kw, DateTimeOffset? At, decimal OverKw, decimal? LastYearKw, DateTimeOffset? LastYearAt);

/// <summary>本月電費試算（含稅）與去年同月同期（1 號到同一天）；時段方案是二段式時整個為 null（不提供試算）</summary>
public record BillCompareDto(decimal Current, decimal? LastYear, DateOnly Through);

/// <summary>碳排（公噸 CO2e）＝淨用電 × 排碳係數</summary>
public record CarbonCompareDto(decimal CurrentTon, decimal? LastYearTon, decimal Factor, int FactorYear, bool Provisional);

/// <summary>TodayKwh 為 null＝太陽能電表今天 00:00 前後都沒有讀值</summary>
public record SolarTodayDto(decimal? TodayKwh, decimal MonthKwh, decimal MonthSaving);

/// <summary>UnackAlarms＝未確認告警數；ActiveAlarms＝未結束告警數（docs/實作/06 §2.4）</summary>
public record SystemStatusDto(int MetersOffline, int MetersTotal, double MonthCoverage, int UnackAlarms, int ActiveAlarms);

public record OverviewDto(
	DateTimeOffset GeneratedAt, TodayDemandDto Today,
	EnergyCompareDto TodayEnergy, EnergyCompareDto MonthEnergy, MaxCompareDto MonthMax,
	BillCompareDto? Bill, CarbonCompareDto Carbon, SolarTodayDto Solar, SystemStatusDto System);

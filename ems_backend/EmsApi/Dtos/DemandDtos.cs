using EmsApi.Services.Energy;

namespace EmsApi.Dtos;

public record ContractDto(
	decimal RegularKw, decimal HalfPeakKw, decimal SatHalfKw, decimal OffPeakKw,
	decimal WarnPct, decimal ShedAirconPct, decimal ShedFanPct, decimal ShedLightPct,
	DateTimeOffset UpdatedAt, string UpdatedBy);

public record ContractUpdateDto(
	decimal RegularKw, decimal HalfPeakKw, decimal SatHalfKw, decimal OffPeakKw,
	decimal WarnPct, decimal ShedAirconPct, decimal ShedFanPct, decimal ShedLightPct);

/// <summary>
/// 即時需量。NetKw＝目前區塊已過部分的平均（用電－太陽能）；InstantNetKw＝最新瞬時 kW。
/// Status：normal／warn（≥ 警示門檻）／over（≥ 該時段契約容量）；Level＝已跨過的最高等級。
/// </summary>
public record RealtimeDemandDto(
	DateTimeOffset At, DateTimeOffset BlockStart, int ElapsedSeconds,
	decimal LoadKw, decimal SolarKw, decimal NetKw, decimal InstantNetKw,
	Tou Tou, decimal CapacityKw, string Status, string? Level,
	int MetersOnline, int MetersTotal, ContractDto Contract);

/// <summary>一個 15 分鐘區塊；還沒算到的區塊各值為 null。MetersOk &lt; MetersTotal＝部分資料</summary>
public record DemandBlockDto(DateTimeOffset BlockStart, Tou Tou, decimal? LoadKw, decimal? SolarKw, decimal? NetKw, int MetersOk, int MetersTotal);

public record TouMaxDto(Tou Tou, decimal Kw, DateTimeOffset At, decimal CapacityKw, decimal OverKw);

/// <summary>某月各時段最高淨需量。OverBlocks＝超過該時段契約容量的區塊數</summary>
public record MonthlyDemandDto(DateOnly Month, List<TouMaxDto> ByTou, decimal MaxKw, DateTimeOffset? MaxAt, decimal OverKw, int OverBlocks);

public record DemandEventDto(long Id, DateTimeOffset OccurredAt, DateTimeOffset BlockStart, string Level, decimal DemandKw, decimal ThresholdKw, decimal ContractKw, string Action, string Source);

/// <summary>
/// 某月電費試算。Kwh 系列是計費度數（用電－太陽能，已分攤未分時段）；AllocatedKwh＝依時段比例分攤的未分時段度數（估算）。
/// IsPartial＝月份還沒結束（流動電費只算到目前，基本電費仍以整月計）。SolarSaving＝沒有太陽能時的電費 − 實際電費。
/// </summary>
public record BillEstimateDto(
	DateOnly Month, bool IsPartial, int DaysWithData, string Scheme,
	decimal PeakKwh, decimal HalfKwh, decimal SatHalfKwh, decimal OffKwh, decimal TotalKwh, decimal AllocatedKwh, decimal SolarKwh,
	ContractDto Contract, List<TouMaxDemand> MaxDemands, double? AvgPowerFactor,
	BillResult Bill, decimal SolarSaving);

public record BillHistoryDto(
	DateOnly Month, bool IsPartial, decimal TotalKwh, decimal BasicTotal, decimal PowerFactorAdjustment, decimal OverageTotal, decimal EnergyTotal,
	decimal Total, decimal TaxIncluded, decimal SolarSaving);

public record TariffRateDto(string Season, string Item, decimal Rate, string Source);

public record TariffDto(string Scheme, string SchemeName, List<TariffRateDto> Rates);

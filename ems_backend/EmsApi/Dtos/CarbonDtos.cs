namespace EmsApi.Dtos;

/// <summary>某年度實際使用的係數。Provisional＝該年度還沒公告，暫用 FactorYear 年度的值</summary>
public record FactorUseDto(int Year, decimal Factor, int FactorYear, bool Provisional, string Source);

/// <summary>
/// 每月碳排（範疇二、地點基準）。NetKwh＝用電－太陽能；EmissionTon＝NetKwh × 係數 ÷ 1000；
/// ReductionTon＝太陽能 × 係數 ÷ 1000（自發自用綠電的減碳量）。用電含未分時段（碳排不分時段）
/// </summary>
public record CarbonMonthDto(
	DateOnly Month, bool IsPartial, decimal LoadKwh, decimal SolarKwh, decimal NetKwh,
	decimal Factor, int FactorYear, bool Provisional, decimal EmissionTon, decimal ReductionTon);

/// <summary>今年累計與去年同期（1/1 到同一天）。LastYearDataFrom 晚於 LastYearFrom＝去年同期資料不完整（歷史資料起點之後才有）</summary>
public record CarbonSummaryDto(
	int Year, DateOnly From, DateOnly To, decimal NetKwh, decimal SolarKwh, decimal EmissionTon, decimal ReductionTon, FactorUseDto Factor,
	DateOnly LastYearFrom, DateOnly LastYearTo, DateOnly? LastYearDataFrom, decimal? LastYearEmissionTon, FactorUseDto LastYearFactor);

public record EmissionFactorDto(int Year, decimal Factor, string Source, bool IsProvisional);

/// <summary>用電結構的一列（某空間類型／大樓／用途／電表）。ByCategory＝這一列各用途的 kWh</summary>
public record StructureRowDto(string Key, string Label, string? SpaceType, int? BuildingId, decimal Kwh, decimal? LastYearKwh, double Share, int Rank,
	Dictionary<string, decimal> ByCategory, int MeterCount);

/// <summary>期間用電結構（不含太陽能；太陽能另列 SolarKwh）。各列加總＝TotalKwh</summary>
public record StructureDto(string Level, DateOnly From, DateOnly To, DateOnly LastYearFrom, DateOnly LastYearTo, string GroupBy,
	decimal TotalKwh, decimal? LastYearTotalKwh, decimal SolarKwh, List<StructureRowDto> Rows);

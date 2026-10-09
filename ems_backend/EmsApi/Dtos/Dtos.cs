namespace EmsApi.Dtos;

public record LoginRequestDto(string Account, string Password);

public record UserDto(int Id, string Account, string DisplayName, string Role);

public record LoginResponseDto(string Token, DateTime ExpiresAt, UserDto User);

public record MeterDto(
	int Id, string Code, string Name, string Category, double RatedKw, int UnitId, bool IsEnabled,
	int BuildingId, string BuildingCode, string BuildingName, string SpaceType);

/// <summary>一個期間（日／月）的用電；Coverage＝可計算小時 ÷ 已定案小時</summary>
public record EnergyPeriodDto(
	DateOnly PeriodStart, decimal TotalKwh, decimal MeasuredKwh, decimal PeakKwh, decimal HalfKwh, decimal SatHalfKwh, decimal OffKwh,
	decimal UnassignedKwh, double Coverage, int IncompleteMeters, int MeterCount, decimal SolarKwh);

/// <summary>Status：ok／no_start／no_end／no_both／counter_decrease／pending（還沒算到）</summary>
public record HourlyEnergyDto(DateTimeOffset HourStart, int Hour, decimal? Kwh, string Status, string Tou, DateTimeOffset? StartAt, DateTimeOffset? EndAt);

public record LatestReadingDto(
	int MeterId, DateTimeOffset? SampledAt, bool Online,
	float? Kw, float? Kvar, float? Pf, float? DemandKw, float? Vavg, float? Iavg, float? Hz, decimal? KwhTotal);

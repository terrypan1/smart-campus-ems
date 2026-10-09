namespace EmsApi.Dtos;

public record AlarmDto(
	long Id, string Type, string Level, string Subject, int? MeterId, string? MeterCode, string? MeterName,
	DateTimeOffset StartedAt, DateTimeOffset? EndedAt, DateTimeOffset LastSeenAt, string Message, string Detail,
	DateTimeOffset? AcknowledgedAt, string? AcknowledgedBy, string Source);

/// <summary>Unacknowledged＝未確認總數；ByLevel＝未確認各等級；ActiveByType＝進行中（會自動結束、還沒結束）各類型；Latest＝最新 10 筆未確認</summary>
public record AlarmSummaryDto(int Unacknowledged, Dictionary<string, int> ByLevel, Dictionary<string, int> ActiveByType, List<AlarmDto> Latest);

public record AckRequestDto(List<long> Ids);

public record TariffRateEditDto(string Season, string Item, decimal Rate);

public record TariffRateAdminDto(string Season, string Item, decimal Rate, decimal OfficialRate, bool Modified, string Source, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public record SchemeDto(string Scheme, DateTimeOffset? UpdatedAt, string? UpdatedBy);

public record OffPeakDayDto(int? Id, DateOnly Date, string Name, bool Fixed);

public record OffPeakDayCreateDto(DateOnly Date, string Name);

public record MeterAdminDto(
	int Id, string Code, string Name, string Category, double RatedKw, int BuildingId, string BuildingName,
	string ModbusHost, int ModbusPort, int UnitId, bool IsEnabled);

public record MeterUpdateDto(string Name, string Category, bool IsEnabled, string ModbusHost, int ModbusPort, int UnitId);

/// <summary>測試連線：成功時帶一筆即時讀值（不寫入資料庫）</summary>
public record MeterTestDto(bool Ok, string Message, int ElapsedMs, float? Kw, float? Pf, float? Vavg, decimal? KwhTotal);

public record UserAdminDto(int Id, string Account, string DisplayName, string Role, bool IsActive, DateTimeOffset CreatedAt);

public record UserCreateDto(string Account, string DisplayName, string Role, string Password);

public record UserUpdateDto(string DisplayName, string Role, bool IsActive);

public record PasswordResetDto(string Password);

public record ChangePasswordDto(string OldPassword, string NewPassword);

public record EmissionFactorEditDto(int Year, decimal Factor, string Source, bool IsProvisional);

public record AuditLogDto(long Id, DateTimeOffset At, string Account, string Item, string? Before, string? After);

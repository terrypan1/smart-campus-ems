namespace EmsApi.Models;

public static class Roles
{
	public const string Admin = "admin";
	public const string Viewer = "viewer";
}

public class User
{
	public int Id { get; set; }
	public string Account { get; set; } = "";
	public string DisplayName { get; set; } = "";
	public string PasswordHash { get; set; } = "";
	public string Role { get; set; } = Roles.Viewer;
	public bool IsActive { get; set; } = true;
	public DateTimeOffset CreatedAt { get; set; }
}

public class Campus
{
	public int Id { get; set; }
	public string Code { get; set; } = "";
	public string Name { get; set; } = "";
	public double ContractKw { get; set; }
	public List<Building> Buildings { get; set; } = [];
}

public class Building
{
	public int Id { get; set; }
	public int CampusId { get; set; }
	public Campus? Campus { get; set; }
	public string Code { get; set; } = "";
	public string Name { get; set; } = "";
	/// <summary>teaching／library／office／dormitory／other</summary>
	public string SpaceType { get; set; } = "";
	public List<Meter> Meters { get; set; } = [];
}

public class Meter
{
	public int Id { get; set; }
	public int BuildingId { get; set; }
	public Building? Building { get; set; }
	/// <summary>例：MAIN-05（校區代碼＋原始電表編號）</summary>
	public string Code { get; set; } = "";
	public string Name { get; set; } = "";
	/// <summary>lighting_socket／aircon／power／other／solar</summary>
	public string Category { get; set; } = "";
	public double RatedKw { get; set; }
	public string ModbusHost { get; set; } = "";
	public int ModbusPort { get; set; }
	public int UnitId { get; set; }
	public bool IsEnabled { get; set; } = true;
}

/// <summary>原始讀值。sampled_at 是實際讀到的時間（UTC），不對齊整分鐘；原始資料不刪，彙總從這裡重算。</summary>
public class MeterReading
{
	public long Id { get; set; }
	public int MeterId { get; set; }
	public DateTimeOffset SampledAt { get; set; }
	public float Vrs { get; set; }
	public float Vst { get; set; }
	public float Vtr { get; set; }
	public float Ir { get; set; }
	public float Is { get; set; }
	public float It { get; set; }
	public float Kw { get; set; }
	public float Kvar { get; set; }
	public float Kva { get; set; }
	public float Pf { get; set; }
	public float Hz { get; set; }
	public float DemandKw { get; set; }
	/// <summary>累積電度（kWh，0.01 解析度）</summary>
	public decimal KwhTotal { get; set; }
	/// <summary>poll＝即時採集；backfill＝Demo 模擬歷史（ems_simulator backfill 產生）</summary>
	public string Source { get; set; } = ReadingSources.Poll;
}

public static class ReadingSources
{
	public const string Poll = "poll";
	public const string Backfill = "backfill";
}

/// <summary>每小時用電（小時邊界規則）。kwh 為 null＝不可計算，原因看 status</summary>
public class MeterHourlyEnergy
{
	public long Id { get; set; }
	public int MeterId { get; set; }
	/// <summary>小時起點（UTC；台北整點＝UTC 整點）</summary>
	public DateTimeOffset HourStart { get; set; }
	public decimal? Kwh { get; set; }
	public string Status { get; set; } = "";
	/// <summary>peak／half／sat_half／off</summary>
	public string Tou { get; set; } = "";
	public DateTimeOffset? StartAt { get; set; }
	public decimal? StartKwh { get; set; }
	public DateTimeOffset? EndAt { get; set; }
	public decimal? EndKwh { get; set; }
}

public static class PeriodTypes
{
	public const string Day = "day";
	public const string Month = "month";
	public const string Year = "year";
}

/// <summary>日／月／年用電（C 方案）。total 為 null＝期間頭尾邊界讀值找不到</summary>
public class MeterEnergySummary
{
	public long Id { get; set; }
	public int MeterId { get; set; }
	public string PeriodType { get; set; } = "";
	/// <summary>台北日期：日＝當天；月＝1 號；年＝1/1</summary>
	public DateOnly PeriodStart { get; set; }
	public decimal? TotalKwh { get; set; }
	public decimal MeasuredKwh { get; set; }
	public decimal PeakKwh { get; set; }
	/// <summary>平日半尖峰</summary>
	public decimal HalfKwh { get; set; }
	/// <summary>週六半尖峰（流動電費、超約計算跟平日半尖峰不同）</summary>
	public decimal SatHalfKwh { get; set; }
	public decimal OffKwh { get; set; }
	public decimal? UnassignedKwh { get; set; }
	public int OkHours { get; set; }
	/// <summary>已定案的小時數（進行中的期間只算到最後一個定案的小時）</summary>
	public int TotalHours { get; set; }
}

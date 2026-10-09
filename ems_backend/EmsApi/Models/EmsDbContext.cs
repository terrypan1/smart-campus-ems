using Microsoft.EntityFrameworkCore;

namespace EmsApi.Models;

public class EmsDbContext(DbContextOptions<EmsDbContext> options) : DbContext(options)
{
	public DbSet<User> Users => Set<User>();
	public DbSet<Campus> Campuses => Set<Campus>();
	public DbSet<Building> Buildings => Set<Building>();
	public DbSet<Meter> Meters => Set<Meter>();
	public DbSet<MeterReading> MeterReadings => Set<MeterReading>();
	public DbSet<MeterHourlyEnergy> MeterHourlyEnergies => Set<MeterHourlyEnergy>();
	public DbSet<MeterEnergySummary> MeterEnergySummaries => Set<MeterEnergySummary>();
	public DbSet<CampusDemand15m> CampusDemands => Set<CampusDemand15m>();
	public DbSet<ContractSetting> ContractSettings => Set<ContractSetting>();
	public DbSet<TariffRate> TariffRates => Set<TariffRate>();
	public DbSet<DemandEvent> DemandEvents => Set<DemandEvent>();
	public DbSet<EmissionFactor> EmissionFactors => Set<EmissionFactor>();
	public DbSet<Alarm> Alarms => Set<Alarm>();
	public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
	public DbSet<OffPeakDay> OffPeakDays => Set<OffPeakDay>();
	public DbSet<AppSetting> AppSettings => Set<AppSetting>();

	protected override void OnModelCreating(ModelBuilder b)
	{
		b.Entity<User>(e =>
		{
			e.HasIndex(x => x.Account).IsUnique();
			e.Property(x => x.Account).HasMaxLength(50);
			e.Property(x => x.DisplayName).HasMaxLength(50);
			e.Property(x => x.Role).HasMaxLength(20);
		});

		b.Entity<Campus>(e =>
		{
			e.HasIndex(x => x.Code).IsUnique();
			e.Property(x => x.Code).HasMaxLength(20);
			e.Property(x => x.Name).HasMaxLength(50);
		});

		b.Entity<Building>(e =>
		{
			e.HasIndex(x => new { x.CampusId, x.Code }).IsUnique();
			e.Property(x => x.Code).HasMaxLength(20);
			e.Property(x => x.Name).HasMaxLength(50);
			e.Property(x => x.SpaceType).HasMaxLength(20);
		});

		b.Entity<Meter>(e =>
		{
			e.HasIndex(x => x.Code).IsUnique();
			e.Property(x => x.Code).HasMaxLength(20);
			e.Property(x => x.Name).HasMaxLength(100);
			e.Property(x => x.Category).HasMaxLength(20);
			e.Property(x => x.ModbusHost).HasMaxLength(100);
		});

		b.Entity<MeterReading>(e =>
		{
			// 同一顆電表同一時間只會有一筆；也是「最新讀值」「時間區間查詢」的索引
			e.HasIndex(x => new { x.MeterId, x.SampledAt }).IsUnique();
			// 全部電表某段時間（即時需量、期間頭尾邊界、總覽同期比較）
			e.HasIndex(x => x.SampledAt);
			e.Property(x => x.KwhTotal).HasPrecision(14, 2);
			// is 是 SQL 保留字，電流欄位統一寫成 i_r／i_s／i_t
			e.Property(x => x.Ir).HasColumnName("i_r");
			e.Property(x => x.Is).HasColumnName("i_s");
			e.Property(x => x.It).HasColumnName("i_t");
			e.Property(x => x.Source).HasMaxLength(10).HasDefaultValue(ReadingSources.Poll);
			e.HasOne<Meter>().WithMany().HasForeignKey(x => x.MeterId);
		});

		b.Entity<MeterHourlyEnergy>(e =>
		{
			e.ToTable("meter_hourly_energy");
			e.HasIndex(x => new { x.MeterId, x.HourStart }).IsUnique();
			e.Property(x => x.Kwh).HasPrecision(14, 2);
			e.Property(x => x.StartKwh).HasPrecision(14, 2);
			e.Property(x => x.EndKwh).HasPrecision(14, 2);
			e.Property(x => x.Status).HasMaxLength(20);
			e.Property(x => x.Tou).HasMaxLength(10);
			e.HasOne<Meter>().WithMany().HasForeignKey(x => x.MeterId);
		});

		b.Entity<MeterEnergySummary>(e =>
		{
			e.ToTable("meter_energy_summary");
			e.HasIndex(x => new { x.MeterId, x.PeriodType, x.PeriodStart }).IsUnique();
			e.HasIndex(x => new { x.PeriodType, x.PeriodStart });
			e.Property(x => x.PeriodType).HasMaxLength(10);
			foreach (var p in new[] { nameof(MeterEnergySummary.TotalKwh), nameof(MeterEnergySummary.MeasuredKwh), nameof(MeterEnergySummary.PeakKwh),
				nameof(MeterEnergySummary.HalfKwh), nameof(MeterEnergySummary.SatHalfKwh), nameof(MeterEnergySummary.OffKwh), nameof(MeterEnergySummary.UnassignedKwh) })
				e.Property(p).HasPrecision(16, 2);
			e.HasOne<Meter>().WithMany().HasForeignKey(x => x.MeterId);
		});

		b.Entity<CampusDemand15m>(e =>
		{
			e.ToTable("campus_demand_15m");
			e.HasIndex(x => x.BlockStart).IsUnique();
			e.Property(x => x.LoadKw).HasPrecision(12, 2);
			e.Property(x => x.SolarKw).HasPrecision(12, 2);
			e.Property(x => x.NetKw).HasPrecision(12, 2);
			e.Property(x => x.Tou).HasMaxLength(10);
		});

		b.Entity<ContractSetting>(e =>
		{
			foreach (var p in new[] { nameof(ContractSetting.RegularKw), nameof(ContractSetting.HalfPeakKw), nameof(ContractSetting.SatHalfKw), nameof(ContractSetting.OffPeakKw) })
				e.Property(p).HasPrecision(10, 1);
			foreach (var p in new[] { nameof(ContractSetting.WarnPct), nameof(ContractSetting.ShedAirconPct), nameof(ContractSetting.ShedFanPct), nameof(ContractSetting.ShedLightPct) })
				e.Property(p).HasPrecision(5, 1);
			e.Property(x => x.UpdatedBy).HasMaxLength(50);
		});

		b.Entity<TariffRate>(e =>
		{
			e.HasIndex(x => new { x.Scheme, x.Season, x.Item }).IsUnique();
			e.Property(x => x.Scheme).HasMaxLength(20);
			e.Property(x => x.Season).HasMaxLength(10);
			e.Property(x => x.Item).HasMaxLength(30);
			e.Property(x => x.Rate).HasPrecision(10, 2);
			e.Property(x => x.OfficialRate).HasPrecision(10, 2);
			e.Property(x => x.Source).HasMaxLength(200);
			e.Property(x => x.UpdatedBy).HasMaxLength(50);
		});

		b.Entity<Alarm>(e =>
		{
			// 同一類型＋對象只能有一筆未結束的告警（AlarmService 用 ON CONFLICT 去重）
			e.HasIndex(x => new { x.Type, x.Subject }).IsUnique().HasFilter("ended_at IS NULL");
			e.HasIndex(x => x.StartedAt);
			e.Property(x => x.Type).HasMaxLength(30);
			e.Property(x => x.Level).HasMaxLength(10);
			e.Property(x => x.Subject).HasMaxLength(100);
			e.Property(x => x.Message).HasMaxLength(300);
			e.Property(x => x.Detail).HasColumnType("jsonb");
			e.Property(x => x.AcknowledgedBy).HasMaxLength(50);
			e.Property(x => x.Source).HasMaxLength(10);
		});

		b.Entity<AuditLog>(e =>
		{
			e.HasIndex(x => x.At);
			e.Property(x => x.Account).HasMaxLength(50);
			e.Property(x => x.Item).HasMaxLength(100);
			e.Property(x => x.Before).HasColumnType("jsonb");
			e.Property(x => x.After).HasColumnType("jsonb");
		});

		b.Entity<OffPeakDay>(e =>
		{
			e.HasIndex(x => x.Date).IsUnique();
			e.Property(x => x.Name).HasMaxLength(50);
		});

		b.Entity<AppSetting>(e =>
		{
			e.HasKey(x => x.Key);
			e.Property(x => x.Key).HasMaxLength(50);
			e.Property(x => x.Value).HasMaxLength(200);
			e.Property(x => x.UpdatedBy).HasMaxLength(50);
		});

		b.Entity<EmissionFactor>(e =>
		{
			e.HasIndex(x => x.Year).IsUnique();
			e.Property(x => x.Factor).HasPrecision(6, 4);
			e.Property(x => x.Source).HasMaxLength(200);
		});

		b.Entity<DemandEvent>(e =>
		{
			e.HasIndex(x => x.OccurredAt);
			e.HasIndex(x => new { x.BlockStart, x.Level }).IsUnique();
			e.Property(x => x.Level).HasMaxLength(20);
			e.Property(x => x.Action).HasMaxLength(100);
			e.Property(x => x.Source).HasMaxLength(10);
			foreach (var p in new[] { nameof(DemandEvent.DemandKw), nameof(DemandEvent.ThresholdKw), nameof(DemandEvent.ContractKw) })
				e.Property(p).HasPrecision(12, 2);
		});
	}
}

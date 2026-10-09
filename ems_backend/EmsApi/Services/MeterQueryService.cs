using EmsApi.Dtos;
using EmsApi.Models;
using Microsoft.EntityFrameworkCore;

namespace EmsApi.Services;

public class MeterQueryService(EmsDbContext db)
{
	/// <summary>最後一筆讀值超過這個時間就視為斷線（採集每分鐘一次，容許漏兩輪）</summary>
	public static readonly TimeSpan OfflineAfter = TimeSpan.FromMinutes(3);

	public Task<List<MeterDto>> GetMetersAsync() =>
		db.Meters.AsNoTracking()
			.OrderBy(m => m.Building!.Code).ThenBy(m => m.UnitId)
			.Select(m => new MeterDto(m.Id, m.Code, m.Name, m.Category, m.RatedKw, m.UnitId, m.IsEnabled,
				m.BuildingId, m.Building!.Code, m.Building.Name, m.Building.SpaceType))
			.ToListAsync();

	public async Task<List<LatestReadingDto>> GetLatestAsync()
	{
		var meterIds = await db.Meters.AsNoTracking().Select(m => m.Id).ToListAsync();
		// 只看最近一天，避免掃整張表；更久沒資料的電表回 null（斷線）
		var since = DateTimeOffset.UtcNow.AddDays(-1);
		var latest = await db.MeterReadings.AsNoTracking()
			.Where(r => r.SampledAt >= since)
			.GroupBy(r => r.MeterId)
			.Select(g => g.OrderByDescending(r => r.SampledAt).First())
			.ToDictionaryAsync(r => r.MeterId);

		var now = DateTimeOffset.UtcNow;
		return meterIds.Select(id => latest.TryGetValue(id, out var r)
			? new LatestReadingDto(id, r.SampledAt, now - r.SampledAt <= OfflineAfter, r.Kw, r.Kvar, r.Pf, r.DemandKw,
				(r.Vrs + r.Vst + r.Vtr) / 3, (r.Ir + r.Is + r.It) / 3, r.Hz, r.KwhTotal)
			: new LatestReadingDto(id, null, false, null, null, null, null, null, null, null, null)).ToList();
	}
}

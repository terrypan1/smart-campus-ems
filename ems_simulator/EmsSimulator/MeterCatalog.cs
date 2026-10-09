using System.Text.Json;

namespace EmsSimulator;

/// <summary>電表主檔（data/meters.json）。backend 與 simulator 共用同一份。</summary>
public sealed record MeterCatalog(CampusDef Campus, List<BuildingDef> Buildings, List<MeterDef> Meters)
{
	private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

	public static MeterCatalog Load(string path)
	{
		var catalog = JsonSerializer.Deserialize<MeterCatalog>(File.ReadAllText(path), JsonOptions)
			?? throw new InvalidOperationException($"讀不到電表主檔：{path}");

		var dup = catalog.Meters.GroupBy(m => m.UnitId).FirstOrDefault(g => g.Count() > 1);
		if (dup != null) throw new InvalidOperationException($"電表主檔 unitId 重複：{dup.Key}");
		if (catalog.Meters.Any(m => m.UnitId is < 1 or > 247)) throw new InvalidOperationException("unitId 必須在 1–247 之間");
		return catalog;
	}

	public string SpaceTypeOf(MeterDef m) => Buildings.FirstOrDefault(b => b.Code == m.Building)?.SpaceType ?? "other";
}

public sealed record CampusDef(string Code, string Name, double ContractKw);

public sealed record BuildingDef(string Code, string Name, string SpaceType);

public sealed record MeterDef(int UnitId, string Building, string Name, string Category, double RatedKw);

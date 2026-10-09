using System.Text.Json;

namespace EmsSimulator;

/// <summary>累積電度持久化：重啟後接續，不歸零。</summary>
public sealed class MeterStateStore(string path)
{
	public Dictionary<int, double> Load()
	{
		if (!File.Exists(path)) return [];
		return JsonSerializer.Deserialize<Dictionary<int, double>>(File.ReadAllText(path)) ?? [];
	}

	public void Save(IEnumerable<SimMeter> meters)
	{
		Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
		var tmp = path + ".tmp";
		File.WriteAllText(tmp, JsonSerializer.Serialize(meters.ToDictionary(m => m.Def.UnitId, m => m.Values.KwhTotal)));
		File.Move(tmp, path, overwrite: true);
	}
}

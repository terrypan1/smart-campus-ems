namespace EmsSimulator;

/// <summary>一顆模擬電表的即時狀態。每秒呼叫 <see cref="Step"/> 一次。</summary>
public sealed class SimMeter
{
	private const double NominalV = 380;
	private readonly Random _rng;
	private readonly string _spaceType;
	private readonly double[] _minuteKw = new double[15];
	private double _minuteSum;
	private int _minuteCount;
	private int _currentMinute = -1;
	private double _drift = 1;

	public MeterDef Def { get; }
	public MeterValues Values { get; } = new();

	public SimMeter(MeterDef def, string spaceType, double initialKwh, int seed)
	{
		Def = def;
		_spaceType = spaceType;
		_rng = new Random(seed);
		Values.KwhTotal = initialKwh;
	}

	public void Step(DateTime localTime, double dtSeconds)
	{
		// 負載：目標值 × 緩慢漂移 × 小雜訊
		_drift = Math.Clamp(_drift + (_rng.NextDouble() - 0.5) * 0.01, 0.9, 1.1);
		var kw = Math.Max(0, LoadModel.TargetKw(Def, _spaceType, localTime) * _drift * (1 + (_rng.NextDouble() - 0.5) * 0.04));

		FillElectrical(Values, Def, kw, _rng);
		Values.KwhTotal += kw * dtSeconds / 3600;
		Values.DemandKw = (float)UpdateDemand(localTime, kw);
	}

	/// <summary>由 kW 推算電壓、電流、kVar、kVA、PF、Hz（即時模擬與歷史資料產生共用）</summary>
	public static void FillElectrical(MeterValues values, MeterDef def, double kw, Random rng)
	{
		var pf = def.Category switch { "aircon" => 0.86, "power" => 0.82, "lighting_socket" => 0.95, "solar" => 0.99, _ => 0.92 };
		var kva = kw / pf;
		var kvar = Math.Sqrt(Math.Max(0, kva * kva - kw * kw));

		var v = NominalV * (1 + (rng.NextDouble() - 0.5) * 0.02);
		var iAvg = kva * 1000 / (Math.Sqrt(3) * v);

		values.Vrs = (float)(v * (1 + (rng.NextDouble() - 0.5) * 0.006));
		values.Vst = (float)(v * (1 + (rng.NextDouble() - 0.5) * 0.006));
		values.Vtr = (float)(v * (1 + (rng.NextDouble() - 0.5) * 0.006));
		values.Ir = (float)(iAvg * (1 + (rng.NextDouble() - 0.5) * 0.08));
		values.Is = (float)(iAvg * (1 + (rng.NextDouble() - 0.5) * 0.08));
		values.It = (float)(iAvg * (1 + (rng.NextDouble() - 0.5) * 0.08));
		values.Kw = (float)kw;
		values.Kvar = (float)kvar;
		values.Kva = (float)kva;
		values.Pf = kw > 0 ? (float)pf : 1f;
		values.Hz = (float)(60 + (rng.NextDouble() - 0.5) * 0.1);
	}

	/// <summary>需量：最近 15 分鐘的平均 kW（每分鐘一格的滑動視窗）</summary>
	private double UpdateDemand(DateTime localTime, double kw)
	{
		var minute = (int)(localTime.Ticks / TimeSpan.TicksPerMinute);
		if (minute != _currentMinute)
		{
			if (_currentMinute >= 0 && _minuteCount > 0) _minuteKw[_currentMinute % 15] = _minuteSum / _minuteCount;
			_currentMinute = minute;
			_minuteSum = 0;
			_minuteCount = 0;
		}
		_minuteSum += kw;
		_minuteCount++;

		var filled = _minuteKw.Where(x => x > 0).ToArray();
		var current = _minuteSum / _minuteCount;
		return filled.Length == 0 ? current : (filled.Sum() + current) / (filled.Length + 1);
	}
}

public sealed class MeterValues
{
	public float Vrs, Vst, Vtr, Ir, Is, It, Kw, Kvar, Kva, Pf, Hz, DemandKw;
	public double KwhTotal;
}

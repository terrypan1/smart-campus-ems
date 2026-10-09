using System.Buffers.Binary;
using System.Net;
using EmsSimulator;
using FluentModbus;

namespace EmsSimulator.Tests;

public class SimulatorTests
{
	private static readonly MeterDef Aircon = new(5, "B", "空調", "aircon", 180);
	private static readonly MeterDef Solar = new(11, "B", "太陽能", "solar", 80);
	private static readonly MeterDef Lighting = new(2, "A", "照明插座", "lighting_socket", 35);

	[Fact]
	public void 主檔可載入且unitId不重複()
	{
		var catalog = MeterCatalog.Load(Path.Combine(AppContext.BaseDirectory, "data", "meters.json"));
		Assert.Equal(51, catalog.Meters.Count);
		Assert.All(catalog.Meters, m => Assert.Contains(catalog.Buildings, b => b.Code == m.Building));
	}

	[Fact]
	public void 太陽能夜間為零_白天大於零()
	{
		Assert.Equal(0, LoadModel.TargetKw(Solar, "teaching", new DateTime(2026, 7, 1, 2, 0, 0)));
		Assert.Equal(0, LoadModel.TargetKw(Solar, "teaching", new DateTime(2026, 7, 1, 19, 0, 0)));
		Assert.True(LoadModel.TargetKw(Solar, "teaching", new DateTime(2026, 7, 1, 12, 0, 0)) > 0);
	}

	[Fact]
	public void 空調夏月平日下午高於冬季()
	{
		var summer = LoadModel.TargetKw(Aircon, "teaching", new DateTime(2026, 7, 15, 14, 0, 0));
		var winter = LoadModel.TargetKw(Aircon, "teaching", new DateTime(2026, 1, 14, 14, 0, 0));
		Assert.True(summer > winter * 3);
	}

	[Fact]
	public void 負載不為負且不超過額定太多()
	{
		var start = new DateTime(2026, 1, 1);
		foreach (var m in new[] { Aircon, Solar, Lighting })
			for (var t = start; t < start.AddYears(1); t = t.AddMinutes(37))
			{
				var kw = LoadModel.TargetKw(m, "teaching", t);
				Assert.InRange(kw, 0, m.RatedKw * 1.1);
			}
	}

	[Fact]
	public void 累積電度只增不減_且與功率積分一致()
	{
		var meter = new SimMeter(Lighting, "teaching", initialKwh: 1000, seed: 1);
		var t = new DateTime(2026, 3, 10, 0, 0, 0);
		var prev = meter.Values.KwhTotal;
		double expected = 1000;
		for (var i = 0; i < 24 * 3600; i += 10)
		{
			meter.Step(t.AddSeconds(i), 10);
			expected += meter.Values.Kw * 10 / 3600.0;
			Assert.True(meter.Values.KwhTotal >= prev);
			prev = meter.Values.KwhTotal;
		}
		Assert.Equal(expected, meter.Values.KwhTotal, 3);
		Assert.True(meter.Values.KwhTotal - 1000 > 100); // 一天至少用了一些電
	}

	[Fact]
	public void 點位編碼_float32與uint32大端序()
	{
		var v = new MeterValues { Vrs = 380.5f, Kw = 123.25f, Pf = 0.86f, KwhTotal = 4_321_214.57 };
		var buf = new byte[RegisterMap.ByteCount];
		RegisterMap.Encode(v, buf);

		Assert.Equal(380.5f, BinaryPrimitives.ReadSingleBigEndian(buf.AsSpan(0, 4)));
		Assert.Equal(123.25f, BinaryPrimitives.ReadSingleBigEndian(buf.AsSpan(24, 4)));
		Assert.Equal(0.86f, BinaryPrimitives.ReadSingleBigEndian(buf.AsSpan(36, 4)));
		Assert.Equal(432_121_457u, BinaryPrimitives.ReadUInt32BigEndian(buf.AsSpan(48, 4)));
	}

	[Fact]
	public async Task Modbus_server寫入後client依UnitId讀回相同位元組()
	{
		const int port = 15502;
		using var server = new ModbusTcpServer();
		server.AddUnit(5);
		server.AddUnit(11);
		server.Start(new IPEndPoint(IPAddress.Loopback, port));

		var v5 = new MeterValues { Kw = 100.5f, KwhTotal = 12345.67 };
		var v11 = new MeterValues { Kw = 42f, KwhTotal = 99.99 };
		lock (server.Lock)
		{
			RegisterMap.Encode(v5, server.GetHoldingRegisterBuffer(5));
			RegisterMap.Encode(v11, server.GetHoldingRegisterBuffer(11));
		}

		using var client = new ModbusTcpClient();
		client.Connect(new IPEndPoint(IPAddress.Loopback, port));
		var r5 = (await client.ReadHoldingRegistersAsync(5, 0, RegisterMap.RegisterCount)).ToArray();
		var r11 = (await client.ReadHoldingRegistersAsync(11, 0, RegisterMap.RegisterCount)).ToArray();

		Assert.Equal(100.5f, BinaryPrimitives.ReadSingleBigEndian(r5.AsSpan(24, 4)));
		Assert.Equal(1_234_567u, BinaryPrimitives.ReadUInt32BigEndian(r5.AsSpan(48, 4)));
		Assert.Equal(42f, BinaryPrimitives.ReadSingleBigEndian(r11.AsSpan(24, 4)));
		Assert.Equal(9_999u, BinaryPrimitives.ReadUInt32BigEndian(r11.AsSpan(48, 4)));
		server.Stop();
	}
}

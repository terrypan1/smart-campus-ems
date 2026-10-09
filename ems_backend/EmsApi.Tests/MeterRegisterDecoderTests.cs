using EmsApi.Services.Collector;
using EmsSimulator;

namespace EmsApi.Tests;

public class MeterRegisterDecoderTests
{
	[Fact]
	public void 契約測試_模擬器編碼後由後端解碼_數值一致()
	{
		var v = new MeterValues
		{
			Vrs = 381.2f, Vst = 379.8f, Vtr = 380.4f,
			Ir = 120.5f, Is = 118.25f, It = 121f,
			Kw = 75.5f, Kvar = 44.8f, Kva = 87.79f, Pf = 0.86f, Hz = 60.02f, DemandKw = 72.3f,
			KwhTotal = 4_321_214.567,
		};
		var buffer = new byte[RegisterMap.ByteCount];
		RegisterMap.Encode(v, buffer);

		var at = new DateTimeOffset(2026, 10, 5, 4, 0, 7, TimeSpan.Zero);
		var r = MeterRegisterDecoder.Decode(buffer, meterId: 9, at);

		Assert.Equal(9, r.MeterId);
		Assert.Equal(at, r.SampledAt);
		Assert.Equal(v.Vrs, r.Vrs);
		Assert.Equal(v.Vst, r.Vst);
		Assert.Equal(v.Vtr, r.Vtr);
		Assert.Equal(v.Ir, r.Ir);
		Assert.Equal(v.Is, r.Is);
		Assert.Equal(v.It, r.It);
		Assert.Equal(v.Kw, r.Kw);
		Assert.Equal(v.Kvar, r.Kvar);
		Assert.Equal(v.Kva, r.Kva);
		Assert.Equal(v.Pf, r.Pf);
		Assert.Equal(v.Hz, r.Hz);
		Assert.Equal(v.DemandKw, r.DemandKw);
		Assert.Equal(4_321_214.57m, r.KwhTotal); // 0.01 kWh 解析度，四捨五入
	}

	[Fact]
	public void 點位長度不足時丟出ArgumentException()
	{
		Assert.Throws<ArgumentException>(() => MeterRegisterDecoder.Decode(new byte[10], 1, DateTimeOffset.UtcNow));
	}

	[Fact]
	public void 點位數量與模擬器一致()
	{
		Assert.Equal(RegisterMap.RegisterCount, MeterRegisterDecoder.RegisterCount);
	}
}

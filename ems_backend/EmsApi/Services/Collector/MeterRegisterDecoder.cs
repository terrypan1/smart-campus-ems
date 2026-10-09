using System.Buffers.Binary;
using EmsApi.Models;

namespace EmsApi.Services.Collector;

/// <summary>
/// 電表點位解析（Holding Registers 0–25，Big-endian、高位 word 在前）。
/// 與 ems_simulator 的 RegisterMap 必須一致（見 docs/實作/01 §6.1）。
/// </summary>
public static class MeterRegisterDecoder
{
	public const ushort StartAddress = 0;
	public const ushort RegisterCount = 26;

	public static MeterReading Decode(ReadOnlySpan<byte> data, int meterId, DateTimeOffset sampledAt)
	{
		if (data.Length < RegisterCount * 2) throw new ArgumentException($"點位資料長度不足：{data.Length} bytes");

		var regs = data.ToArray();
		float F(int register) => BinaryPrimitives.ReadSingleBigEndian(regs.AsSpan(register * 2, 4));

		return new MeterReading
		{
			MeterId = meterId,
			SampledAt = sampledAt,
			Vrs = F(0),
			Vst = F(2),
			Vtr = F(4),
			Ir = F(6),
			Is = F(8),
			It = F(10),
			Kw = F(12),
			Kvar = F(14),
			Kva = F(16),
			Pf = F(18),
			Hz = F(20),
			DemandKw = F(22),
			KwhTotal = BinaryPrimitives.ReadUInt32BigEndian(regs.AsSpan(48, 4)) / 100m,
		};
	}
}

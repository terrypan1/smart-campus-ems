using System.Buffers.Binary;

namespace EmsSimulator;

/// <summary>
/// 點位表（Holding Registers，每顆電表相同）。Big-endian、高位 word 在前。
/// 與 ems_backend 的 MeterRegisterDecoder 必須一致（見 docs/實作/01 §6.1）。
/// </summary>
public static class RegisterMap
{
	public const int RegisterCount = 26;
	public const int ByteCount = RegisterCount * 2;

	public static void Encode(MeterValues v, Span<byte> buffer)
	{
		float[] floats = [v.Vrs, v.Vst, v.Vtr, v.Ir, v.Is, v.It, v.Kw, v.Kvar, v.Kva, v.Pf, v.Hz, v.DemandKw];
		for (var i = 0; i < floats.Length; i++)
			BinaryPrimitives.WriteSingleBigEndian(buffer.Slice(i * 4, 4), floats[i]);

		// 累積電度：uint32 × 0.01 kWh（float32 在幾百萬 kWh 時精度不夠）
		BinaryPrimitives.WriteUInt32BigEndian(buffer.Slice(48, 4), (uint)Math.Round(v.KwhTotal * 100));
	}
}

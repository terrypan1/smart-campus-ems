using EmsApi.Models;
using EmsApi.Services.Energy;

namespace EmsApi.Tests;

/// <summary>docs/實作/05 §6 #2</summary>
public class CarbonTests
{
	private static readonly List<EmissionFactor> Factors =
	[
		new() { Year = 2022, Factor = 0.495m },
		new() { Year = 2023, Factor = 0.494m },
		new() { Year = 2024, Factor = 0.474m },
		new() { Year = 2025, Factor = 0.467m },
	];

	[Fact]
	public void 已公告年度用公告值()
	{
		var f = CarbonService.Resolve(Factors, 2025);
		Assert.Equal(0.467m, f.Factor);
		Assert.False(f.Provisional);
		Assert.Equal(2025, f.FactorYear);
	}

	[Fact]
	public void 未公告年度暫用最近一個已公告年度並標暫用()
	{
		var f = CarbonService.Resolve(Factors, 2026);
		Assert.Equal(0.467m, f.Factor);
		Assert.True(f.Provisional);
		Assert.Equal(2025, f.FactorYear);
	}

	[Fact]
	public void 比最早年度還早時用最早的係數並標暫用()
	{
		var f = CarbonService.Resolve(Factors, 2020);
		Assert.Equal(0.495m, f.Factor);
		Assert.True(f.Provisional);
	}

	[Fact]
	public void 跨年逐月套各自年度的係數()
	{
		// 2024-12 用 113 年度、2025-01 用 114 年度：同樣 10,000 度排放不同
		Assert.Equal(4.74m, CarbonService.Ton(10_000, CarbonService.Resolve(Factors, 2024).Factor));
		Assert.Equal(4.67m, CarbonService.Ton(10_000, CarbonService.Resolve(Factors, 2025).Factor));
	}
}

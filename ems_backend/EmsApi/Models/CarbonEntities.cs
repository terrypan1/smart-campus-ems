namespace EmsApi.Models;

/// <summary>
/// 電力排碳係數（kg CO2e/kWh），經濟部能源署每年公告「公用售電業電力排碳係數」（data/能源署排碳係數/）。
/// Year 用西元年。還沒公告的年度不建資料，計算時暫用最近一個已公告年度（畫面標「暫用」）。
/// </summary>
public class EmissionFactor
{
	public int Id { get; set; }
	public int Year { get; set; }
	public decimal Factor { get; set; }
	/// <summary>來源文件</summary>
	public string Source { get; set; } = "";
	/// <summary>手動輸入的暫定值（06 系統設定可編輯）</summary>
	public bool IsProvisional { get; set; }
}

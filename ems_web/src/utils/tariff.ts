// 電價項目名稱（對應後端 TariffItems；docs/實作/03 §3）
export const SCHEME_NAME = '高壓電力 三段式（尖峰時間固定）大學（專）適用'

export const SEASON_LABEL: Record<string, string> = { summer: '夏月', nonsummer: '非夏月' }

export const ITEM_LABEL: Record<string, string> = {
  basic_regular: '經常契約',
  basic_half: '半尖峰契約',
  basic_sat_half: '週六半尖峰契約',
  basic_off: '離峰契約',
  energy_peak: '平日尖峰',
  energy_half: '平日半尖峰',
  energy_sat_half: '週六半尖峰',
  energy_off: '離峰',
}

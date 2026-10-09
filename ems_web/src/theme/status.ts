// 全站語意色：唯一來源。CSS（標籤、表格）讀 --st-* 變數，JS（ECharts）呼叫 statusColor()。
// 兩套主題「色相一致、只調亮度」：切換主題時使用者不會搞混顏色的意思。
// 原則（ISA-101 高效能 HMI）：正常狀態用低飽和色，只有需要人處理的異常才用高飽和色。

export type ThemeMode = 'light' | 'dark'

interface StatusDef {
  label: string
  /** 主色：圓點、圖表系列 [light, dark] */
  base: [string, string]
  /** 文字色：放在 soft 底上要讀得清楚。light 是白底所以用深色、dark 用淺色 */
  text: [string, string]
}

export const STATUS = {
  /** 台電時間電價時段 */
  tou: {
    peak: { label: '尖峰', base: ['#dc2626', '#f04848'], text: ['#b91c1c', '#ff8a8a'] },
    half: { label: '半尖峰', base: ['#ea580c', '#fb8a3c'], text: ['#c2410c', '#fdba74'] },
    /** 週六半尖峰：流動電費、超約都跟平日半尖峰不同（docs/實作/03 §5.1） */
    sat_half: { label: '週六半尖峰', base: ['#f59e0b', '#fbbf24'], text: ['#b45309', '#fcd34d'] },
    off: { label: '離峰', base: ['#16a34a', '#3fb96a'], text: ['#15803d', '#6fd394'] },
    /** 斷線期間的用電：知道總量、不知道是哪幾個小時（C 方案） */
    unassigned: { label: '未分時段', base: ['#94a3b8', '#5b6573'], text: ['#475569', '#b4bfcc'] },
  },
  alarm: {
    info: { label: '資訊', base: ['#0284c7', '#38a8e0'], text: ['#0369a1', '#86cdf2'] },
    warning: { label: '警告', base: ['#d97706', '#fbbf24'], text: ['#b45309', '#fcd34d'] },
    error: { label: '錯誤', base: ['#dc2626', '#f04848'], text: ['#b91c1c', '#ff8a8a'] },
    critical: { label: '嚴重', base: ['#be123c', '#ff3d6e'], text: ['#9f1239', '#ff9ab3'] },
  },
  /** 需量：即時狀態與事件等級（門檻是經常契約容量的百分比；卸載只記錄、不控制設備） */
  demand: {
    normal: { label: '正常', base: ['#16a34a', '#3fb96a'], text: ['#15803d', '#6fd394'] },
    warn: { label: '預警', base: ['#d97706', '#fbbf24'], text: ['#b45309', '#fcd34d'] },
    shed_aircon: { label: '卸載：空調（模擬）', base: ['#ea580c', '#fb8a3c'], text: ['#c2410c', '#fdba74'] },
    shed_fan: { label: '卸載：進排風（模擬）', base: ['#dc2626', '#f04848'], text: ['#b91c1c', '#ff8a8a'] },
    shed_light: { label: '卸載：照明（模擬）', base: ['#dc2626', '#f04848'], text: ['#b91c1c', '#ff8a8a'] },
    over_contract: { label: '超約', base: ['#be123c', '#ff3d6e'], text: ['#9f1239', '#ff9ab3'] },
    over: { label: '超約', base: ['#be123c', '#ff3d6e'], text: ['#9f1239', '#ff9ab3'] },
    /** 總覽燈號：任一卸載等級（關空調／進排風／照明）合併成「卸載中」 */
    shed: { label: '卸載中', base: ['#ea580c', '#fb8a3c'], text: ['#c2410c', '#fdba74'] },
  },
  /** 同期比較：用電、需量、電費、碳排增加為紅、減少為綠（節能語意） */
  trend: {
    up: { label: '增加', base: ['#dc2626', '#f04848'], text: ['#b91c1c', '#ff8a8a'] },
    down: { label: '減少', base: ['#16a34a', '#3fb96a'], text: ['#15803d', '#6fd394'] },
    flat: { label: '持平', base: ['#64748b', '#8b98a8'], text: ['#475569', '#b4bfcc'] },
  },
  conn: {
    online: { label: '連線', base: ['#16a34a', '#3fb96a'], text: ['#15803d', '#6fd394'] },
    offline: { label: '斷線', base: ['#94a3b8', '#5b6573'], text: ['#64748b', '#8a95a3'] },
  },
  /** 空間類型（用電結構） */
  space: {
    teaching: { label: '教學', base: ['#0d9488', '#2dd4bf'], text: ['#0f766e', '#5eead4'] },
    library: { label: '圖書館', base: ['#7c3aed', '#9d74f0'], text: ['#6d28d9', '#c0a6f7'] },
    office: { label: '行政研究', base: ['#0284c7', '#38a8e0'], text: ['#0369a1', '#86cdf2'] },
    dormitory: { label: '宿舍', base: ['#db2777', '#f472b6'], text: ['#be185d', '#f9a8d4'] },
    other: { label: '其他', base: ['#64748b', '#8b98a8'], text: ['#475569', '#b4bfcc'] },
  },
  /** 碳排 */
  carbon: {
    emission: { label: '排放', base: ['#475569', '#94a3b8'], text: ['#334155', '#cbd5e1'] },
    reduction: { label: '太陽能減碳', base: ['#16a34a', '#3fb96a'], text: ['#15803d', '#6fd394'] },
  },
  /** 電表用途 */
  category: {
    aircon: { label: '空調', base: ['#0284c7', '#38a8e0'], text: ['#0369a1', '#86cdf2'] },
    lighting_socket: { label: '照明插座', base: ['#7c3aed', '#9d74f0'], text: ['#6d28d9', '#c0a6f7'] },
    power: { label: '動力', base: ['#db2777', '#f472b6'], text: ['#be185d', '#f9a8d4'] },
    other: { label: '其他', base: ['#64748b', '#8b98a8'], text: ['#475569', '#b4bfcc'] },
    solar: { label: '太陽能', base: ['#ca8a04', '#eab308'], text: ['#a16207', '#fde047'] },
  },
} as const satisfies Record<string, Record<string, StatusDef>>

export type StatusGroup = keyof typeof STATUS
export type StatusKey<G extends StatusGroup> = keyof (typeof STATUS)[G]

const idx = (m: ThemeMode) => (m === 'light' ? 0 : 1)

/** 給 ECharts 用：直接拿實際色碼（canvas 不吃 CSS 變數） */
export function statusColor<G extends StatusGroup>(group: G, key: StatusKey<G>, mode: ThemeMode) {
  return (STATUS[group][key] as StatusDef).base[idx(mode)]
}

/**
 * 給大面積長條用的柔和色：主色往卡片底色混一些（色相不變、只降飽和）。
 * 時段堆疊長條如果用主色會非常刺眼，線、點、標籤仍用 statusColor。
 */
export function statusFill<G extends StatusGroup>(group: G, key: StatusKey<G>, mode: ThemeMode, amount = 0.4) {
  const base = statusColor(group, key, mode)
  const bg = mode === 'light' ? '#ffffff' : '#171717'
  const hex = (c: string) => [1, 3, 5].map((i) => parseInt(c.slice(i, i + 2), 16))
  const [a, b] = [hex(base), hex(bg)]
  return `#${a.map((v, i) => Math.round(v + (b[i] - v) * amount).toString(16).padStart(2, '0')).join('')}`
}

export function statusLabel<G extends StatusGroup>(group: G, key: string) {
  return (STATUS[group] as Record<string, StatusDef>)[key]?.label ?? key
}

/** 把整張表寫成 --st-<group>-<key>[-text|-soft|-border] 變數，主題切換時重寫一次 */
export function applyStatusVars(mode: ThemeMode) {
  const s = document.documentElement.style
  const soft = mode === 'light' ? 12 : 18
  const border = mode === 'light' ? 30 : 40
  for (const [g, defs] of Object.entries(STATUS)) {
    for (const [k, d] of Object.entries(defs as Record<string, StatusDef>)) {
      const base = d.base[idx(mode)]
      s.setProperty(`--st-${g}-${k}`, base)
      s.setProperty(`--st-${g}-${k}-text`, d.text[idx(mode)])
      s.setProperty(`--st-${g}-${k}-soft`, `color-mix(in srgb, ${base} ${soft}%, transparent)`)
      s.setProperty(`--st-${g}-${k}-border`, `color-mix(in srgb, ${base} ${border}%, transparent)`)
    }
  }
  // Quasar 自帶的語意色（color="positive" 這類）也接到這張表
  s.setProperty('--q-positive', statusColor('conn', 'online', mode))
  s.setProperty('--q-negative', statusColor('alarm', 'error', mode))
  s.setProperty('--q-warning', statusColor('alarm', 'warning', mode))
  s.setProperty('--q-info', statusColor('alarm', 'info', mode))
}

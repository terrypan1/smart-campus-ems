import type { ThemeMode } from './status'

// ECharts 畫在 canvas 上不吃 CSS 變數，圖表的中性色在這裡給實際值（與 tokens.css 的 --text-*、--divider 對齊）。
// 語意相關的系列色一律呼叫 status.ts 的 statusColor()，不要在圖表裡另寫色碼。
export const CHART = {
  light: {
    text: '#475569',
    axis: '#94a3b8',
    grid: '#edf0f3',
    tooltipBg: '#ffffff',
    tooltipBorder: '#e3e8ee',
    tooltipText: '#0f172a',
    primary: '#0d9488',
  },
  dark: {
    text: '#b4b9c2',
    axis: '#858b95',
    grid: 'rgba(255,255,255,0.08)',
    tooltipBg: '#161616',
    tooltipBorder: 'rgba(255,255,255,0.16)',
    tooltipText: '#f3f4f6',
    primary: '#2dd4bf',
  },
} satisfies Record<ThemeMode, Record<string, string>>

/** 所有圖表共用的 tooltip 樣式 */
export const tooltipStyle = (mode: ThemeMode) => ({
  backgroundColor: CHART[mode].tooltipBg,
  borderColor: CHART[mode].tooltipBorder,
  textStyle: { color: CHART[mode].tooltipText, fontSize: 12 },
})

/** 類別軸／數值軸的共用樣式 */
export const axisStyle = (mode: ThemeMode) => ({
  axisLine: { lineStyle: { color: CHART[mode].axis } },
  axisLabel: { color: CHART[mode].text },
  splitLine: { lineStyle: { color: CHART[mode].grid } },
})

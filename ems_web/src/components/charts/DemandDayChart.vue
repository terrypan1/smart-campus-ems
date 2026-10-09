<template>
  <div class="wrap">
    <v-chart class="chart" :option="option" autoresize />
    <!-- 下方色條的圖例：只列當天出現的電價時段 -->
    <div class="tou-legend">
      <span class="text-3">電價時段</span>
      <span v-for="t in tous" :key="t" class="item"><i :style="{ background: statusColor('tou', t, mode) }" />{{ statusLabel('tou', t) }}</span>
    </div>
  </div>
</template>

<script setup lang="ts">
// 需量曲線（需量＝每 15 分鐘的平均用電 kW）：
// 綠線＝全校需量（已扣太陽能），超過契約容量的點標紅；live＝進行中的 15 分鐘（虛線接到最後一個已結束的區塊）。
// 今天：「現在」之後淡灰「尚未發生」。下方色條＝台電電價時段。
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart, LineChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent, MarkLineComponent, MarkAreaComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { axisStyle, CHART, tooltipStyle } from '@/theme/chart'
import { statusColor, statusLabel } from '@/theme/status'
import { fmtNum } from '@/utils/format'
import type { Contract, DemandBlock } from '@/types/api'

use([LineChart, BarChart, GridComponent, TooltipComponent, LegendComponent, MarkLineComponent, MarkAreaComponent, CanvasRenderer])

/** 進行中的 15 分鐘：到 at 為止的平均 */
export interface LiveBlock {
  blockStart: string
  at: string
  netKw: number
}

const props = defineProps<{ blocks: DemandBlock[]; contract: Contract; live?: LiveBlock | null }>()
const { mode } = useTheme()
const TOU_ORDER = ['peak', 'half', 'sat_half', 'off'] as const
const tous = computed(() => TOU_ORDER.filter((t) => props.blocks.some((b) => b.tou === t)))

const hhmm = (iso: string | number) =>
  new Intl.DateTimeFormat('zh-TW', { timeZone: 'Asia/Taipei', hour12: false, hour: '2-digit', minute: '2-digit' }).format(new Date(iso))

const option = computed(() => {
  const m = mode.value
  const c = props.contract
  const blocks = props.blocks
  const labels = blocks.map((b) => hhmm(b.blockStart))
  const red = statusColor('demand', 'over_contract', m)
  const liveIdx = props.live ? blocks.findIndex((b) => b.blockStart === props.live!.blockStart) : -1
  // 最後一個有值的已結束區塊（虛線從這裡接到進行中的點）
  const lastIdx = blocks.reduce((a, b, i) => (b.netKw != null && (liveIdx < 0 || i < liveIdx) ? i : a), -1)

  const values = blocks.map((b) => b.netKw ?? 0).concat(props.live ? [props.live.netKw] : [])
  // 取整到 500，頂端刻度才不會跟契約容量附近的刻度疊在一起
  const yMax = Math.ceil((Math.max(c.regularKw, ...values) * 1.05) / 500) * 500

  const hLines = [
    { name: `契約容量 ${fmtNum(c.regularKw, 0)} kW`, value: c.regularKw, color: red, solid: true },
    { name: `預警 ${c.warnPct}%`, value: (c.regularKw * c.warnPct) / 100, color: statusColor('demand', 'warn', m), solid: false },
    { name: `卸載 ${c.shedAirconPct}%（模擬）`, value: (c.regularKw * c.shedAirconPct) / 100, color: statusColor('demand', 'shed_aircon', m), solid: false },
  ]

  const row = (k: string, v: string) => `<div style="display:flex;justify-content:space-between;gap:16px"><span>${k}</span><b>${v}</b></div>`
  const tooltip = (i: number) => {
    const b = blocks[i]
    const end = hhmm(Date.parse(b.blockStart) + 15 * 60_000)
    const head = `<div style="margin-bottom:4px;font-weight:600">${labels[i]}–${end} · ${statusLabel('tou', b.tou)}時段</div>`
    if (i === liveIdx && props.live) {
      const kw = props.live.netKw
      return head + row('需量', `${fmtNum(kw, 0)} kW`) + row('占契約容量', `${fmtNum((kw / c.regularKw) * 100, 1)}%`) +
        `<div style="opacity:.75;margin-top:4px">目前區間：統計至 ${hhmm(props.live.at)}，區間結束後定案</div>`
    }
    if (b.netKw == null) return head + '<div style="opacity:.75">尚未發生</div>'
    return head + row('需量', `${fmtNum(b.netKw, 0)} kW`) + row('占契約容量', `${fmtNum((b.netKw / c.regularKw) * 100, 1)}%`) +
      row('已扣除太陽能發電', `${fmtNum(b.solarKw, 0)} kW`) +
      (b.netKw > c.regularKw ? `<div style="color:${red};margin-top:4px">超約 ${fmtNum(b.netKw - c.regularKw, 0)} kW</div>` : '') +
      (b.metersOk < b.metersTotal ? `<div style="opacity:.75;margin-top:4px">${b.metersTotal - b.metersOk} 顆電表這段時間缺讀值，數字可能偏低</div>` : '')
  }

  const nowLabel = liveIdx >= 0 ? labels[liveIdx] : null
  return {
    animationDuration: 300,
    grid: [
      { left: 8, right: 16, top: 36, bottom: 48, containLabel: true },
      // 電價時段色條
      { left: 52, right: 16, bottom: 22, height: 8 },
    ],
    legend: {
      top: 0, right: 0, textStyle: { color: CHART[m].text }, itemWidth: 14, itemHeight: 8,
      data: ['需量（已扣太陽能）', '超約', ...(props.live ? ['目前區間'] : [])],
    },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'line' },
      ...tooltipStyle(m),
      formatter: (items: { dataIndex: number }[]) => tooltip(items[0].dataIndex),
    },
    xAxis: [
      { type: 'category', data: labels, gridIndex: 0, ...axisStyle(m), axisLabel: { show: false }, axisTick: { show: false } },
      { type: 'category', data: labels, gridIndex: 1, axisLine: { show: false }, axisTick: { show: false }, axisLabel: { color: CHART[m].text, interval: 7, margin: 6 } },
    ],
    yAxis: [
      { type: 'value', gridIndex: 0, name: 'kW', nameTextStyle: { color: CHART[m].axis }, ...axisStyle(m), max: yMax },
      { type: 'value', gridIndex: 1, min: 0, max: 1, show: false },
    ],
    series: [
      {
        name: '需量（已扣太陽能）',
        type: 'line',
        showSymbol: false,
        connectNulls: false,
        lineStyle: { width: 2, color: CHART[m].primary },
        itemStyle: { color: CHART[m].primary },
        data: blocks.map((b, i) => (i === liveIdx ? null : b.netKw)),
        markLine: {
          symbol: 'none',
          silent: true,
          data: [
            ...hLines.map((l) => ({
              yAxis: l.value,
              lineStyle: { color: l.color, type: l.solid ? 'solid' : 'dashed', width: l.solid ? 1.5 : 1 },
              label: { formatter: l.name, color: l.color, position: 'insideEndTop', fontSize: 11 },
            })),
            ...(nowLabel
              ? [{ xAxis: nowLabel, lineStyle: { color: CHART[m].axis, type: 'solid', width: 1 }, label: { formatter: '現在', color: CHART[m].text, position: 'end', fontSize: 11 } }]
              : []),
          ],
        },
        // 今天：現在之後淡灰
        markArea: nowLabel && liveIdx < blocks.length - 1
          ? {
              silent: true,
              itemStyle: { color: m === 'light' ? 'rgba(148,163,184,0.14)' : 'rgba(255,255,255,0.05)' },
              label: { show: true, position: 'inside', color: CHART[m].axis, fontSize: 12 },
              data: [[{ xAxis: labels[liveIdx + 1], name: '尚未發生' }, { xAxis: labels[blocks.length - 1] }]],
            }
          : undefined,
      },
      {
        // 超約的區間：連續超約連成紅線、單一區間是紅點
        name: '超約',
        type: 'line',
        showSymbol: true,
        symbolSize: 5,
        connectNulls: false,
        z: 3,
        lineStyle: { width: 3, color: red },
        itemStyle: { color: red },
        data: blocks.map((b, i) => (i !== liveIdx && b.netKw != null && b.netKw > c.regularKw ? b.netKw : null)),
      },
      ...(props.live && liveIdx >= 0
        ? [
            {
              name: '目前區間',
              type: 'line',
              z: 4,
              symbol: 'circle',
              symbolSize: (_v: unknown, p: { dataIndex: number }) => (p.dataIndex === liveIdx ? 9 : 0),
              lineStyle: { width: 2, type: 'dashed', color: CHART[m].primary },
              itemStyle: { color: props.live.netKw > c.regularKw ? red : CHART[m].primary, borderColor: m === 'light' ? '#fff' : '#171717', borderWidth: 2 },
              data: blocks.map((_b, i) => (i === liveIdx ? props.live!.netKw : i === lastIdx ? blocks[lastIdx].netKw : null)),
            },
          ]
        : []),
      {
        name: '電價時段',
        type: 'bar',
        xAxisIndex: 1,
        yAxisIndex: 1,
        barCategoryGap: 0,
        data: blocks.map((b) => ({ value: 1, itemStyle: { color: statusColor('tou', b.tou, m) } })),
      },
    ],
  }
})
</script>

<style scoped>
.wrap {
  display: flex;
  flex-direction: column;
  width: 100%;
  height: 100%;
}
.chart {
  flex: 1;
  min-height: 0;
}
.tou-legend {
  display: flex;
  justify-content: flex-end;
  gap: 14px;
  padding-top: 6px;
  font-size: 12px;
}
.tou-legend .item {
  display: inline-flex;
  align-items: center;
  gap: 5px;
  color: var(--text-2);
}
.tou-legend i {
  width: 12px;
  height: 8px;
  border-radius: 2px;
}
</style>

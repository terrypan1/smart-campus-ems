<template>
  <v-chart class="chart" :option="option" autoresize />
</template>

<script setup lang="ts">
// 每日／每月用電量：依台電電價時段堆疊（柔和色），灰色＝電表斷線期間的用電（總量已知、時段不明）。
// currentIndex＝還沒結束的今天／本月，長條淡一點、標「到目前」
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { axisStyle, CHART, tooltipStyle } from '@/theme/chart'
import { statusColor, statusFill } from '@/theme/status'
import { fmtNum } from '@/utils/format'
import type { EnergyPeriod } from '@/types/api'

use([BarChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

const props = defineProps<{ periods: EnergyPeriod[]; labels: string[]; titles: string[]; currentIndex: number }>()
const { mode } = useTheme()

const SERIES = [
  { key: 'peakKwh', tou: 'peak', name: '尖峰' },
  { key: 'halfKwh', tou: 'half', name: '半尖峰' },
  { key: 'satHalfKwh', tou: 'sat_half', name: '週六半尖峰' },
  { key: 'offKwh', tou: 'off', name: '離峰' },
  { key: 'unassignedKwh', tou: 'unassigned', name: '斷線期間' },
] as const

const option = computed(() => {
  const m = mode.value
  const row = (dot: string, label: string, v: string) =>
    `<div style="display:flex;justify-content:space-between;gap:20px"><span>${dot}${label}</span><b>${v}</b></div>`
  const dot = (c: string) => `<i style="display:inline-block;width:8px;height:8px;border-radius:2px;margin-right:6px;background:${c}"></i>`
  return {
    animationDuration: 300,
    grid: { left: 8, right: 8, top: 36, bottom: 8, containLabel: true },
    legend: { top: 0, right: 0, textStyle: { color: CHART[m].text }, itemWidth: 12, itemHeight: 8, itemGap: 14 },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow', shadowStyle: { color: m === 'light' ? 'rgba(15,23,42,0.04)' : 'rgba(255,255,255,0.05)' } },
      ...tooltipStyle(m),
      formatter: (items: { dataIndex: number }[]) => {
        const i = items[0].dataIndex
        const p = props.periods[i]
        return [
          `<div style="margin-bottom:6px;font-weight:600">${props.titles[i]}${i === props.currentIndex ? '（到目前）' : ''}</div>`,
          row('', '用電量', `${fmtNum(p.totalKwh, 0)} 度`),
          '<div style="height:4px"></div>',
          ...SERIES.filter((s) => p[s.key] > 0).map((s) => row(dot(statusFill('tou', s.tou, m)), s.name, `${fmtNum(p[s.key], 0)} 度`)),
          p.solarKwh ? row('', '太陽能發電（另計）', `${fmtNum(p.solarKwh, 0)} 度`) : '',
          p.coverage < 0.9995
            ? `<div style="opacity:.75;margin-top:6px">資料完整度 ${fmtNum(p.coverage * 100, 1)}%：有電表斷線，斷線期間的用電算進「斷線期間」</div>`
            : '',
        ].join('')
      },
    },
    xAxis: {
      type: 'category',
      data: props.labels,
      ...axisStyle(m),
      axisTick: { show: false },
      axisLabel: { color: CHART[m].text, formatter: (v: string, i: number) => (i === props.currentIndex ? `${v}\n到目前` : v) },
    },
    yAxis: { type: 'value', name: '度', nameTextStyle: { color: CHART[m].axis }, ...axisStyle(m), axisLine: { show: false } },
    series: SERIES.map((s, si) => ({
      name: s.name,
      type: 'bar',
      stack: 'kwh',
      barMaxWidth: 22,
      barCategoryGap: '38%',
      itemStyle: { color: statusFill('tou', s.tou, m), borderColor: m === 'light' ? '#ffffff' : '#171717', borderWidth: 0.5 },
      emphasis: { itemStyle: { color: statusColor('tou', s.tou, m) } },
      data: props.periods.map((p, i) => ({
        value: Math.round(p[s.key]),
        // 還沒結束的今天／本月淡一點；最上面那段圓角
        itemStyle: {
          opacity: i === props.currentIndex ? 0.5 : 1,
          borderRadius: SERIES.slice(si + 1).every((x) => !p[x.key]) ? [3, 3, 0, 0] : 0,
        },
      })),
    })),
  }
})
</script>

<style scoped>
.chart {
  width: 100%;
  height: 100%;
}
</style>

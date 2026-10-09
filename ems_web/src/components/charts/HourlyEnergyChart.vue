<template>
  <v-chart class="chart" :option="option" autoresize />
</template>

<script setup lang="ts">
// 某顆電表一天 24 小時的用電量：依電價時段上色（柔和色）；算不出來的小時畫灰色斜線欄、標「無法計算」
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, MarkAreaComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { axisStyle, CHART, tooltipStyle } from '@/theme/chart'
import { statusColor, statusFill, statusLabel } from '@/theme/status'
import { fmtNum } from '@/utils/format'
import { HOUR_STATUS_LABEL } from '@/utils/energy'
import type { HourlyEnergy } from '@/types/api'

use([BarChart, GridComponent, TooltipComponent, MarkAreaComponent, CanvasRenderer])

const props = defineProps<{ hours: HourlyEnergy[] }>()
const { mode } = useTheme()
const hh = (h: number) => `${String(h).padStart(2, '0')}:00`

const option = computed(() => {
  const m = mode.value
  const gray = statusColor('tou', 'unassigned', m)
  const missing = props.hours.filter((h) => h.status !== 'ok' && h.status !== 'pending')
  return {
    animationDuration: 300,
    grid: { left: 8, right: 8, top: 28, bottom: 8, containLabel: true },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow', shadowStyle: { color: m === 'light' ? 'rgba(15,23,42,0.04)' : 'rgba(255,255,255,0.05)' } },
      ...tooltipStyle(m),
      formatter: (items: { dataIndex: number }[]) => {
        const h = props.hours[items[0].dataIndex]
        const title = `<div style="margin-bottom:4px;font-weight:600">${hh(h.hour)}–${hh(h.hour + 1)}${h.tou ? ` · ${statusLabel('tou', h.tou)}時段` : ''}</div>`
        if (h.status !== 'ok') return title + HOUR_STATUS_LABEL[h.status]
        return title + `用電量 <b>${fmtNum(h.kwh, 2)} 度</b>`
      },
    },
    xAxis: {
      type: 'category',
      data: props.hours.map((h) => hh(h.hour)),
      ...axisStyle(m),
      axisTick: { show: false },
      axisLabel: { color: CHART[m].text, interval: 2 },
    },
    yAxis: { type: 'value', name: '度', nameTextStyle: { color: CHART[m].axis }, ...axisStyle(m), axisLine: { show: false } },
    series: [
      {
        type: 'bar',
        barMaxWidth: 20,
        barCategoryGap: '30%',
        data: props.hours.map((h) => ({
          value: h.status === 'ok' ? h.kwh : null,
          itemStyle: { color: h.tou ? statusFill('tou', h.tou, m) : gray, borderRadius: [3, 3, 0, 0] },
          emphasis: { itemStyle: { color: h.tou ? statusColor('tou', h.tou, m) : gray } },
        })),
        // 算不出來的小時：整欄灰色斜線＋「無法計算」
        markArea: {
          silent: true,
          itemStyle: { color: { image: hatch(gray), repeat: 'repeat' } as unknown as string, opacity: 0.3 },
          label: { color: CHART[m].text, fontSize: 11, position: 'insideTop', distance: 6 },
          data: missing.map((h) => [{ xAxis: hh(h.hour), name: '無法\n計算' }, { xAxis: hh(h.hour) }]),
        },
      },
    ],
  }
})

/** 斜線圖樣（canvas pattern） */
function hatch(color: string) {
  const c = document.createElement('canvas')
  c.width = c.height = 8
  const ctx = c.getContext('2d')!
  ctx.strokeStyle = color
  ctx.lineWidth = 2
  ctx.beginPath()
  ctx.moveTo(0, 8)
  ctx.lineTo(8, 0)
  ctx.stroke()
  return c
}
</script>

<style scoped>
.chart {
  width: 100%;
  height: 100%;
}
</style>

<template>
  <v-chart class="chart" :option="option" autoresize />
</template>

<script setup lang="ts">
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent, MarkLineComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { axisStyle, CHART, tooltipStyle } from '@/theme/chart'

use([BarChart, GridComponent, TooltipComponent, LegendComponent, MarkLineComponent, CanvasRenderer])

export interface BarSeries {
  name: string
  data: number[]
  color: string
}

/** 水平參考線（例：契約容量） */
export interface RefLine {
  name: string
  value: number
  color: string
}

const props = defineProps<{ categories: string[]; series: BarSeries[]; unit?: string; stacked?: boolean; lines?: RefLine[]; rotate?: number }>()
const { mode } = useTheme()

const option = computed(() => ({
  animationDuration: 300,
  grid: { left: 8, right: 16, top: 36, bottom: 8, containLabel: true },
  legend: { top: 0, right: 0, textStyle: { color: CHART[mode.value].text }, itemWidth: 12, itemHeight: 8 },
  tooltip: {
    trigger: 'axis',
    axisPointer: { type: 'shadow' },
    ...tooltipStyle(mode.value),
    valueFormatter: (v: number) => `${v.toFixed(1)} ${props.unit ?? ''}`,
  },
  xAxis: { type: 'category', data: props.categories, ...axisStyle(mode.value), axisLabel: { color: CHART[mode.value].text, interval: 0, rotate: props.rotate ?? 30, fontSize: 11 } },
  yAxis: { type: 'value', name: props.unit, nameTextStyle: { color: CHART[mode.value].axis }, ...axisStyle(mode.value) },
  series: props.series.map((s, i) => ({
    name: s.name,
    type: 'bar',
    stack: props.stacked ? 'total' : undefined,
    data: s.data,
    itemStyle: { color: s.color, borderRadius: props.stacked ? 0 : [4, 4, 0, 0] },
    barMaxWidth: 28,
    markLine:
      i === 0 && props.lines?.length
        ? {
            symbol: 'none',
            silent: true,
            data: props.lines.map((l) => ({
              yAxis: l.value,
              lineStyle: { color: l.color, type: 'dashed', width: 1.5 },
              label: { formatter: l.name, color: l.color, position: 'insideEndTop', fontSize: 11 },
            })),
          }
        : undefined,
  })),
}))
</script>

<style scoped>
.chart {
  width: 100%;
  height: 100%;
}
</style>

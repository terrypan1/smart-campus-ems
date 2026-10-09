<template>
  <v-chart class="chart" :option="option" autoresize @click="(e: { dataIndex: number }) => emit('pick', e.dataIndex)" />
</template>

<script setup lang="ts">
// 橫向排名：每列依系列堆疊（例：每棟大樓的空調／照明插座／動力／其他）；由上到下＝排名
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { BarChart } from 'echarts/charts'
import { GridComponent, TooltipComponent, LegendComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { axisStyle, CHART, tooltipStyle } from '@/theme/chart'
import { fmtNum } from '@/utils/format'

use([BarChart, GridComponent, TooltipComponent, LegendComponent, CanvasRenderer])

export interface RankSeries {
  name: string
  color: string
  data: number[]
}

const props = defineProps<{ categories: string[]; series: RankSeries[]; unit: string }>()
const emit = defineEmits<{ pick: [index: number] }>()
const { mode } = useTheme()

const option = computed(() => {
  const m = mode.value
  return {
    animationDuration: 300,
    grid: { left: 8, right: 24, top: 32, bottom: 8, containLabel: true },
    legend: { top: 0, right: 0, textStyle: { color: CHART[m].text }, itemWidth: 12, itemHeight: 8 },
    tooltip: {
      trigger: 'axis',
      axisPointer: { type: 'shadow' },
      ...tooltipStyle(m),
      valueFormatter: (v: number) => `${fmtNum(v, 0)} ${props.unit}`,
    },
    // 第一名在最上面
    yAxis: { type: 'category', inverse: true, data: props.categories, ...axisStyle(m), axisLabel: { color: CHART[m].text, fontSize: 12 } },
    xAxis: { type: 'value', name: props.unit, nameTextStyle: { color: CHART[m].axis }, ...axisStyle(m) },
    series: props.series.map((s) => ({
      name: s.name,
      type: 'bar',
      stack: 'total',
      barMaxWidth: 18,
      itemStyle: { color: s.color },
      data: s.data.map((v) => Math.round(v)),
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

<template>
  <v-chart class="chart" :option="option" autoresize @click="(e: { dataIndex: number }) => emit('pick', props.items[e.dataIndex])" />
</template>

<script setup lang="ts">
// 占比環圈圖：中間顯示合計
import { computed } from 'vue'
import VChart from 'vue-echarts'
import { use } from 'echarts/core'
import { PieChart } from 'echarts/charts'
import { TooltipComponent, LegendComponent, GraphicComponent } from 'echarts/components'
import { CanvasRenderer } from 'echarts/renderers'
import { useTheme } from '@/theme/useTheme'
import { CHART, tooltipStyle } from '@/theme/chart'
import { fmtNum } from '@/utils/format'

use([PieChart, TooltipComponent, LegendComponent, GraphicComponent, CanvasRenderer])

export interface DonutItem {
  key: string
  name: string
  value: number
  color: string
}

const props = defineProps<{ items: DonutItem[]; unit: string; centerLabel: string }>()
const emit = defineEmits<{ pick: [item: DonutItem | undefined] }>()
const { mode } = useTheme()

const option = computed(() => {
  const m = mode.value
  const total = props.items.reduce((a, b) => a + b.value, 0)
  return {
    animationDuration: 300,
    tooltip: {
      trigger: 'item',
      ...tooltipStyle(m),
      formatter: (p: { name: string; value: number; percent: number }) => `<b>${p.name}</b><br/>${fmtNum(p.value, 0)} ${props.unit} · ${fmtNum(p.percent, 1)}%`,
    },
    legend: { orient: 'vertical', right: 0, top: 'middle', textStyle: { color: CHART[m].text }, itemWidth: 12, itemHeight: 8 },
    graphic: [
      { type: 'text', left: '30%', top: '42%', style: { text: props.centerLabel, fill: CHART[m].text, fontSize: 12, textAlign: 'center' } },
      { type: 'text', left: '30%', top: '50%', style: { text: fmtNum(total, 0), fill: CHART[m].tooltipText, fontSize: 20, fontWeight: 700, textAlign: 'center' } },
    ],
    series: [
      {
        type: 'pie',
        radius: ['52%', '78%'],
        center: ['32%', '52%'],
        avoidLabelOverlap: true,
        label: { show: false },
        itemStyle: { borderColor: m === 'light' ? '#ffffff' : '#171717', borderWidth: 2 },
        data: props.items.map((i) => ({ name: i.name, value: Math.round(i.value), itemStyle: { color: i.color } })),
      },
    ],
  }
})
</script>

<style scoped>
.chart {
  width: 100%;
  height: 100%;
}
</style>

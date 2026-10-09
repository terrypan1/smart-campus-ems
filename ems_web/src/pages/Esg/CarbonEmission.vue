<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">碳排放</div>
        <div class="page-sub">外購電力的碳排放量（範疇二）＝外購電力 × 電力排碳係數；太陽能自發自用不計入 · 歷史資料為模擬產生</div>
      </div>
      <q-space />
      <q-btn outline no-caps color="primary" :icon="mdiMicrosoftExcel" label="匯出 Excel" :loading="exporting" @click="exportExcel" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="kpis q-mb-md">
      <div class="app-card kpi">
        <div class="kpi-label">{{ sum?.year }} 年累計碳排放量</div>
        <div class="kpi-value num">{{ fmtNum(sum?.emissionTon, 1) }}<small>公噸</small></div>
        <div class="kpi-sub">
          <template v-if="lastYearComparable && sum?.lastYearEmissionTon != null">
            <span class="delta" :style="{ color: `var(--st-trend-${delta! > 0 ? 'up' : 'down'}-text)` }">{{ delta! > 0 ? '▲' : '▼' }} {{ fmtNum(Math.abs(delta!), 1) }}%</span>
            比去年同期（{{ fmtNum(sum.lastYearEmissionTon, 1) }} 公噸）
          </template>
          <template v-else-if="sum">系統資料從 {{ dataStart }} 開始，去年同期不完整，暫不比較</template>
        </div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">{{ sum?.year }} 年太陽能減碳量</div>
        <div class="kpi-value num">{{ fmtNum(sum?.reductionTon, 1) }}<small>公噸</small></div>
        <div class="kpi-sub">太陽能發電 {{ fmtNum(sum?.solarKwh, 0) }} 度，自發自用</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">近 13 個月碳排放量</div>
        <div class="kpi-value num">{{ fmtNum(total13, 1) }}<small>公噸</small></div>
        <div class="kpi-sub">外購電力 {{ fmtNum(months.reduce((a, m) => a + m.netKwh, 0), 0) }} 度</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">電力排碳係數</div>
        <div class="kpi-value num">{{ sum?.factor.factor ?? '—' }}<small>公斤／度</small></div>
        <div class="kpi-sub">
          每用 1 度電約排放 {{ sum?.factor.factor ?? '—' }} 公斤（{{ sum ? sum.factor.factorYear - 1911 : '' }} 年度）
          <span v-if="sum?.factor.provisional" class="prov">{{ sum.year - 1911 }} 年度還沒公告，先用這個</span>
        </div>
      </div>
    </div>

    <div class="charts q-mb-md">
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">每月碳排放量</div>
          <span class="text-3 text-caption">外購電力 × 電力排碳係數；夏月空調用電多，排放量較高</span>
        </div>
        <div style="height: 280px">
          <BarChart v-if="months.length" :categories="chart.categories" :series="[chart.emission]" unit="公噸" :rotate="0" />
        </div>
      </div>
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">每月太陽能減碳量</div>
          <span class="text-3 text-caption">太陽能發電量 × 電力排碳係數</span>
        </div>
        <div style="height: 280px">
          <BarChart v-if="months.length" :categories="chart.short" :series="[chart.reduction]" unit="公噸" :rotate="0" />
          <div class="text-3 text-caption text-right">（月）</div>
        </div>
      </div>
    </div>

    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">每月明細</div>
        <span class="text-3 text-caption">係數標 ＊ 的月份：該年度還沒公告，先用最近一年的係數 · 匯出 Excel 有同樣的欄位</span>
      </div>
      <q-table
        class="app-table detail"
        flat
        dense
        row-key="month"
        :rows="months"
        :columns="columns"
        :pagination="{ rowsPerPage: 0, sortBy: 'month', descending: true }"
        hide-pagination
        :loading="loading"
      >
        <template #body-cell-month="p">
          <q-td :props="p">
            <span class="num">{{ p.row.month.slice(0, 4) }} 年 {{ Number(p.row.month.slice(5, 7)) }} 月</span>
            <span v-if="p.row.isPartial" class="chip-now">到目前</span>
          </q-td>
        </template>
        <template #body-cell-emissionTon="p">
          <q-td :props="p">
            <div class="em-cell">
              <b class="num">{{ fmtNum(p.row.emissionTon, 1) }}</b>
              <div class="em-bar"><div :style="{ width: `${(p.row.emissionTon / maxEmission) * 100}%` }" /></div>
            </div>
          </q-td>
        </template>
        <template #body-cell-factor="p">
          <q-td :props="p">
            <span class="num">{{ p.row.factor }}</span><span v-if="p.row.provisional" class="star">＊</span>
            <q-tooltip>{{ p.row.factorYear - 1911 }} 年度係數{{ p.row.provisional ? `（${Number(p.row.month.slice(0, 4)) - 1911} 年度還沒公告，暫用）` : '' }}</q-tooltip>
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- 怎麼算的：算式、係數、適用範圍三欄 -->
    <div class="app-card q-pa-md">
      <div class="card-head"><div class="card-title">怎麼算的</div></div>
      <div class="explain">
        <div class="box">
          <div class="sub-title">算式<span v-if="example" class="text-3">（以 {{ example.month.slice(0, 4) }} 年 {{ Number(example.month.slice(5, 7)) }} 月為例）</span></div>
          <template v-if="example">
            <div class="eq"><span class="text-2">外購電力</span><b class="num">{{ fmtNum(example.netKwh, 0) }} 度</b></div>
            <div class="eq sub"><span>用電量 {{ fmtNum(example.loadKwh, 0) }} − 太陽能 {{ fmtNum(example.solarKwh, 0) }}</span></div>
            <div class="eq"><span class="text-2">× 電力排碳係數</span><b class="num">{{ example.factor }} 公斤／度</b></div>
            <div class="eq"><span class="text-2">÷ 1,000</span><span /></div>
            <div class="eq total"><span>碳排放量</span><b class="num">{{ fmtNum(example.emissionTon, 1) }} 公噸</b></div>
          </template>
        </div>
        <div class="box">
          <div class="sub-title">電力排碳係數（經濟部能源署每年公告）</div>
          <div v-for="f in factors" :key="f.year" class="eq">
            <span class="text-2">{{ f.year - 1911 }} 年度</span><b class="num">{{ f.factor }} 公斤／度</b>
          </div>
          <div class="eq sub"><span>{{ thisYear - 1911 }} 年度還沒公告，先用 {{ factors.at(-1) ? factors.at(-1)!.year - 1911 : '' }} 年度</span></div>
          <div class="text-3 text-caption q-mt-sm">公告文件存在系統裡；新年度公告後在「系統設定 → 契約與係數」新增。</div>
        </div>
        <div class="box">
          <div class="sub-title">適用範圍</div>
          <div class="scope ok">✓ 外購電力造成的間接排放（範疇二）</div>
          <div class="scope ok">✓ 可當 ESG 報告、節能成效的參考</div>
          <div class="scope no">✕ 不含燒燃料（鍋爐、公務車）與其他間接排放</div>
          <div class="scope no">✕ 不能取代正式的溫室氣體盤查</div>
        </div>
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import type { QTableColumn } from 'quasar'
import { mdiMicrosoftExcel } from '@quasar/extras/mdi-v7'
import BarChart from '@/components/charts/BarChart.vue'
import { $exportCarbon, $getCarbonMonthly, $getCarbonSummary, $getEmissionFactors } from '@/api'
import { errorMessage } from '@/api/http'
import { statusFill } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum } from '@/utils/format'
import type { CarbonMonth, CarbonSummary, EmissionFactor } from '@/types/api'

const { mode } = useTheme()
const months = ref<CarbonMonth[]>([])
const sum = ref<CarbonSummary | null>(null)
const factors = ref<EmissionFactor[]>([])
const loading = ref(false)
const exporting = ref(false)
const error = ref('')

const total13 = computed(() => months.value.reduce((a, m) => a + m.emissionTon, 0))
const maxEmission = computed(() => Math.max(1, ...months.value.map((m) => m.emissionTon)))
/** 去年同期要從 1/1 就有資料才比較 */
const lastYearComparable = computed(() => !!sum.value?.lastYearDataFrom && sum.value.lastYearDataFrom <= sum.value.lastYearFrom)
const dataStart = computed(() => sum.value?.lastYearDataFrom?.replaceAll('-', '/') ?? '—')
const delta = computed(() =>
  sum.value?.lastYearEmissionTon ? ((sum.value.emissionTon - sum.value.lastYearEmissionTon) / sum.value.lastYearEmissionTon) * 100 : null,
)
/** 算式示範：最近一個完整的月份 */
const example = computed(() => [...months.value].reverse().find((m) => !m.isPartial) ?? null)

const thisYear = new Date().getFullYear()
const chart = computed(() => ({
  categories: months.value.map((m) => `${m.month.slice(2, 7).replace('-', '/')}${m.isPartial ? '\n到目前' : ''}`),
  // 右邊小圖：只寫月份
  short: months.value.map((m) => `${Number(m.month.slice(5, 7))}`),
  emission: { name: '碳排放量', color: statusFill('carbon', 'emission', mode.value, 0.15), data: months.value.map((m) => Math.round(m.emissionTon * 10) / 10) },
  reduction: { name: '太陽能減碳量', color: statusFill('carbon', 'reduction', mode.value, 0.15), data: months.value.map((m) => Math.round(m.reductionTon * 10) / 10) },
}))

const kwh = (v: number) => fmtNum(v, 0)
const columns: QTableColumn<CarbonMonth>[] = [
  { name: 'month', label: '月份', field: 'month', align: 'left', sortable: true },
  { name: 'loadKwh', label: '用電量（度）', field: 'loadKwh', format: kwh, classes: 'num' },
  { name: 'solarKwh', label: '太陽能發電（度）', field: 'solarKwh', format: kwh, classes: 'num' },
  { name: 'netKwh', label: '外購電力（度）', field: 'netKwh', format: kwh, classes: 'num' },
  { name: 'factor', label: '電力排碳係數（公斤／度）', field: 'factor', align: 'left' },
  { name: 'emissionTon', label: '碳排放量（公噸）', field: 'emissionTon', align: 'left', sortable: true },
  { name: 'reductionTon', label: '太陽能減碳量（公噸）', field: 'reductionTon', format: (v) => fmtNum(v, 1), classes: 'num' },
]

async function exportExcel() {
  exporting.value = true
  try {
    await $exportCarbon(13)
  } catch (e) {
    error.value = errorMessage(e, '匯出失敗')
  } finally {
    exporting.value = false
  }
}

onMounted(async () => {
  loading.value = true
  try {
    const [m, s, f] = await Promise.all([$getCarbonMonthly(13), $getCarbonSummary(), $getEmissionFactors()])
    months.value = m.data ?? []
    sum.value = s.data ?? null
    factors.value = f.data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取碳排資料失敗')
  } finally {
    loading.value = false
  }
})
</script>

<style scoped>
.kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}
.charts {
  display: grid;
  grid-template-columns: minmax(0, 2fr) minmax(0, 1fr);
  gap: 16px;
}
.explain {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .charts,
  .explain {
    grid-template-columns: minmax(0, 1fr);
  }
}
.delta {
  margin-right: 6px;
  font-weight: 600;
}
.prov {
  display: inline-block;
  margin-top: 4px;
  padding: 1px 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
}
.detail :deep(tbody td) {
  height: 40px;
}
.chip-now {
  margin-left: 6px;
  padding: 1px 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--accent-strong);
  background: var(--accent-soft);
}
.em-cell {
  display: flex;
  align-items: center;
  gap: 10px;
  min-width: 160px;
}
.em-cell b {
  min-width: 48px;
  text-align: right;
}
.em-bar {
  flex: 1;
  height: 6px;
  border-radius: 999px;
  background: var(--tile-bg);
}
.em-bar > div {
  height: 100%;
  border-radius: 999px;
  background: var(--st-carbon-emission);
}
.box {
  padding: 14px 16px;
  border-radius: var(--radius-md);
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.eq {
  display: flex;
  justify-content: space-between;
  gap: 12px;
  padding: 4px 0;
  font-size: 13px;
}
.eq.sub {
  padding-top: 0;
  font-size: 12px;
  color: var(--text-3);
}
.eq.total {
  margin-top: 6px;
  padding-top: 8px;
  font-size: 15px;
  border-top: 1px solid var(--divider);
}
.eq.total b {
  color: var(--accent-strong);
}
.scope {
  padding: 4px 0;
  font-size: 13px;
}
.scope.ok {
  color: var(--text-1);
}
.scope.no {
  color: var(--text-2);
}
.star {
  margin-left: 2px;
  color: var(--st-alarm-warning-text);
}
.sub-title {
  margin-bottom: 8px;
  font-size: 13px;
  font-weight: 600;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">電表清單</div>
        <div class="page-sub">主校區 · 每 10 秒更新 · 最後更新 {{ lastRefresh || '—' }}</div>
      </div>
      <q-space />
      <q-btn flat dense no-caps class="text-2" :icon="mdiRefresh" label="重新整理" :loading="loading" @click="load" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <!-- 摘要 -->
    <div class="kpis q-mb-md">
      <div class="app-card kpi">
        <div class="kpi-label">即時用電</div>
        <div class="kpi-value num">{{ fmtNum(summary.loadKw) }}<small>kW</small></div>
        <div class="kpi-sub">不含太陽能發電</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">15 分鐘需量</div>
        <div class="kpi-value num">{{ fmtNum(summary.demandKw) }}<small>kW</small></div>
        <div class="kpi-sub">經常契約 {{ fmtNum(contractKw, 0) }} kW · {{ contractKw ? fmtNum((summary.demandKw / contractKw) * 100) : '—' }}%</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">太陽能發電</div>
        <div class="kpi-value num">{{ fmtNum(summary.solarKw) }}<small>kW</small></div>
        <div class="kpi-sub">{{ summary.solarCount }} 座案場</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">電表通訊</div>
        <div class="kpi-value num">
          {{ summary.online }}<small>/ {{ meters.length }}</small>
        </div>
        <div class="kpi-sub">
          <StatusTag v-if="summary.offline" group="conn" k="offline" :label="`${summary.offline} 顆斷線`" />
          <StatusTag v-else group="conn" k="online" label="全部連線" />
        </div>
      </div>
    </div>

    <!-- 各大樓即時用電 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">各大樓即時用電</div>
        <span class="text-3 text-caption">依用途堆疊，不含太陽能</span>
      </div>
      <div style="height: 300px">
        <BarChart :categories="buildingChart.categories" :series="buildingChart.series" unit="kW" stacked />
      </div>
    </div>

    <!-- 電表表格 -->
    <div class="app-card q-pa-md">
      <div class="row items-center q-col-gutter-sm q-mb-md">
        <div class="col-12 col-sm-4 col-md-3">
          <q-input v-model="filter.text" dense outlined clearable placeholder="搜尋電表名稱或編號">
            <template #prepend><q-icon :name="mdiMagnify" size="18px" /></template>
          </q-input>
        </div>
        <div class="col-6 col-sm-4 col-md-3">
          <q-select v-model="filter.building" dense outlined clearable emit-value map-options :options="buildingOptions" label="大樓" />
        </div>
        <div class="col-6 col-sm-4 col-md-2">
          <q-select v-model="filter.category" dense outlined clearable emit-value map-options :options="categoryOptions" label="用途" />
        </div>
        <q-space />
        <div class="text-2 text-caption">共 {{ rows.length }} 顆</div>
      </div>

      <q-table
        class="app-table sticky"
        flat
        dense
        row-key="id"
        :rows="rows"
        :columns="columns"
        :pagination="{ rowsPerPage: 0, sortBy: 'buildingCode' }"
        hide-pagination
        :loading="loading && !meters.length"
      >
        <template #body-cell-name="p">
          <q-td :props="p">
            <div class="text-1">{{ p.row.name }}</div>
            <div class="text-3 mono" style="font-size: 11px">{{ p.row.code }} · Unit {{ p.row.unitId }}</div>
          </q-td>
        </template>
        <template #body-cell-category="p">
          <q-td :props="p"><StatusTag group="category" :k="p.row.category" /></q-td>
        </template>
        <template #body-cell-online="p">
          <q-td :props="p"><StatusTag group="conn" :k="p.row.online ? 'online' : 'offline'" /></q-td>
        </template>
      </q-table>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref } from 'vue'
import type { QTableColumn } from 'quasar'
import { mdiMagnify, mdiRefresh } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import BarChart from '@/components/charts/BarChart.vue'
import { $getContract, $getLatestReadings, $getMeters } from '@/api'
import { errorMessage } from '@/api/http'
import { STATUS, statusColor, type StatusKey } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum, fmtTime } from '@/utils/format'
import type { LatestReading, Meter, MeterCategory } from '@/types/api'

// 經常契約容量：需量管理的契約設定（docs/實作/03 §4）
const contractKw = ref(0)
const REFRESH_MS = 10_000

const { mode } = useTheme()
const meters = ref<Meter[]>([])
const latest = ref<Map<number, LatestReading>>(new Map())
const loading = ref(false)
const error = ref('')
const lastRefresh = ref('')
const filter = reactive({ text: '', building: null as string | null, category: null as MeterCategory | null })

type Row = Meter & LatestReading

const allRows = computed<Row[]>(() =>
  meters.value.map((m) => ({ ...m, ...(latest.value.get(m.id) ?? emptyReading(m.id)) })),
)

const rows = computed(() => {
  const text = (filter.text ?? '').trim().toLowerCase()
  return allRows.value.filter(
    (r) =>
      (!filter.building || r.buildingCode === filter.building) &&
      (!filter.category || r.category === filter.category) &&
      (!text || r.name.toLowerCase().includes(text) || r.code.toLowerCase().includes(text)),
  )
})

const summary = computed(() => {
  const on = allRows.value.filter((r) => r.online)
  const load = on.filter((r) => r.category !== 'solar')
  const solar = allRows.value.filter((r) => r.category === 'solar')
  return {
    online: on.length,
    offline: allRows.value.length - on.length,
    loadKw: sum(load.map((r) => r.kw)),
    demandKw: sum(load.map((r) => r.demandKw)),
    solarKw: sum(solar.filter((r) => r.online).map((r) => r.kw)),
    solarCount: solar.length,
  }
})

const LOAD_CATEGORIES = (Object.keys(STATUS.category) as MeterCategory[]).filter((c) => c !== 'solar')

const buildingChart = computed(() => {
  const buildings = [...new Map(allRows.value.map((r) => [r.buildingCode, r.buildingName])).entries()]
  return {
    categories: buildings.map(([, name]) => name.replace(/^[A-Z]+棟\s*/, '')),
    series: LOAD_CATEGORIES.map((cat) => ({
      name: STATUS.category[cat].label,
      color: statusColor('category', cat as StatusKey<'category'>, mode.value),
      data: buildings.map(([code]) => sum(allRows.value.filter((r) => r.buildingCode === code && r.category === cat && r.online).map((r) => r.kw))),
    })),
  }
})

const buildingOptions = computed(() =>
  [...new Map(meters.value.map((m) => [m.buildingCode, m.buildingName])).entries()].map(([value, label]) => ({ value, label })),
)
const categoryOptions = (Object.keys(STATUS.category) as MeterCategory[]).map((value) => ({ value, label: STATUS.category[value].label }))

const columns: QTableColumn<Row>[] = [
  { name: 'buildingCode', label: '大樓', field: 'buildingName', align: 'left', sortable: true, sort: (_a, _b, x, y) => x.buildingCode.localeCompare(y.buildingCode) || x.unitId - y.unitId },
  { name: 'name', label: '電表', field: 'name', align: 'left', sortable: true },
  { name: 'category', label: '用途', field: 'category', align: 'left', sortable: true },
  { name: 'kw', label: 'kW', field: 'kw', format: (v) => fmtNum(v), sortable: true, classes: 'num' },
  { name: 'kvar', label: 'kVar', field: 'kvar', format: (v) => fmtNum(v), sortable: true, classes: 'num' },
  { name: 'pf', label: 'PF', field: 'pf', format: (v) => fmtNum(v, 2), classes: 'num' },
  { name: 'demandKw', label: '需量 kW', field: 'demandKw', format: (v) => fmtNum(v), sortable: true, classes: 'num' },
  { name: 'vavg', label: '電壓 V', field: 'vavg', format: (v) => fmtNum(v), classes: 'num' },
  { name: 'iavg', label: '電流 A', field: 'iavg', format: (v) => fmtNum(v), classes: 'num' },
  { name: 'kwhTotal', label: '累積電度 kWh', field: 'kwhTotal', format: (v) => fmtNum(v, 2), sortable: true, classes: 'num' },
  { name: 'sampledAt', label: '最後讀值', field: 'sampledAt', format: (v) => fmtTime(v), classes: 'mono text-2' },
  { name: 'online', label: '通訊', field: 'online', align: 'center', sortable: true },
]

function sum(values: (number | null)[]) {
  return values.reduce<number>((a, v) => a + (v ?? 0), 0)
}

function emptyReading(meterId: number): LatestReading {
  return { meterId, sampledAt: null, online: false, kw: null, kvar: null, pf: null, demandKw: null, vavg: null, iavg: null, hz: null, kwhTotal: null }
}

async function load() {
  loading.value = true
  try {
    const [m, r, c] = await Promise.all([meters.value.length ? null : $getMeters(), $getLatestReadings(), contractKw.value ? null : $getContract()])
    if (m) meters.value = m.data ?? []
    if (c) contractKw.value = c.data?.regularKw ?? 0
    latest.value = new Map((r.data ?? []).map((x) => [x.meterId, x]))
    lastRefresh.value = new Date().toLocaleTimeString('zh-TW', { hour12: false, timeZone: 'Asia/Taipei' })
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取電表資料失敗')
  } finally {
    loading.value = false
  }
}

let timer = 0
onMounted(() => {
  load()
  timer = window.setInterval(load, REFRESH_MS)
})
onUnmounted(() => clearInterval(timer))
</script>

<style scoped>
.kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

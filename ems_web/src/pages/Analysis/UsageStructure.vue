<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">用電結構</div>
        <div class="page-sub">依空間類型、大樓、用途的用電占比與排名 · 不含太陽能 · 歷史資料為模擬產生</div>
      </div>
      <q-space />
      <q-btn-toggle
        v-model="mode2"
        unelevated
        no-caps
        toggle-color="primary"
        :toggle-text-color="mode === 'dark' ? 'black' : 'white'"
        class="level-toggle"
        :options="[{ label: '單月', value: 'month' }, { label: '期間', value: 'range' }]"
      />
      <q-select v-if="mode2 === 'month'" v-model="month" dense outlined emit-value map-options :options="monthOptions" label="月份" style="min-width: 150px" />
      <template v-else>
        <q-input v-model="range.from" dense outlined type="date" label="起" style="width: 160px" />
        <q-input v-model="range.to" dense outlined type="date" label="迄" style="width: 160px" />
      </template>
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="kpis q-mb-md">
      <div class="app-card kpi">
        <div class="kpi-label">期間用電</div>
        <div class="kpi-value num">{{ fmtNum(byBuilding?.totalKwh, 0) }}<small>kWh</small></div>
        <div class="kpi-sub">
          <template v-if="totalDelta != null">
            <span class="delta" :style="{ color: `var(--st-trend-${totalDelta > 0 ? 'up' : 'down'}-text)` }">{{ totalDelta > 0 ? '▲' : '▼' }} {{ fmtNum(Math.abs(totalDelta), 1) }}%</span>
            去年同期 {{ fmtNum(byBuilding?.lastYearTotalKwh, 0) }} kWh
          </template>
          <template v-else>去年同期沒有資料</template>
        </div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">用電最多的空間類型</div>
        <div class="kpi-value">{{ bySpace?.rows[0] ? statusLabel('space', bySpace.rows[0].key) : '—' }}</div>
        <div class="kpi-sub">占 {{ fmtNum((bySpace?.rows[0]?.share ?? 0) * 100, 1) }}%</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">用電最多的大樓</div>
        <div class="kpi-value">{{ byBuilding?.rows[0]?.label ?? '—' }}</div>
        <div class="kpi-sub">{{ fmtNum(byBuilding?.rows[0]?.kwh, 0) }} kWh · 占 {{ fmtNum((byBuilding?.rows[0]?.share ?? 0) * 100, 1) }}%</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">太陽能發電（另計）</div>
        <div class="kpi-value num">{{ fmtNum(byBuilding?.solarKwh, 0) }}<small>kWh</small></div>
        <div class="kpi-sub">{{ rangeLabel }}</div>
      </div>
    </div>

    <div class="grid2 q-mb-md">
      <div class="app-card q-pa-md">
        <div class="card-head"><div class="card-title">空間類型</div></div>
        <div style="height: 260px">
          <DonutChart v-if="spaceItems.length" :items="spaceItems" unit="kWh" center-label="kWh" />
        </div>
      </div>
      <div class="app-card q-pa-md">
        <div class="card-head"><div class="card-title">用途</div></div>
        <div style="height: 260px">
          <DonutChart v-if="categoryItems.length" :items="categoryItems" unit="kWh" center-label="kWh" />
        </div>
      </div>
    </div>

    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">大樓排名</div>
        <span class="text-3 text-caption">依用途堆疊；點大樓看該棟各電表</span>
      </div>
      <div :style="{ height: `${Math.max(240, (byBuilding?.rows.length ?? 0) * 26 + 50)}px` }">
        <RankBarChart v-if="byBuilding" :categories="rank.categories" :series="rank.series" unit="kWh" @pick="(i) => drill(byBuilding!.rows[i])" />
      </div>
    </div>

    <div class="app-card q-pa-md">
      <div class="card-head"><div class="card-title">大樓明細</div></div>
      <q-table
        class="app-table"
        flat
        dense
        row-key="key"
        :rows="byBuilding?.rows ?? []"
        :columns="columns"
        :pagination="{ rowsPerPage: 0 }"
        hide-pagination
        :loading="loading"
        @row-click="(_e, r) => drill(r)"
      >
        <template #body-cell-spaceType="p">
          <q-td :props="p"><StatusTag group="space" :k="p.row.spaceType" /></q-td>
        </template>
        <template #body-cell-delta="p">
          <q-td :props="p">
            <span v-if="pct(p.row) != null" :style="{ color: `var(--st-trend-${pct(p.row)! > 0 ? 'up' : 'down'}-text)` }">
              {{ pct(p.row)! > 0 ? '▲' : '▼' }} {{ fmtNum(Math.abs(pct(p.row)!), 1) }}%
            </span>
            <span v-else class="text-3">—</span>
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- 下鑽：某棟各電表 -->
    <q-dialog v-model="detail.open">
      <q-card style="width: 760px; max-width: 94vw">
        <q-card-section class="row items-center">
          <div class="text-h6">{{ detail.title }}</div>
          <q-space />
          <q-btn flat round dense :icon="mdiClose" v-close-popup />
        </q-card-section>
        <q-card-section class="q-pt-none">
          <q-table class="app-table" flat dense row-key="key" :rows="detail.rows" :columns="meterColumns" :pagination="{ rowsPerPage: 0 }" hide-pagination :loading="detail.loading">
            <template #body-cell-category="p">
              <q-td :props="p"><StatusTag group="category" :k="Object.keys(p.row.byCategory)[0] as MeterCategory" /></q-td>
            </template>
          </q-table>
        </q-card-section>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import type { QTableColumn } from 'quasar'
import { mdiClose } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import DonutChart from '@/components/charts/DonutChart.vue'
import RankBarChart from '@/components/charts/RankBarChart.vue'
import { $getStructure } from '@/api'
import { errorMessage } from '@/api/http'
import { statusColor, statusLabel, type StatusKey } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum } from '@/utils/format'
import { addDays, addMonths, taipeiToday } from '@/utils/energy'
import type { MeterCategory, Structure, StructureRow } from '@/types/api'

const CATEGORIES = ['aircon', 'lighting_socket', 'power', 'other'] as const

const { mode } = useTheme()
const today = taipeiToday()
const mode2 = ref<'month' | 'range'>('month')
// 預設上個月（完整的一個月）
const month = ref(addMonths(today, -1))
const range = reactive({ from: addDays(today, -30), to: addDays(today, -1) })
const monthOptions = Array.from({ length: 13 }, (_, i) => {
  const v = addMonths(today, -i)
  return { value: v, label: `${v.slice(0, 7).replace('-', ' 年 ')} 月${i === 0 ? '（至今）' : ''}` }
})

const byBuilding = ref<Structure | null>(null)
const bySpace = ref<Structure | null>(null)
const byCategory = ref<Structure | null>(null)
const loading = ref(false)
const error = ref('')

const query = computed(() =>
  mode2.value === 'month' ? { level: 'month' as const, from: month.value, to: month.value } : { level: 'day' as const, from: range.from, to: range.to },
)
const rangeLabel = computed(() => (byBuilding.value ? `${byBuilding.value.from} ～ ${byBuilding.value.level === 'month' ? `${byBuilding.value.to.slice(0, 7)} 月底` : byBuilding.value.to}` : ''))
const totalDelta = computed(() => {
  const s = byBuilding.value
  return s?.lastYearTotalKwh ? ((s.totalKwh - s.lastYearTotalKwh) / s.lastYearTotalKwh) * 100 : null
})
const pct = (r: StructureRow) => (r.lastYearKwh ? ((r.kwh - r.lastYearKwh) / r.lastYearKwh) * 100 : null)

const spaceItems = computed(() =>
  (bySpace.value?.rows ?? []).map((r) => ({ key: r.key, name: statusLabel('space', r.key), value: r.kwh, color: statusColor('space', r.key as StatusKey<'space'>, mode.value) })),
)
const categoryItems = computed(() =>
  (byCategory.value?.rows ?? []).map((r) => ({ key: r.key, name: statusLabel('category', r.key), value: r.kwh, color: statusColor('category', r.key as StatusKey<'category'>, mode.value) })),
)
const rank = computed(() => {
  const rows = byBuilding.value?.rows ?? []
  return {
    categories: rows.map((r) => r.label),
    series: CATEGORIES.map((c) => ({ name: statusLabel('category', c), color: statusColor('category', c, mode.value), data: rows.map((r) => r.byCategory[c] ?? 0) })),
  }
})

const kwh = (v: number | null) => fmtNum(v, 0)
const columns: QTableColumn<StructureRow>[] = [
  { name: 'rank', label: '排名', field: 'rank', align: 'left' },
  { name: 'label', label: '大樓', field: 'label', align: 'left' },
  { name: 'spaceType', label: '空間類型', field: 'spaceType', align: 'left' },
  { name: 'kwh', label: '用電 kWh', field: 'kwh', format: kwh, classes: 'num', sortable: true },
  { name: 'share', label: '占比', field: 'share', format: (v) => `${fmtNum(v * 100, 1)}%`, classes: 'num' },
  ...CATEGORIES.map((c) => ({ name: c, label: statusLabel('category', c), field: (r: StructureRow) => r.byCategory[c] ?? 0, format: kwh, classes: 'num' })),
  { name: 'lastYearKwh', label: '去年同期', field: 'lastYearKwh', format: kwh, classes: 'num' },
  { name: 'delta', label: '增減', field: 'lastYearKwh', align: 'right' },
]
const meterColumns: QTableColumn<StructureRow>[] = [
  { name: 'label', label: '電表', field: 'label', align: 'left' },
  { name: 'category', label: '用途', field: 'key', align: 'left' },
  { name: 'kwh', label: '用電 kWh', field: 'kwh', format: kwh, classes: 'num' },
  { name: 'share', label: '占該棟', field: 'share', format: (v) => `${fmtNum(v * 100, 1)}%`, classes: 'num' },
  { name: 'lastYearKwh', label: '去年同期', field: 'lastYearKwh', format: kwh, classes: 'num' },
]

async function load() {
  const q = query.value
  if (!q.from || !q.to) return
  loading.value = true
  try {
    const [b, s, c] = await Promise.all([
      $getStructure(q.level, q.from, q.to, 'building'),
      $getStructure(q.level, q.from, q.to, 'spaceType'),
      $getStructure(q.level, q.from, q.to, 'category'),
    ])
    byBuilding.value = b.data ?? null
    bySpace.value = s.data ?? null
    byCategory.value = c.data ?? null
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取用電結構失敗')
  } finally {
    loading.value = false
  }
}

const detail = reactive({ open: false, loading: false, title: '', rows: [] as StructureRow[] })
async function drill(r: StructureRow | undefined) {
  if (!r?.buildingId) return
  const q = query.value
  Object.assign(detail, { open: true, loading: true, title: `${r.label} · 各電表`, rows: [] })
  try {
    detail.rows = (await $getStructure(q.level, q.from, q.to, 'meter', r.buildingId)).data?.rows ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取電表明細失敗')
  } finally {
    detail.loading = false
  }
}

watch(query, load, { deep: true })
onMounted(load)
</script>

<style scoped>
.kpis {
  display: grid;
  grid-template-columns: repeat(4, minmax(0, 1fr));
  gap: 16px;
}
.grid2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .grid2 {
    grid-template-columns: minmax(0, 1fr);
  }
}
.kpi-value:not(.num) {
  font-size: 22px;
}
.level-toggle {
  border: 1px solid var(--card-border);
  border-radius: 8px;
}
.delta {
  margin-right: 6px;
  font-weight: 600;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

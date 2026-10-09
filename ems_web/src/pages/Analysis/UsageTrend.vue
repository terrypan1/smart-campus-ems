<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">用電趨勢</div>
        <div class="page-sub">{{ scopeName }}的用電量（太陽能發電另外列）· 歷史資料為模擬產生</div>
      </div>
      <q-space />
      <q-select v-model="buildingId" dense outlined emit-value map-options :options="buildingOptions" label="範圍" style="min-width: 200px" />
      <q-btn-toggle
        v-model="level"
        unelevated
        no-caps
        toggle-color="primary"
        :toggle-text-color="mode === 'dark' ? 'black' : 'white'"
        class="level-toggle"
        :options="[{ label: '每日（近 31 天）', value: 'day' }, { label: '每月（近 13 個月）', value: 'month' }]"
      />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <!-- 摘要 -->
    <div class="kpis q-mb-md">
      <div class="app-card kpi">
        <div class="kpi-label">期間用電量</div>
        <div class="kpi-value num">{{ fmtNum(sum.total, 0) }}<small>度</small></div>
        <div class="kpi-sub">{{ rangeLabel }}</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">各電價時段占比</div>
        <div class="tou-bar q-mt-sm">
          <span v-for="t in TOU_KEYS" :key="t.key" :style="{ flex: sum[t.key] || 0.0001, background: statusFill('tou', t.tou, mode) }" />
        </div>
        <div class="tou-legend">
          <span v-for="t in TOU_KEYS.filter((x) => sum[x.key] > 0)" :key="t.key">
            <i :style="{ background: statusFill('tou', t.tou, mode) }" />{{ t.name }} <b class="num">{{ pct(sum[t.key]) }}</b>
          </span>
        </div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">斷線期間的用電</div>
        <div class="kpi-value num">{{ fmtNum(sum.unassigned, 0) }}<small>度</small></div>
        <div class="kpi-sub">電表斷線時，知道總共用了多少，但分不出是哪個時段</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">資料完整度</div>
        <div class="kpi-value num">{{ fmtNum(sum.coverage * 100, 1) }}<small>%</small></div>
        <div class="kpi-sub">有讀值、算得出用電量的小時比例</div>
      </div>
    </div>

    <!-- 趨勢圖 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">{{ level === 'day' ? '每日用電量' : '每月用電量' }}</div>
        <span class="text-3 text-caption">依台電電價時段分色；灰色是電表斷線期間的用電（總量已知、時段不明）</span>
      </div>
      <div style="height: 340px">
        <EnergyTrendChart v-if="periods.length" :periods="periods" :labels="labels" :titles="titles" :current-index="currentIndex" />
        <div v-else class="full-height column items-center justify-center text-3">{{ loading ? '讀取中…' : '這段期間沒有資料' }}</div>
      </div>
    </div>

    <!-- 明細表 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">{{ level === 'day' ? '每日明細' : '每月明細' }}</div>
        <span class="text-3 text-caption">單位：度；「—」表示 0</span>
      </div>
      <q-table
        class="app-table sticky detail"
        flat
        dense
        row-key="periodStart"
        :rows="tableRows"
        :columns="columns"
        :pagination="{ rowsPerPage: 0, sortBy: 'periodStart', descending: true }"
        hide-pagination
        :loading="loading"
      >
        <template #header-cell="p">
          <q-th :props="p">
            <i v-if="TOU_BY_FIELD[p.col.name]" class="hdot" :style="{ background: statusFill('tou', TOU_BY_FIELD[p.col.name], mode) }" />{{ p.col.label }}
          </q-th>
        </template>
        <template #body-cell-periodStart="p">
          <q-td :props="p">
            <span class="num">{{ periodText(p.row.periodStart) }}</span>
            <span v-if="level === 'day'" class="text-3 q-ml-xs">{{ weekday(p.row.periodStart) }}</span>
            <span v-if="isCurrent(p.row.periodStart)" class="chip-now">到目前</span>
          </q-td>
        </template>
        <template #body-cell-totalKwh="p">
          <q-td :props="p">
            <div class="total-cell">
              <b class="num">{{ fmtNum(p.row.totalKwh, 0) }}</b>
              <div class="split">
                <span v-for="t in SPLIT" :key="t.key" :style="{ flex: p.row[t.field] || 0, background: statusFill('tou', t.tou, mode) }" />
              </div>
            </div>
          </q-td>
        </template>
        <template #body-cell-quality="p">
          <q-td :props="p">
            <span v-if="isCurrent(p.row.periodStart)" class="text-3">—</span>
            <span v-else-if="p.row.coverage >= 0.9995 && !p.row.incompleteMeters" class="ok">完整</span>
            <span v-else class="warn">
              {{ fmtNum(p.row.coverage * 100, 1) }}%
              <q-tooltip max-width="300px">{{ qualityTip(p.row) }}</q-tooltip>
            </span>
          </q-td>
        </template>
      </q-table>
    </div>

    <!-- 每小時明細 -->
    <div class="app-card q-pa-md">
      <div class="card-head">
        <div class="card-title">單一電表的每小時用電</div>
        <span class="text-3 text-caption">用整點前後 20 分鐘內的讀值計算；找不到讀值的小時不補值，標成「無法計算」</span>
      </div>
      <div class="row items-center q-col-gutter-sm q-mb-sm">
        <div class="col-12 col-sm-5 col-md-4">
          <q-select v-model="hourlyMeterId" dense outlined emit-value map-options :options="meterOptions" label="電表" options-dense />
        </div>
        <div class="col-6 col-sm-3 col-md-2">
          <q-input v-model="hourlyDate" dense outlined type="date" label="日期" />
        </div>
        <div class="col-12 col-md row items-center q-gutter-xs">
          <span class="text-3 text-caption q-mr-xs">看斷線範例</span>
          <q-chip v-for="e in EXAMPLES" :key="e.label" dense clickable outline :label="e.label" @click="pickExample(e)" />
        </div>
      </div>
      <div class="hour-summary q-mb-xs">
        <span>當天合計 <b class="num">{{ fmtNum(hourSum, 1) }}</b> 度</span>
        <span v-if="finalizedCount && okCount === finalizedCount" class="ok">{{ finalizedCount }} 小時都算得出來</span>
        <span v-else-if="finalizedCount" class="warn">{{ finalizedCount - okCount }} 小時無法計算（合計不含這些小時）</span>
      </div>
      <div style="height: 260px">
        <HourlyEnergyChart v-if="hours.length" :hours="hours" />
      </div>
      <div v-if="missingHours.length" class="missing q-mt-sm">
        <div v-for="h in missingHours" :key="h.hour" class="text-2 text-caption">
          <span class="num miss-hour">{{ String(h.hour).padStart(2, '0') }}:00</span>{{ HOUR_STATUS_LABEL[h.status] }}
        </div>
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import type { QTableColumn } from 'quasar'
import EnergyTrendChart from '@/components/charts/EnergyTrendChart.vue'
import HourlyEnergyChart from '@/components/charts/HourlyEnergyChart.vue'
import { $getEnergySummary, $getHourlyEnergy, $getMeters } from '@/api'
import { errorMessage } from '@/api/http'
import { fmtNum } from '@/utils/format'
import { HOUR_STATUS_LABEL, addDays, addMonths, taipeiToday } from '@/utils/energy'
import { statusFill } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import type { EnergyPeriod, HourlyEnergy, Meter, Tou } from '@/types/api'

const TOU_KEYS = [
  { key: 'peak', tou: 'peak', name: '尖峰' },
  { key: 'half', tou: 'half', name: '半尖峰' },
  { key: 'satHalf', tou: 'sat_half', name: '週六半尖峰' },
  { key: 'off', tou: 'off', name: '離峰' },
] as const
/** 明細表裡每列的時段比例條 */
const SPLIT = [
  { key: 'peak', field: 'peakKwh', tou: 'peak' },
  { key: 'half', field: 'halfKwh', tou: 'half' },
  { key: 'satHalf', field: 'satHalfKwh', tou: 'sat_half' },
  { key: 'off', field: 'offKwh', tou: 'off' },
  { key: 'unassigned', field: 'unassignedKwh', tou: 'unassigned' },
] as const
const TOU_BY_FIELD: Record<string, Tou | 'unassigned'> = {
  peakKwh: 'peak', halfKwh: 'half', satHalfKwh: 'sat_half', offKwh: 'off', unassignedKwh: 'unassigned',
}

// 02 安排的斷線事件（docs/實作/02 §3.2），點了直接跳到那一天
const EXAMPLES = [
  { label: 'R 棟 3/12', unitId: 42, date: '2026-03-12' },
  { label: '全校 7/8', unitId: 2, date: '2026-07-08' },
  { label: '圖書館 9/20', unitId: 34, date: '2026-09-20' },
]
const WEEK = ['週日', '週一', '週二', '週三', '週四', '週五', '週六']

// 深色主題的 primary 是淺灰白，選中的切換鈕要用黑字（Quasar 預設白字會看不見）
const { mode } = useTheme()
const level = ref<'day' | 'month'>('day')
const buildingId = ref<number | null>(null)
const meters = ref<Meter[]>([])
const periods = ref<EnergyPeriod[]>([])
const loading = ref(false)
const error = ref('')

const today = taipeiToday()
const range = computed(() =>
  level.value === 'day' ? { from: addDays(today, -30), to: today } : { from: addMonths(today, -12), to: today },
)
const rangeLabel = computed(() => `${range.value.from} ～ ${range.value.to}（到目前）`)

const buildingOptions = computed(() => [
  { value: null, label: '全校' },
  ...[...new Map(meters.value.map((m) => [m.buildingId, m.buildingName])).entries()].map(([value, label]) => ({ value, label })),
])
const scopeName = computed(() => buildingOptions.value.find((o) => o.value === buildingId.value)?.label ?? '全校')

const weekday = (d: string) => WEEK[new Date(`${d}T00:00:00Z`).getUTCDay()]
const periodText = (d: string) => (level.value === 'day' ? d.slice(5).replace('-', '/') : `${d.slice(0, 4)} 年 ${Number(d.slice(5, 7))} 月`)
const labels = computed(() =>
  periods.value.map((p) => (level.value === 'day' ? p.periodStart.slice(5).replace('-', '/') : p.periodStart.slice(2, 7).replace('-', '/'))),
)
const titles = computed(() =>
  periods.value.map((p) => (level.value === 'day' ? `${p.periodStart.replaceAll('-', '/')}（${weekday(p.periodStart)}）` : periodText(p.periodStart))),
)
/** 今天／本月還沒結束 */
const isCurrent = (periodStart: string) => (level.value === 'day' ? periodStart === today : periodStart.slice(0, 7) === today.slice(0, 7))
const currentIndex = computed(() => periods.value.findIndex((p) => isCurrent(p.periodStart)))

const sum = computed(() => {
  const s = periods.value.reduce(
    (a, p) => ({
      total: a.total + p.totalKwh,
      peak: a.peak + p.peakKwh,
      half: a.half + p.halfKwh,
      satHalf: a.satHalf + p.satHalfKwh,
      off: a.off + p.offKwh,
      unassigned: a.unassigned + p.unassignedKwh,
      cov: a.cov + p.coverage,
    }),
    { total: 0, peak: 0, half: 0, satHalf: 0, off: 0, unassigned: 0, cov: 0 },
  )
  return { ...s, coverage: periods.value.length ? s.cov / periods.value.length : 0 }
})
const pct = (v: number) => {
  const measured = sum.value.peak + sum.value.half + sum.value.satHalf + sum.value.off
  return measured ? `${fmtNum((v / measured) * 100, 0)}%` : '—'
}

function qualityTip(r: EnergyPeriod) {
  const parts = [`有讀值、算得出來的小時占 ${fmtNum(r.coverage * 100, 1)}%；斷線期間的用電 ${fmtNum(r.unassignedKwh, 0)} 度算在灰色。`]
  if (r.incompleteMeters) parts.push(`${r.incompleteMeters} 顆電表在${level.value === 'day' ? '這天' : '這個月'}開始或結束時剛好斷線，只算得到有讀值的部分，用電量會偏低。`)
  return parts.join('')
}

const tableRows = computed(() => periods.value)
const dash = (v: number) => (v ? fmtNum(v, 0) : '—')
const columns = computed<QTableColumn<EnergyPeriod>[]>(() => [
  { name: 'periodStart', label: level.value === 'day' ? '日期' : '月份', field: 'periodStart', align: 'left', sortable: true },
  { name: 'totalKwh', label: '用電量', field: 'totalKwh', align: 'left', sortable: true },
  { name: 'peakKwh', label: '尖峰', field: 'peakKwh', format: dash, classes: 'num' },
  { name: 'halfKwh', label: '半尖峰', field: 'halfKwh', format: dash, classes: 'num' },
  { name: 'satHalfKwh', label: '週六半尖峰', field: 'satHalfKwh', format: dash, classes: 'num' },
  { name: 'offKwh', label: '離峰', field: 'offKwh', format: dash, classes: 'num' },
  { name: 'unassignedKwh', label: '斷線期間', field: 'unassignedKwh', format: dash, classes: 'num' },
  { name: 'solarKwh', label: '太陽能發電', field: 'solarKwh', format: dash, classes: 'num text-2' },
  { name: 'quality', label: '資料', field: 'coverage', align: 'center' },
])

async function loadSummary() {
  loading.value = true
  try {
    const res = await $getEnergySummary(level.value, range.value.from, range.value.to, buildingId.value)
    periods.value = res.data ?? []
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取用電資料失敗')
  } finally {
    loading.value = false
  }
}

/* ---------- 每小時明細 ---------- */
const hourlyMeterId = ref<number | null>(null)
const hourlyDate = ref(addDays(today, -1))
const hours = ref<HourlyEnergy[]>([])
const meterOptions = computed(() => meters.value.map((m) => ({ value: m.id, label: `${m.buildingName} · ${m.name}` })))
const missingHours = computed(() => hours.value.filter((h) => h.status !== 'ok' && h.status !== 'pending'))
const okCount = computed(() => hours.value.filter((h) => h.status === 'ok').length)
const finalizedCount = computed(() => hours.value.filter((h) => h.status !== 'pending').length)
const hourSum = computed(() => hours.value.reduce((a, h) => a + (h.kwh ?? 0), 0))

async function loadHourly() {
  if (!hourlyMeterId.value || !hourlyDate.value) return
  try {
    const res = await $getHourlyEnergy(hourlyMeterId.value, hourlyDate.value)
    hours.value = res.data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取每小時資料失敗')
  }
}

function pickExample(e: (typeof EXAMPLES)[number]) {
  hourlyMeterId.value = meters.value.find((m) => m.unitId === e.unitId)?.id ?? hourlyMeterId.value
  hourlyDate.value = e.date
}

watch([level, buildingId], loadSummary)
watch([hourlyMeterId, hourlyDate], loadHourly)

onMounted(async () => {
  try {
    meters.value = (await $getMeters()).data ?? []
    hourlyMeterId.value = meters.value.find((m) => m.category !== 'solar')?.id ?? null
  } catch (e) {
    error.value = errorMessage(e, '讀取電表清單失敗')
  }
  loadSummary()
})
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
.tou-bar {
  display: flex;
  height: 10px;
  margin: 14px 0 10px;
  overflow: hidden;
  border-radius: 999px;
  gap: 2px;
}
.tou-legend {
  display: flex;
  flex-wrap: wrap;
  gap: 4px 12px;
  font-size: 12px;
  color: var(--text-2);
}
.tou-legend i,
.hdot {
  display: inline-block;
  width: 8px;
  height: 8px;
  margin-right: 5px;
  border-radius: 2px;
  vertical-align: 0;
}
.level-toggle {
  border: 1px solid var(--card-border);
  border-radius: 8px;
}
/* 明細表 */
.detail :deep(tbody td) {
  height: 40px;
}
.total-cell {
  display: flex;
  align-items: center;
  gap: 12px;
  min-width: 190px;
}
.total-cell b {
  min-width: 64px;
  text-align: right;
}
.split {
  display: flex;
  flex: 1;
  height: 6px;
  overflow: hidden;
  border-radius: 999px;
  background: var(--tile-bg);
  gap: 1px;
}
.chip-now {
  margin-left: 6px;
  padding: 1px 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--accent-strong);
  background: var(--accent-soft);
}
.ok {
  font-size: 12px;
  color: var(--st-conn-online-text);
}
.warn {
  font-size: 12px;
  font-weight: 600;
  color: var(--st-alarm-warning-text);
  cursor: help;
}
/* 每小時 */
.hour-summary {
  display: flex;
  gap: 16px;
  align-items: baseline;
  font-size: 13px;
}
.missing {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.miss-hour {
  display: inline-block;
  width: 48px;
  font-weight: 600;
  color: var(--text-1);
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

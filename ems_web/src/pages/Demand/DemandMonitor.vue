<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">需量監控</div>
        <div class="page-sub">需量＝每 15 分鐘的平均用電（kW），已扣除太陽能發電；最高需量超過契約容量即為超約 · 每 30 秒更新 · 歷史資料為模擬產生</div>
      </div>
      <q-space />
      <q-btn flat dense no-caps class="text-2" :icon="mdiRefresh" label="重新整理" :loading="loading" @click="loadAll" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="top q-mb-md">
      <!-- 目前需量 -->
      <div class="app-card q-pa-md realtime">
        <div class="card-head">
          <div class="card-title">目前需量 <span class="text-3 text-caption text-weight-regular">本需量區間開始到現在的平均用電</span></div>
          <StatusTag v-if="rt" group="demand" :k="rt.level ?? 'normal'" :label="levelLabel" />
        </div>
        <template v-if="rt">
          <div class="row items-end q-gutter-x-md">
            <div class="big num">{{ fmtNum(rt.netKw, 0) }}<small>kW</small></div>
            <div class="text-2 q-pb-sm">
              契約容量的 <b class="num text-1">{{ fmtNum(usage, 0) }}%</b>
            </div>
          </div>
          <div class="gauge q-mt-md">
            <div class="gauge-fill" :style="{ width: `${Math.min(usage, 110) / 1.1}%`, background: `var(--st-demand-${rt.level ?? 'normal'})` }" />
            <div
              v-for="t in ticks"
              :key="t.label"
              class="gauge-tick"
              :style="{ left: `${t.pct / 1.1}%`, borderColor: `var(--st-demand-${t.k})` }"
              :title="`${t.label} ${fmtNum(t.kw, 0)} kW`"
            />
          </div>
          <div class="gauge-scale text-3">
            <span v-for="t in ticks" :key="t.label" :style="{ left: `${t.pct / 1.1}%` }">{{ t.short }}</span>
          </div>
          <div class="facts q-mt-lg">
            <div><span class="text-3">需量區間</span><span>{{ blockLabel }}，已過 {{ elapsed }}</span></div>
            <div><span class="text-3">目前時段</span><span><StatusTag group="tou" :k="rt.tou" /></span></div>
            <div><span class="text-3">瞬時功率</span><span>{{ fmtNum(rt.instantNetKw, 0) }} kW</span></div>
            <div><span class="text-3">本時段契約容量</span><span>{{ fmtNum(rt.capacityKw, 0) }} kW</span></div>
            <div><span class="text-3">全校負載／太陽能發電</span><span>{{ fmtNum(rt.loadKw, 0) }} ／ {{ fmtNum(rt.solarKw, 0) }} kW</span></div>
            <div><span class="text-3">電表連線</span><span>{{ rt.metersOnline }} / {{ rt.metersTotal }}</span></div>
          </div>
        </template>
        <div v-else class="text-3 q-py-lg">{{ loading ? '讀取中…' : '沒有資料' }}</div>
      </div>

      <!-- 本月最高需量 -->
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">本月各時段最高需量</div>
          <span class="text-3 text-caption">{{ thisMonth?.month.slice(0, 7) }}</span>
        </div>
        <template v-if="thisMonth">
          <div v-for="t in thisMonth.byTou" :key="t.tou" class="touline">
            <StatusTag group="tou" :k="t.tou" />
            <div class="touline-bar">
              <div :style="{ width: `${Math.min(t.kw / t.capacityKw, 1.1) * 90}%`, background: `var(--st-tou-${t.tou})` }" />
            </div>
            <div class="num text-right">
              <b>{{ fmtNum(t.kw, 0) }}</b> kW
              <div class="text-3 text-caption">{{ fmtDay(t.at) }}</div>
            </div>
          </div>
          <div class="text-2 text-caption q-mt-sm">
            <template v-if="thisMonth.overKw > 0">
              <StatusTag group="demand" k="over_contract" :label="`最多超約 ${fmtNum(thisMonth.overKw, 0)} kW`" />
              <span class="q-ml-sm">本月超約 {{ thisMonth.overBlocks }} 個需量區間</span>
            </template>
            <span v-else>本月尚未超約</span>
          </div>
        </template>
        <div v-else class="text-3 q-py-lg">本月還沒有資料</div>
      </div>

      <!-- 契約設定 -->
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">契約容量與預警值</div>
          <q-btn v-if="isAdmin && contract" flat dense no-caps color="primary" :icon="mdiPencil" label="修改" @click="openEdit" />
        </div>
        <template v-if="contract">
          <div class="facts">
            <div><span class="text-3">經常契約</span><span><b class="num">{{ fmtNum(contract.regularKw, 0) }}</b> kW</span></div>
            <div><span class="text-3">半尖峰契約</span><span>{{ fmtNum(contract.halfPeakKw, 0) }} kW</span></div>
            <div><span class="text-3">週六半尖峰契約</span><span>{{ fmtNum(contract.satHalfKw, 0) }} kW</span></div>
            <div><span class="text-3">離峰契約</span><span>{{ fmtNum(contract.offPeakKw, 0) }} kW</span></div>
          </div>
          <q-separator class="q-my-sm" />
          <div class="facts">
            <div v-for="t in ticks.slice(0, 4)" :key="t.label">
              <span class="text-3"><StatusTag group="demand" :k="t.k" :label="t.label" /></span>
              <span>{{ t.pct }}% · {{ fmtNum(t.kw, 0) }} kW</span>
            </div>
          </div>
          <div class="text-3 text-caption q-mt-sm">
            預警值與卸載值以經常契約容量的百分比設定；卸載為模擬，只記錄不實際控制設備。最後修改：{{ fmtTime(contract.updatedAt) }}（{{ contract.updatedBy }}）
          </div>
        </template>
      </div>
    </div>

    <!-- 需量曲線 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">需量曲線</div>
        <q-space />
        <q-btn flat dense round size="sm" :icon="mdiChevronLeft" @click="dayDate = addDays(dayDate, -1)" />
        <q-input v-model="dayDate" dense outlined type="date" style="width: 160px" />
        <q-btn flat dense round size="sm" :icon="mdiChevronRight" :disable="dayDate >= today" @click="dayDate = addDays(dayDate, 1)" />
      </div>
      <div class="text-3 text-caption q-mb-xs">
        每 15 分鐘一點，已扣除太陽能發電；下方色條是時間電價時段
        <template v-if="partialBlocks"> · {{ partialBlocks }} 個需量區間有電表缺讀值，需量可能偏低</template>
      </div>
      <div style="height: 340px">
        <DemandDayChart v-if="dayBlocks.length && contract" :blocks="dayBlocks" :contract="contract" :live="dayDate === today && rt ? { blockStart: rt.blockStart, at: rt.at, netKw: rt.netKw } : null" />
      </div>
      <div class="text-2 text-caption q-mt-xs">
        當日最高需量 <b class="num">{{ fmtNum(dayMax?.netKw, 0) }}</b> kW
        <template v-if="dayMax">（{{ fmtHm(dayMax.blockStart) }}，{{ statusLabel('tou', dayMax.tou) }}）</template>
      </div>
    </div>

    <!-- 近 13 個月 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">近 13 個月最高需量</div>
        <span class="text-3 text-caption">依時間電價時段；紅色虛線為契約容量。夏月（5/16–10/15）才有尖峰時段</span>
      </div>
      <div style="height: 300px">
        <BarChart v-if="monthly.length" :categories="monthlyChart.categories" :series="monthlyChart.series" :lines="monthlyChart.lines" unit="kW" :rotate="0" />
      </div>
    </div>

    <!-- 提醒與超約紀錄 -->
    <div class="app-card q-pa-md">
      <div class="card-head">
        <div class="card-title">近 30 天需量警報</div>
        <span class="text-3 text-caption">「即時」為每分鐘偵測；「歷史」為重算歷史資料時補記的超約</span>
      </div>
      <q-table
        class="app-table sticky"
        flat
        dense
        row-key="id"
        :rows="events"
        :columns="eventColumns"
        :pagination="{ rowsPerPage: 10 }"
        :loading="loading"
        no-data-label="近 30 天沒有需量警報"
      >
        <template #body-cell-level="p">
          <q-td :props="p"><StatusTag group="demand" :k="p.row.level" /></q-td>
        </template>
      </q-table>
    </div>

    <!-- 修改契約 -->
    <ContractDialog v-model="editOpen" :contract="contract" @saved="onSaved" />
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import type { QTableColumn } from 'quasar'
import { mdiChevronLeft, mdiChevronRight, mdiPencil, mdiRefresh } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import BarChart from '@/components/charts/BarChart.vue'
import DemandDayChart from '@/components/charts/DemandDayChart.vue'
import ContractDialog from '@/components/demand/ContractDialog.vue'
import { $getContract, $getDemandDay, $getDemandEvents, $getMonthlyDemand, $getRealtimeDemand } from '@/api'
import { errorMessage } from '@/api/http'
import { auth } from '@/stores/auth'
import { statusColor, statusLabel } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum, fmtTime } from '@/utils/format'
import { addDays, taipeiToday } from '@/utils/energy'
import type { Contract, DemandBlock, DemandEvent, DemandLevel, MonthlyDemand, RealtimeDemand, Tou } from '@/types/api'

const REFRESH_MS = 30_000
const TOUS: Tou[] = ['peak', 'half', 'sat_half', 'off']

const { mode } = useTheme()
const isAdmin = computed(() => auth.user?.role === 'admin')
const today = taipeiToday()

const rt = ref<RealtimeDemand | null>(null)
const contract = ref<Contract | null>(null)
const monthly = ref<MonthlyDemand[]>([])
const events = ref<DemandEvent[]>([])
const dayBlocks = ref<DemandBlock[]>([])
const dayDate = ref(today)
const loading = ref(false)
const error = ref('')

const tz = { timeZone: 'Asia/Taipei', hour12: false } as const
const fmtHm = (iso: string) => new Date(iso).toLocaleTimeString('zh-TW', { ...tz, hour: '2-digit', minute: '2-digit' })
const fmtDay = (iso: string) => new Date(iso).toLocaleString('zh-TW', { ...tz, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })

/* ---------- 即時 ---------- */
const usage = computed(() => (rt.value && contract.value ? (rt.value.netKw / contract.value.regularKw) * 100 : 0))
const levelLabel = computed(() => (rt.value?.level ? statusLabel('demand', rt.value.level) : '正常'))
const blockLabel = computed(() => (rt.value ? `${fmtHm(rt.value.blockStart)}–${fmtHm(new Date(Date.parse(rt.value.blockStart) + 15 * 60_000).toISOString())}` : ''))
const elapsed = computed(() => {
  const s = rt.value?.elapsedSeconds ?? 0
  return `${Math.floor(s / 60)} 分 ${String(s % 60).padStart(2, '0')} 秒`
})

const ticks = computed(() => {
  const c = contract.value
  if (!c) return []
  const t: { k: DemandLevel; label: string; short: string; pct: number }[] = [
    { k: 'warn', label: '預警', short: `${c.warnPct}%`, pct: c.warnPct },
    { k: 'shed_aircon', label: '卸載：空調', short: `${c.shedAirconPct}%`, pct: c.shedAirconPct },
    { k: 'shed_fan', label: '卸載：進排風', short: '', pct: c.shedFanPct },
    { k: 'shed_light', label: '卸載：照明', short: '', pct: c.shedLightPct },
    { k: 'over_contract', label: '契約容量', short: '100%', pct: 100 },
  ]
  return t.map((x) => ({ ...x, kw: (c.regularKw * x.pct) / 100 }))
})

/* ---------- 日曲線 ---------- */
const dayMax = computed(() =>
  dayBlocks.value.reduce<DemandBlock | null>((a, b) => (b.netKw != null && (a?.netKw == null || b.netKw > a.netKw) ? b : a), null),
)
const partialBlocks = computed(() => dayBlocks.value.filter((b) => b.netKw != null && b.metersOk < b.metersTotal).length)

/* ---------- 13 個月 ---------- */
const thisMonth = computed(() => monthly.value.find((m) => m.month.slice(0, 7) === today.slice(0, 7)) ?? null)
const monthlyChart = computed(() => ({
  categories: monthly.value.map((m) => m.month.slice(2, 7).replace('-', '/')),
  series: TOUS.map((t) => ({
    name: statusLabel('tou', t),
    color: statusColor('tou', t, mode.value),
    data: monthly.value.map((m) => m.byTou.find((x) => x.tou === t)?.kw ?? 0),
  })),
  lines: contract.value
    ? [{ name: `契約容量 ${fmtNum(contract.value.regularKw, 0)} kW`, value: contract.value.regularKw, color: statusColor('demand', 'over_contract', mode.value) }]
    : [],
}))

const eventColumns: QTableColumn<DemandEvent>[] = [
  { name: 'occurredAt', label: '時間', field: 'occurredAt', align: 'left', format: (v) => fmtTime(v), sortable: true },
  { name: 'level', label: '等級', field: 'level', align: 'left' },
  { name: 'demandKw', label: '需量', field: 'demandKw', format: (v) => `${fmtNum(v, 0)} kW`, classes: 'num' },
  { name: 'pct', label: '占契約容量', field: (r) => r.demandKw / r.contractKw, format: (v) => `${fmtNum(v * 100, 1)}%`, classes: 'num' },
  { name: 'thresholdKw', label: '設定值', field: 'thresholdKw', format: (v) => `${fmtNum(v, 0)} kW`, classes: 'num' },
  { name: 'source', label: '來源', field: 'source', align: 'left', format: (v) => (v === 'live' ? '即時' : '歷史') },
]

/* ---------- 讀取 ---------- */
async function loadRealtime() {
  try {
    rt.value = (await $getRealtimeDemand()).data ?? null
    if (rt.value) contract.value = rt.value.contract
  } catch (e) {
    error.value = errorMessage(e, '讀取目前需量失敗')
  }
}
async function loadDay() {
  try {
    dayBlocks.value = (await $getDemandDay(dayDate.value)).data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取需量曲線失敗')
  }
}
async function loadAll() {
  loading.value = true
  error.value = ''
  try {
    const [m, ev, c] = await Promise.all([$getMonthlyDemand(13), $getDemandEvents(), $getContract()])
    monthly.value = m.data ?? []
    events.value = ev.data ?? []
    contract.value = c.data ?? null
    await Promise.all([loadRealtime(), loadDay()])
  } catch (e) {
    error.value = errorMessage(e, '讀取需量資料失敗')
  } finally {
    loading.value = false
  }
}

/* ---------- 修改契約（admin） ---------- */
const editOpen = ref(false)
const openEdit = () => (editOpen.value = true)
function onSaved(c: Contract) {
  contract.value = c
  // 背景補記通常幾秒內完成
  setTimeout(loadAll, 3000)
}

watch(dayDate, loadDay)

let timer = 0
onMounted(() => {
  loadAll()
  timer = window.setInterval(() => {
    loadRealtime()
    if (dayDate.value === today) loadDay()
  }, REFRESH_MS)
})
onUnmounted(() => clearInterval(timer))
</script>

<style scoped>
.top {
  display: grid;
  grid-template-columns: minmax(0, 1.3fr) minmax(0, 1fr) minmax(0, 1fr);
  gap: 16px;
}
@media (max-width: 1279px) {
  .top {
    grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  }
  .realtime {
    grid-column: 1 / -1;
  }
}
@media (max-width: 767px) {
  .top {
    grid-template-columns: minmax(0, 1fr);
  }
}
.big {
  font-size: 44px;
  font-weight: 700;
  line-height: 1;
}
.big small {
  margin-left: 6px;
  font-size: 16px;
  font-weight: 500;
  color: var(--text-2);
}
.gauge {
  position: relative;
  height: 14px;
  border-radius: 999px;
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.gauge-fill {
  height: 100%;
  border-radius: 999px;
  transition: width 0.4s;
}
.gauge-tick {
  position: absolute;
  top: -4px;
  bottom: -4px;
  border-left: 2px solid;
}
.gauge-scale {
  position: relative;
  height: 16px;
  font-size: 11px;
}
.gauge-scale span {
  position: absolute;
  transform: translateX(-50%);
  margin-top: 4px;
}
.facts {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 8px 16px;
  font-size: 13px;
}
.facts > div {
  display: flex;
  flex-direction: column;
  gap: 2px;
}
.facts .text-3 {
  font-size: 12px;
}
.touline {
  display: grid;
  grid-template-columns: 92px minmax(0, 1fr) 84px;
  align-items: center;
  gap: 10px;
  padding: 6px 0;
  border-bottom: 1px solid var(--divider);
}
.touline-bar {
  position: relative;
  height: 8px;
  border-radius: 999px;
  background: var(--tile-bg);
}
.touline-bar > div {
  height: 100%;
  border-radius: 999px;
}
/* 90% 寬的位置＝契約容量 */
.touline-bar::after {
  content: '';
  position: absolute;
  left: 90%;
  top: -3px;
  bottom: -3px;
  border-left: 2px solid var(--st-demand-over_contract);
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

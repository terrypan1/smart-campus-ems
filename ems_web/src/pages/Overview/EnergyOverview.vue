<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">用電總覽</div>
        <div class="page-sub">主校區 · 歷史資料為模擬產生</div>
      </div>
      <q-space />
      <div class="row items-center q-gutter-sm">
        <span class="chip clickable" @click="go('Monitor', 'MeterList')">電表 {{ online }}/{{ rt?.metersTotal ?? '—' }} 連線</span>
        <span class="chip clickable" :class="{ warn: (ov?.system.unackAlarms ?? 0) > 0 }" @click="go('Alarm', 'AlarmEvents')">
          {{ ov?.system.unackAlarms ? `${ov.system.unackAlarms} 筆告警未確認` : '沒有未確認的告警' }}
        </span>
        <span class="live" :class="{ stale }">
          <span class="live-dot" />{{ stale ? '連線中斷' : '即時' }} · {{ updatedText }}
          <q-tooltip>每 10 秒更新；電表每分鐘回傳一次讀值</q-tooltip>
        </span>
      </div>
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <!-- 第一層：現在、今天 -->
    <div class="tier1 q-mb-md">
      <div class="app-card q-pa-lg now-card clickable" :class="`is-${light}`" @click="go('Demand', 'DemandMonitor')">
        <div class="row items-center no-wrap">
          <div class="card-title">目前需量</div>
          <span class="text-3 text-caption q-ml-sm">本需量區間開始到現在的平均用電</span>
          <q-space />
          <StatusTag v-if="rt" group="demand" :k="light" :label="LIGHT_LABEL[light]" class="status-tag" />
        </div>
        <template v-if="rt && contract">
          <div class="row items-end q-gutter-x-lg q-mt-sm">
            <div class="big"><AnimatedNumber :value="rt.netKw" /><small>kW</small></div>
            <div class="text-2 q-pb-sm">契約容量 <b class="num text-1">{{ fmtNum(contract.regularKw, 0) }}</b> kW 的 <b class="num text-1">{{ fmtNum(usage, 0) }}%</b></div>
          </div>
          <div class="gauge q-mt-md">
            <div class="gauge-fill" :style="{ width: `${Math.min(usage, 110) / 1.1}%`, background: `var(--st-demand-${light})` }" />
            <div v-for="t in ticks" :key="t.pct" class="gauge-tick" :style="{ left: `${t.pct / 1.1}%`, borderColor: `var(--st-demand-${t.k})` }" />
          </div>
          <div class="gauge-scale text-3">
            <span v-for="t in ticks" :key="t.pct" :style="{ left: `${t.pct / 1.1}%` }">{{ t.label }}</span>
          </div>
          <div class="sentence q-mt-md">{{ nowSentence }}</div>
          <div class="text-3 text-caption q-mt-xs">
            需量計算區間 {{ blockLabel }}（統計至 {{ fmtHm(rt.at) }}）· 已扣除太陽能發電 {{ fmtNum(rt.solarKw, 0) }} kW · {{ statusLabel('tou', rt.tou) }}時段
          </div>
        </template>
        <div v-else class="text-3 q-py-lg">{{ loading ? '讀取中…' : '沒有即時資料' }}</div>
      </div>

      <div class="app-card q-pa-lg clickable today-card" :class="{ 'is-over': todayOverCount > 0 }" @click="go('Demand', 'DemandMonitor')">
        <div class="card-title">今日最高需量</div>
        <template v-if="todayMax">
          <div class="mid q-mt-sm"><AnimatedNumber :value="todayMax.kw" :show-delta="false" /><small>kW</small></div>
          <div class="text-2">{{ todayMax.when }}</div>
          <div class="sentence q-mt-sm" :class="{ 'text-over': todayMax.kw > (contract?.regularKw ?? Infinity) }">{{ todayMaxSentence }}</div>
          <div class="today-stats q-mt-lg">
            <div>
              <div class="text-3 text-caption">超約</div>
              <div class="stat num" :class="{ 'text-over': todayOverCount > 0 }">{{ todayOverCount }}<small>次</small></div>
            </div>
            <div>
              <div class="text-3 text-caption">預警</div>
              <div class="stat num">{{ ov?.today.events.warn ?? 0 }}<small>次</small></div>
            </div>
            <div>
              <div class="text-3 text-caption">卸載（模擬）</div>
              <div class="stat num">{{ shedEvents }}<small>次</small></div>
            </div>
          </div>
          <div class="text-3 text-caption q-mt-sm">次數以 15 分鐘需量區間計</div>
        </template>
        <div v-else class="text-3 q-py-lg">{{ loading ? '讀取中…' : '今天還沒有資料' }}</div>
      </div>
    </div>

    <!-- 第二層：今天的走勢 -->
    <div class="tier2 q-mb-md">
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">今日需量曲線</div>
          <span class="text-3 text-caption">需量＝每 15 分鐘的平均用電（kW），已扣除太陽能發電；當月最高需量超過契約容量即產生超約附加費</span>
        </div>
        <div style="height: 370px">
          <DemandDayChart v-if="dayBlocks.length && contract" :blocks="dayBlocks" :contract="contract" :live="liveBlock" />
        </div>
      </div>
      <div class="column q-gutter-y-md no-wrap">
        <div class="app-card q-pa-md clickable" @click="go('Monitor', 'MeterList')">
          <div class="card-head">
            <div class="card-title">大樓即時負載排行</div>
            <span class="text-3 text-caption">不含太陽能</span>
          </div>
          <div v-for="(b, i) in topBuildings" :key="b.name" class="rank">
            <span class="rank-no num">{{ i + 1 }}</span>
            <div class="col">
              <div class="row justify-between">
                <span>{{ b.name }}</span>
                <span class="num"><b>{{ fmtNum(b.kw, 0) }}</b> kW <span class="text-3">· 占 {{ fmtNum(b.share, 0) }}%</span></span>
              </div>
              <div class="rank-bar"><div :style="{ width: `${(b.kw / (topBuildings[0]?.kw || 1)) * 100}%` }" /></div>
            </div>
          </div>
          <div v-if="!topBuildings.length" class="text-3">沒有即時資料</div>
        </div>
        <div class="app-card q-pa-md clickable col" @click="go('Demand', 'DemandMonitor')">
          <div class="card-head">
            <div class="card-title">今日需量警報</div>
            <span class="text-3 text-caption">{{ events.length ? `${events.length} 次` : '' }}</span>
          </div>
          <div v-for="e in events.slice(0, 5)" :key="e.id" class="event">
            <span class="num text-2">{{ fmtHm(e.occurredAt) }}</span>
            <StatusTag group="demand" :k="e.level" :label="EVENT_LABEL[e.level]" />
            <span class="num">{{ fmtNum(e.demandKw, 0) }} kW <span class="text-3">· 契約的 {{ fmtNum((e.demandKw / e.contractKw) * 100, 0) }}%</span></span>
          </div>
          <div v-if="!events.length" class="text-3">今日尚無需量警報</div>
        </div>
      </div>
    </div>

    <!-- 第三層：用電、電費、碳排（對去年同期） -->
    <div class="tier3">
      <div v-for="k in kpis" :key="k.label" class="app-card kpi clickable" @click="go(k.to[0], k.to[1])">
        <div class="kpi-label">{{ k.label }}<span v-if="k.trial" class="trial">試算</span></div>
        <div class="kpi-value num">{{ k.value }}<small>{{ k.unit }}</small></div>
        <div class="kpi-sub">
          <template v-if="k.cmp">
            <span v-if="k.cmp.pct != null" class="delta" :style="{ color: `var(--st-trend-${k.cmp.dir}-text)` }">
              {{ k.cmp.dir === 'up' ? '▲' : k.cmp.dir === 'down' ? '▼' : '—' }} {{ fmtNum(Math.abs(k.cmp.pct), 1) }}%
            </span>
            <span>{{ k.cmp.text }}</span>
            <q-tooltip v-if="k.cmp.tip" max-width="320px">{{ k.cmp.tip }}</q-tooltip>
          </template>
          <span v-else>{{ k.sub }}</span>
        </div>
      </div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRouter } from 'vue-router'
import StatusTag from '@/components/status/StatusTag.vue'
import AnimatedNumber from '@/components/status/AnimatedNumber.vue'
import DemandDayChart from '@/components/charts/DemandDayChart.vue'
import { $getDemandDay, $getDemandEvents, $getLatestReadings, $getMeters, $getOverview, $getRealtimeDemand } from '@/api'
import { errorMessage } from '@/api/http'
import { statusLabel } from '@/theme/status'
import { fmtNum } from '@/utils/format'
import { taipeiToday } from '@/utils/energy'
import { pagePath } from '@/router/menu'
import type { Contract, DemandBlock, DemandEvent, DemandLevel, LatestReading, Meter, Overview, RealtimeDemand } from '@/types/api'

const LIVE_MS = 10_000
const DAY_MS = 60_000
const SUMMARY_MS = 5 * 60_000

const LIGHT_LABEL = { normal: '正常', warn: '預警', shed: '卸載中（模擬）', over: '超約' } as const
const EVENT_LABEL: Record<DemandLevel, string> = {
  warn: '預警', shed_aircon: '卸載：空調（模擬）', shed_fan: '卸載：進排風（模擬）', shed_light: '卸載：照明（模擬）', over_contract: '超約',
}

const router = useRouter()
const go = (category: string, key: string) => router.push(pagePath(category, key))

const today = taipeiToday()
const rt = ref<RealtimeDemand | null>(null)
const contract = ref<Contract | null>(null)
const ov = ref<Overview | null>(null)
const dayBlocks = ref<DemandBlock[]>([])
const events = ref<DemandEvent[]>([])
const meters = ref<Meter[]>([])
const latest = ref<LatestReading[]>([])
const loading = ref(false)
const error = ref('')

const tz = { timeZone: 'Asia/Taipei', hour12: false } as const
const fmtHm = (iso: string | number) => new Date(iso).toLocaleTimeString('zh-TW', { ...tz, hour: '2-digit', minute: '2-digit' })

/* ---------- 即時燈號 ---------- */
const lastOk = ref(0)
const nowMs = ref(Date.now())
const stale = computed(() => !!lastOk.value && nowMs.value - lastOk.value > 60_000)
const updatedText = computed(() => {
  if (!lastOk.value) return '讀取中'
  const s = Math.max(0, Math.round((nowMs.value - lastOk.value) / 1000))
  return s < 5 ? '剛剛更新' : `${s} 秒前更新`
})
const online = computed(() => rt.value?.metersOnline ?? '—')

/* ---------- 現在 ---------- */
const usage = computed(() => (rt.value && contract.value ? (rt.value.netKw / contract.value.regularKw) * 100 : 0))
/** 燈號：正常／預警（≥ 預警值）／卸載中（≥ 卸載值）／超約 */
const light = computed<'normal' | 'warn' | 'shed' | 'over'>(() => {
  const l = rt.value?.level
  if (!l) return 'normal'
  if (l === 'over_contract') return 'over'
  return l === 'warn' ? 'warn' : 'shed'
})
const nowSentence = computed(() => {
  const r = rt.value
  const c = contract.value
  if (!r || !c) return ''
  const gap = c.regularKw - r.netKw
  if (gap < 0) return `超約 ${fmtNum(-gap, 0)} kW；本區間結束時需量仍高於契約容量，本月即產生超約附加費`
  return `距契約容量尚餘 ${fmtNum(gap, 0)} kW`
})
const ticks = computed(() => {
  const c = contract.value
  if (!c) return []
  return [
    { k: 'warn', pct: c.warnPct, label: `預警 ${c.warnPct}%` },
    { k: 'shed', pct: c.shedAirconPct, label: `卸載 ${c.shedAirconPct}%` },
    { k: 'over', pct: 100, label: '契約 100%' },
  ]
})
const blockLabel = computed(() => {
  if (!rt.value) return ''
  const s = Date.parse(rt.value.blockStart)
  return `${fmtHm(s)}–${fmtHm(s + 15 * 60_000)}`
})
/** 曲線上的進行中區塊 */
const liveBlock = computed(() => (rt.value ? { blockStart: rt.value.blockStart, at: rt.value.at, netKw: rt.value.netKw } : null))

/* ---------- 今天（跟曲線同一份資料：已結束的 15 分鐘＋進行中） ---------- */
const todayMax = computed(() => {
  let best: { kw: number; when: string } | null = null
  for (const b of dayBlocks.value) {
    if (b.netKw == null || (rt.value && b.blockStart === rt.value.blockStart)) continue
    if (!best || b.netKw > best.kw) best = { kw: b.netKw, when: `${fmtHm(b.blockStart)}–${fmtHm(Date.parse(b.blockStart) + 15 * 60_000)}` }
  }
  if (rt.value && (!best || rt.value.netKw > best.kw)) best = { kw: rt.value.netKw, when: `${blockLabel.value}（進行中）` }
  return best
})
const todayMaxSentence = computed(() => {
  const t = todayMax.value
  const c = contract.value
  if (!t || !c) return ''
  const pct = fmtNum((t.kw / c.regularKw) * 100, 0)
  return t.kw > c.regularKw ? `超約 ${fmtNum(t.kw - c.regularKw, 0)} kW（契約容量的 ${pct}%）` : `契約容量的 ${pct}%，未超約`
})
const todayOverCount = computed(() => {
  const c = contract.value
  if (!c) return 0
  return dayBlocks.value.filter((b) => b.netKw != null && b.netKw > c.regularKw && b.blockStart !== rt.value?.blockStart).length
})
const shedEvents = computed(() => {
  const e = ov.value?.today.events ?? {}
  return (e.shed_aircon ?? 0) + (e.shed_fan ?? 0) + (e.shed_light ?? 0)
})

/* ---------- 大樓排行（即時 kW，不含太陽能） ---------- */
const topBuildings = computed(() => {
  const kw = new Map(latest.value.filter((l) => l.online).map((l) => [l.meterId, l.kw ?? 0]))
  const by = new Map<string, number>()
  for (const m of meters.value) if (m.category !== 'solar') by.set(m.buildingName, (by.get(m.buildingName) ?? 0) + (kw.get(m.id) ?? 0))
  const total = [...by.values()].reduce((a, b) => a + b, 0)
  return [...by.entries()]
    .map(([name, v]) => ({ name, kw: v, share: total ? (v / total) * 100 : 0 }))
    .sort((a, b) => b.kw - a.kw)
    .slice(0, 5)
})

/* ---------- 對去年同期 ---------- */
interface Cmp {
  pct: number | null
  dir: 'up' | 'down' | 'flat'
  text: string
  tip?: string
}
const fmtDate = (iso: string) => new Date(iso).toLocaleString('zh-TW', { ...tz, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit' })
const lastYearLabel = (iso: string) => `${new Date(iso).getFullYear()} 年`
function cmp(cur: number | null | undefined, last: number | null | undefined, lastText: string, tip?: string): Cmp {
  if (cur == null || last == null || last === 0) return { pct: null, dir: 'flat', text: '去年同期沒有資料', tip }
  const pct = ((cur - last) / last) * 100
  return { pct, dir: Math.abs(pct) < 0.05 ? 'flat' : pct > 0 ? 'up' : 'down', text: `比去年同期（${lastText}）`, tip }
}

const kpis = computed(() => {
  const o = ov.value
  if (!o) return []
  const money = (v: number) => fmtNum(Math.round(v), 0)
  const e1 = o.todayEnergy
  const e2 = o.monthEnergy
  return [
    {
      label: '今日用電量', value: fmtNum(e1.current, 0), unit: '度', to: ['Analysis', 'UsageTrend'],
      cmp: e1.current == null
        ? { pct: null, dir: 'flat' as const, text: '今日 00:00 前後無讀值，無法計算' }
        : cmp(e1.current, e1.lastYear, `${fmtNum(e1.lastYear, 0)} 度`,
          `今天 00:00 到現在，已扣除太陽能發電；去年同期＝${fmtDate(e1.lastYearFrom)}～${fmtDate(e1.lastYearTo)}（去年同一個星期幾、同一時間）`),
    },
    {
      label: '本月用電量', value: fmtNum(e2.current, 0), unit: '度', to: ['Analysis', 'UsageTrend'],
      cmp: cmp(e2.current, e2.lastYear, `${fmtNum(e2.lastYear, 0)} 度`,
        `本月 1 號 00:00 到現在，已扣除太陽能發電；去年同期＝${lastYearLabel(e2.lastYearFrom)} 同月 1 號到同一天的同一時間`),
    },
    {
      label: '本月最高需量', value: fmtNum(o.monthMax.kw, 0), unit: 'kW', to: ['Demand', 'DemandMonitor'],
      cmp: cmp(o.monthMax.kw, o.monthMax.lastYearKw, `${fmtNum(o.monthMax.lastYearKw, 0)} kW`,
        `本月最高需量${o.monthMax.at ? `發生於 ${fmtDate(o.monthMax.at)}` : ''}${o.monthMax.overKw > 0 ? `，超約 ${fmtNum(o.monthMax.overKw, 0)} kW` : '，未超約'}；去年同期＝去年同月整月最高需量`),
    },
    o.bill
      ? {
          label: '本月電費', trial: true, value: money(o.bill.current), unit: '元', to: ['Billing', 'BillEstimate'],
          cmp: cmp(o.bill.current, o.bill.lastYear, `${o.bill.lastYear == null ? '—' : money(o.bill.lastYear)} 元`,
            `依台電現行電價試算（含稅）；基本電費以整月計、流動電費算到今天。去年同期也算到同一天`),
        }
      : { label: '本月電費', trial: true, value: '—', unit: '', to: ['Billing', 'TariffSetting'], cmp: null, sub: '目前時段設定為二段式，沒有對應的費率可試算' },
    {
      label: '本月碳排放量', value: fmtNum(o.carbon.currentTon, 1), unit: '公噸 CO2e', to: ['Esg', 'CarbonEmission'],
      cmp: cmp(o.carbon.currentTon, o.carbon.lastYearTon, `${fmtNum(o.carbon.lastYearTon, 1)} 公噸`,
        `本月外購電力 × 經濟部能源署電力排碳係數 ${o.carbon.factor}（${o.carbon.factorYear - 1911} 年度${o.carbon.provisional ? '，今年尚未公告、暫用' : ''}）`),
    },
    {
      label: '今日太陽能發電量', value: fmtNum(o.solar.todayKwh, 0), unit: '度', to: ['Esg', 'CarbonEmission'], cmp: null,
      sub: `本月 ${fmtNum(o.solar.monthKwh, 0)} 度，自發自用約節省電費 ${money(o.solar.monthSaving)} 元`,
    },
  ]
})

/* ---------- 讀取 ---------- */
async function loadLive() {
  try {
    const [r, l, m] = await Promise.all([$getRealtimeDemand(), $getLatestReadings(), meters.value.length ? null : $getMeters()])
    rt.value = r.data ?? null
    if (rt.value) contract.value = rt.value.contract
    latest.value = l.data ?? []
    if (m) meters.value = m.data ?? []
    lastOk.value = Date.now()
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取目前需量失敗')
  }
}
async function loadDay() {
  try {
    const [d, ev] = await Promise.all([$getDemandDay(today), $getDemandEvents(today, today)])
    dayBlocks.value = d.data ?? []
    events.value = ev.data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取今日需量失敗')
  }
}
async function loadSummary() {
  try {
    ov.value = (await $getOverview()).data ?? null
  } catch (e) {
    error.value = errorMessage(e, '讀取摘要失敗')
  }
}
async function loadAll() {
  loading.value = true
  await Promise.all([loadLive(), loadDay(), loadSummary()])
  loading.value = false
}

const timers: number[] = []
onMounted(() => {
  loadAll()
  timers.push(
    window.setInterval(loadLive, LIVE_MS),
    window.setInterval(loadDay, DAY_MS),
    window.setInterval(loadSummary, SUMMARY_MS),
    window.setInterval(() => (nowMs.value = Date.now()), 1000),
  )
})
onUnmounted(() => timers.forEach(clearInterval))
</script>

<style scoped>
.tier1,
.tier2 {
  display: grid;
  grid-template-columns: minmax(0, 1.7fr) minmax(0, 1fr);
  gap: 16px;
}
.tier3 {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .tier1,
  .tier2 {
    grid-template-columns: minmax(0, 1fr);
  }
  .tier3 {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
.clickable {
  cursor: pointer;
}
.chip {
  padding: 4px 10px;
  font-size: 12px;
  border-radius: 999px;
  color: var(--text-2);
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.chip.warn {
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
  box-shadow: inset 0 0 0 1px var(--st-alarm-warning-border);
}
.live {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: var(--text-2);
}
.live-dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--st-conn-online);
  animation: pulse 2s ease-out infinite;
}
.live.stale .live-dot {
  background: var(--st-conn-offline);
  animation: none;
}
@keyframes pulse {
  0% {
    box-shadow: 0 0 0 0 color-mix(in srgb, var(--st-conn-online) 60%, transparent);
  }
  100% {
    box-shadow: 0 0 0 8px transparent;
  }
}
/* 超約時整張卡片轉紅（不閃爍） */
.now-card.is-over,
.today-card.is-over {
  background: var(--st-demand-over-soft);
  box-shadow: inset 0 0 0 2px var(--st-demand-over-border);
}
.now-card.is-shed {
  box-shadow: inset 0 0 0 2px var(--st-demand-shed-border);
}
.now-card.is-warn {
  box-shadow: inset 0 0 0 2px var(--st-demand-warn-border);
}
.status-tag {
  font-size: 14px;
}
.big {
  font-size: 56px;
  font-weight: 700;
  line-height: 1;
}
.mid {
  font-size: 40px;
  font-weight: 700;
  line-height: 1.1;
}
.big small,
.mid small,
.stat small {
  margin-left: 6px;
  font-size: 15px;
  font-weight: 500;
  color: var(--text-2);
}
.sentence {
  font-size: 15px;
  font-weight: 600;
}
.gauge {
  position: relative;
  height: 16px;
  border-radius: 999px;
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.gauge-fill {
  height: 100%;
  border-radius: 999px;
  transition: width 0.6s ease-out, background 0.3s;
}
.gauge-tick {
  position: absolute;
  top: -4px;
  bottom: -4px;
  border-left: 2px solid;
}
.gauge-scale {
  position: relative;
  height: 18px;
  font-size: 11px;
}
.gauge-scale span {
  position: absolute;
  transform: translateX(-50%);
  margin-top: 4px;
  white-space: nowrap;
}
.today-stats {
  display: grid;
  grid-template-columns: repeat(3, minmax(0, 1fr));
  gap: 8px;
}
.stat {
  font-size: 24px;
  font-weight: 700;
}
.text-over {
  color: var(--st-demand-over-text);
}
.rank {
  display: flex;
  align-items: center;
  gap: 10px;
  padding: 6px 0;
  font-size: 13px;
}
.rank-no {
  width: 20px;
  height: 20px;
  flex: none;
  border-radius: 50%;
  font-size: 11px;
  line-height: 20px;
  text-align: center;
  color: var(--accent-text);
  background: var(--accent);
}
.rank-bar {
  height: 6px;
  margin-top: 4px;
  border-radius: 999px;
  background: var(--tile-bg);
}
.rank-bar > div {
  height: 100%;
  border-radius: 999px;
  background: var(--accent);
  transition: width 0.6s ease-out;
}
.event {
  display: grid;
  grid-template-columns: 44px auto minmax(0, 1fr);
  align-items: center;
  gap: 10px;
  padding: 6px 0;
  font-size: 13px;
  border-bottom: 1px solid var(--divider);
}
.trial {
  margin-left: 6px;
  padding: 1px 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
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

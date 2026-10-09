<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">電費估算 <span class="trial">試算</span></div>
        <div class="page-sub">{{ SCHEME_NAME }} · 依台電現行電價（114/10/1 起）· 非台電帳單</div>
      </div>
      <q-space />
      <q-select v-model="month" dense outlined emit-value map-options :options="monthOptions" label="月份" style="min-width: 160px" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>
    <q-banner v-if="est?.isPartial" class="app-card q-mb-md banner-info" dense>
      本月尚未結束：流動電費只算到目前（{{ est.daysWithData }} 天），基本電費仍以整月計
    </q-banner>

    <div class="kpis q-mb-md">
      <div class="app-card kpi">
        <div class="kpi-label">應繳總額（含營業稅）</div>
        <div class="kpi-value num">{{ fmtMoney(b?.total) }}<small>元</small></div>
        <div class="kpi-sub">其中營業稅 {{ fmtMoney(b?.taxIncluded) }} 元（電價已內含 5%）</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">計費度數</div>
        <div class="kpi-value num">{{ fmtNum(est?.totalKwh, 0) }}<small>kWh</small></div>
        <div class="kpi-sub">用電－太陽能 · 平均 {{ avgPrice }} 元/度</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">超約附加費</div>
        <div class="kpi-value num">{{ fmtMoney(b?.overageTotal) }}<small>元</small></div>
        <div class="kpi-sub">{{ b?.overage.length ? `超約 ${b.overage.map((o) => `${statusLabel('tou', o.tou)} ${fmtNum(o.overKw, 0)} kW`).join('、')}` : '本月沒有超約' }}</div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">太陽能省下</div>
        <div class="kpi-value num">{{ fmtMoney(est?.solarSaving) }}<small>元</small></div>
        <div class="kpi-sub">發電 {{ fmtNum(est?.solarKwh, 0) }} kWh · 與沒有太陽能時相比</div>
      </div>
    </div>

    <div class="grid2 q-mb-md">
      <!-- 明細 -->
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">電費明細</div>
          <span class="text-3 text-caption">依據：詳細電價表第五章</span>
        </div>
        <table v-if="b" class="bill">
          <tbody>
            <tr class="sec"><td colspan="3">基本電費 <span class="ref">五(一)2</span></td></tr>
            <tr v-for="l in b.basic" :key="l.season + l.item">
              <td>{{ ITEM_LABEL[l.item] }}（{{ SEASON_LABEL[l.season] }}）</td>
              <td class="calc num">{{ fmtNum(l.kw, 0) }} kW × {{ fmtNum(l.rate, 2) }}{{ l.dayFraction < 1 ? ` × ${dayFrac(l)}` : '' }}</td>
              <td class="amt num">{{ fmtMoney(l.amount) }}</td>
            </tr>
            <tr class="sec"><td colspan="3">功率因數調整 <span class="ref">六</span></td></tr>
            <tr>
              <td>月平均功率因數 {{ b.powerFactorPct ?? '—' }}%</td>
              <td class="calc num">基本電費 × {{ b.powerFactorAdjustPct > 0 ? '+' : '' }}{{ fmtNum(b.powerFactorAdjustPct, 1) }}%</td>
              <td class="amt num">{{ fmtMoney(b.powerFactorAdjustment) }}</td>
            </tr>
            <tr class="sec"><td colspan="3">超約附加費 <span class="ref">七</span></td></tr>
            <tr v-for="o in b.overage" :key="o.tou">
              <td>{{ statusLabel('tou', o.tou) }} 最高 {{ fmtNum(o.maxDemandKw, 0) }} kW，超出 {{ fmtNum(o.overKw, 1) }} kW</td>
              <td class="calc num">
                {{ fmtNum(o.within10Kw, 1) }} × {{ fmtNum(o.rate, 2) }} × 2<template v-if="o.beyond10Kw > 0"> ＋ {{ fmtNum(o.beyond10Kw, 1) }} × {{ fmtNum(o.rate, 2) }} × 3</template>
              </td>
              <td class="amt num">{{ fmtMoney(o.amount) }}</td>
            </tr>
            <tr v-if="!b.overage.length"><td colspan="2" class="text-3">沒有超約</td><td class="amt num">0</td></tr>
            <tr class="sec"><td colspan="3">流動電費</td></tr>
            <tr v-for="l in b.energy" :key="l.season + l.tou">
              <td><StatusTag group="tou" :k="l.tou" />（{{ SEASON_LABEL[l.season] }}）</td>
              <td class="calc num">{{ fmtNum(l.kwh, 0) }} 度 × {{ fmtNum(l.rate, 2) }}</td>
              <td class="amt num">{{ fmtMoney(l.amount) }}</td>
            </tr>
            <tr class="total">
              <td colspan="2">應繳總額</td>
              <td class="amt num">{{ fmtMoney(b.total) }}</td>
            </tr>
            <tr>
              <td colspan="2" class="text-3">其中營業稅（總額 ÷ 1.05 × 0.05）<span class="ref">八(一)</span></td>
              <td class="amt num text-3">{{ fmtMoney(b.taxIncluded) }}</td>
            </tr>
          </tbody>
        </table>
      </div>

      <!-- 時段度數與需量 -->
      <div class="column q-gutter-y-md no-wrap">
        <div class="app-card q-pa-md">
          <div class="card-head"><div class="card-title">各時段計費度數</div></div>
          <div v-if="est" class="tou-bar">
            <span v-for="t in TOUS" :key="t" :style="{ flex: touKwh(t) || 0.0001, background: `var(--st-tou-${t})` }" />
          </div>
          <div v-for="t in TOUS" :key="t" class="kv">
            <StatusTag group="tou" :k="t" />
            <span class="num">{{ fmtNum(touKwh(t), 0) }} kWh</span>
          </div>
          <div class="text-3 text-caption q-mt-sm">
            含依時段比例分攤的未分時段 {{ fmtNum(est?.allocatedKwh, 0) }} kWh（斷線期間，估算）
          </div>
        </div>
        <div class="app-card q-pa-md">
          <div class="card-head"><div class="card-title">各時段最高需量</div></div>
          <div v-for="m in est?.maxDemands ?? []" :key="m.tou" class="kv">
            <StatusTag group="tou" :k="m.tou" />
            <span class="num">{{ fmtNum(m.kw, 0) }} kW <span class="text-3">· {{ m.date.slice(5).replace('-', '/') }}</span></span>
          </div>
          <div class="text-3 text-caption q-mt-sm">經常契約 {{ fmtNum(est?.contract.regularKw, 0) }} kW；需量（已扣除太陽能發電，15 分鐘平均）</div>
        </div>
      </div>
    </div>

    <!-- 13 個月 -->
    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">近 13 個月電費</div>
        <span class="text-3 text-caption">夏月（5/16–10/15）費率較高；本月為至今</span>
      </div>
      <div style="height: 300px">
        <BarChart v-if="history.length" :categories="historyChart.categories" :series="historyChart.series" unit="元" stacked :rotate="0" />
      </div>
    </div>

    <!-- 解讀 -->
    <div class="app-card q-pa-md">
      <div class="card-head"><div class="card-title">試算說明（條文未寫清楚、由本系統解讀的部分）</div></div>
      <ul class="notes text-2">
        <li v-for="n in NOTES" :key="n">{{ n }}</li>
      </ul>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue'
import StatusTag from '@/components/status/StatusTag.vue'
import BarChart from '@/components/charts/BarChart.vue'
import { $getBillEstimate, $getBillHistory } from '@/api'
import { errorMessage } from '@/api/http'
import { statusColor, statusLabel } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum } from '@/utils/format'
import { taipeiToday } from '@/utils/energy'
import { ITEM_LABEL, SCHEME_NAME, SEASON_LABEL } from '@/utils/tariff'
import type { BasicLine, BillEstimate, BillHistory, Tou } from '@/types/api'

const TOUS: Tou[] = ['peak', 'half', 'sat_half', 'off']
// docs/實作/03 §3.4
const NOTES = [
  '超約的 10% 基準：各時段用自己的累計契約容量（例：半尖峰＝經常＋半尖峰契約）。',
  '超約計費單價：各時段用該時段的契約電價（尖峰→經常契約、半尖峰→半尖峰契約、週六半尖峰／離峰→週六半尖峰／離峰契約）。',
  '功率因數調整套用在契約內的基本電費；超約部分不給予功率因數扣減。月平均功率因數以當月讀值 ΣkW、Σkvar 估算。',
  '5 月、10 月跨夏月與非夏月：基本電費依日數比例拆分；計費週期用日曆月（台電實際依抄表日）。',
  '需量＝每個整 15 分鐘區間的用電量（度）× 4，即該區間的平均用電（kW）；區間頭尾取 ±5 分鐘內最接近的累積電度。',
  '斷線期間的未分時段度數，依當天已量測的時段比例分攤計價（估算）。',
  '太陽能視為自發自用：計費度數與需量都用「用電－發電」。',
]

const { mode } = useTheme()
const today = taipeiToday()
const month = ref(`${today.slice(0, 7)}-01`)
const est = ref<BillEstimate | null>(null)
const history = ref<BillHistory[]>([])
const error = ref('')
const b = computed(() => est.value?.bill ?? null)

const monthOptions = computed(() =>
  [...history.value].reverse().map((h) => ({ value: h.month, label: `${h.month.slice(0, 7).replace('-', ' 年 ')} 月${h.isPartial ? '（至今）' : ''}` })),
)
const fmtMoney = (v: number | null | undefined) => fmtNum(v == null ? null : Math.round(v), 0)
const avgPrice = computed(() => (b.value && est.value?.totalKwh ? fmtNum(b.value.total / est.value.totalKwh, 2) : '—'))
const dayFrac = (l: BasicLine) => {
  const days = (b.value?.summerDays ?? 0) + (b.value?.nonSummerDays ?? 0)
  return `${l.season === 'summer' ? b.value?.summerDays : b.value?.nonSummerDays}/${days} 天`
}
const touKwh = (t: Tou) => {
  const e = est.value
  if (!e) return 0
  return { peak: e.peakKwh, half: e.halfKwh, sat_half: e.satHalfKwh, off: e.offKwh }[t]
}

const historyChart = computed(() => {
  const m = mode.value
  const h = history.value
  return {
    categories: h.map((x) => x.month.slice(2, 7).replace('-', '/')),
    series: [
      { name: '基本電費（含功率因數）', color: statusColor('category', 'other', m), data: h.map((x) => Math.round(x.basicTotal + x.powerFactorAdjustment)) },
      { name: '超約附加費', color: statusColor('demand', 'over_contract', m), data: h.map((x) => Math.round(x.overageTotal)) },
      { name: '流動電費', color: statusColor('category', 'aircon', m), data: h.map((x) => Math.round(x.energyTotal)) },
    ],
  }
})

async function loadEstimate() {
  try {
    est.value = (await $getBillEstimate(month.value)).data ?? null
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取電費試算失敗')
  }
}

watch(month, loadEstimate)
onMounted(async () => {
  loadEstimate()
  try {
    history.value = (await $getBillHistory(13)).data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取電費歷史失敗')
  }
})
</script>

<style scoped>
.trial {
  margin-left: 8px;
  padding: 2px 8px;
  font-size: 12px;
  font-weight: 600;
  vertical-align: middle;
  border-radius: 999px;
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
}
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
.grid2 {
  display: grid;
  grid-template-columns: minmax(0, 1.6fr) minmax(0, 1fr);
  gap: 16px;
}
@media (max-width: 1023px) {
  .grid2 {
    grid-template-columns: minmax(0, 1fr);
  }
}
.bill {
  width: 100%;
  border-collapse: collapse;
  font-size: 13px;
}
.bill td {
  padding: 6px 4px;
  border-bottom: 1px solid var(--divider);
}
.bill .sec td {
  padding-top: 12px;
  font-weight: 600;
  color: var(--text-1);
  border-bottom: none;
}
.bill .calc {
  color: var(--text-2);
}
.bill .amt {
  text-align: right;
  white-space: nowrap;
}
.bill .total td {
  font-size: 15px;
  font-weight: 700;
  border-top: 2px solid var(--text-3);
}
.ref {
  margin-left: 6px;
  font-size: 11px;
  font-weight: 400;
  color: var(--text-3);
}
.tou-bar {
  display: flex;
  height: 12px;
  margin: 4px 0 10px;
  overflow: hidden;
  border-radius: 999px;
  gap: 2px;
}
.kv {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 5px 0;
  border-bottom: 1px solid var(--divider);
}
.notes {
  margin: 0;
  padding-left: 20px;
  line-height: 1.9;
  font-size: 13px;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
.banner-info {
  color: var(--st-alarm-info-text);
  background: var(--st-alarm-info-soft);
}
</style>

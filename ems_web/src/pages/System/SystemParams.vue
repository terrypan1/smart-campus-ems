<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">契約與係數</div>
        <div class="page-sub">契約容量與預警值、電力排碳係數、彙總與告警重算 · 修改都會寫入稽核紀錄</div>
      </div>
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="grid2 q-mb-md">
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">契約容量與預警值</div>
          <q-space />
          <q-btn flat dense no-caps color="primary" :icon="mdiPencil" label="修改" :disable="!contract" @click="contractOpen = true" />
        </div>
        <q-markup-table v-if="contract" flat dense class="app-table">
          <tbody>
            <tr><td>經常契約</td><td class="text-right num"><b>{{ fmtNum(contract.regularKw, 0) }}</b> kW</td></tr>
            <tr><td>半尖峰契約</td><td class="text-right num">{{ fmtNum(contract.halfPeakKw, 0) }} kW</td></tr>
            <tr><td>週六半尖峰契約</td><td class="text-right num">{{ fmtNum(contract.satHalfKw, 0) }} kW</td></tr>
            <tr><td>離峰契約</td><td class="text-right num">{{ fmtNum(contract.offPeakKw, 0) }} kW</td></tr>
            <tr v-for="t in thresholds" :key="t.label">
              <td><StatusTag group="demand" :k="t.k" :label="t.label" /></td>
              <td class="text-right num">{{ t.pct }}% · {{ fmtNum((contract.regularKw * t.pct) / 100, 0) }} kW</td>
            </tr>
          </tbody>
        </q-markup-table>
        <div v-if="contract" class="text-3 text-caption q-mt-sm">最後修改：{{ fmtTime(contract.updatedAt) }}（{{ contract.updatedBy }}）· 需量監控頁也可以修改，同一份設定</div>
      </div>

      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">電力排碳係數</div>
          <span class="text-3 text-caption">kg CO2e/kWh · 能源署公告</span>
          <q-space />
          <q-btn flat dense no-caps color="primary" :icon="mdiPlus" label="新增／修改年度" @click="openFactor()" />
        </div>
        <q-markup-table flat dense class="app-table">
          <thead><tr><th class="text-left">年度</th><th class="text-right">係數</th><th class="text-left">來源</th><th /></tr></thead>
          <tbody>
            <tr v-for="f in factors" :key="f.year">
              <td>{{ f.year - 1911 }} 年度（{{ f.year }}）<span v-if="f.isProvisional" class="prov">暫定</span></td>
              <td class="text-right num">{{ f.factor }}</td>
              <td class="wrap text-2">{{ f.source }}</td>
              <td class="text-right nowrap">
                <q-btn flat round dense size="sm" :icon="mdiPencil" class="text-2" @click="openFactor(f)" />
                <q-btn flat round dense size="sm" :icon="mdiDeleteOutline" class="text-2" @click="removeFactor(f)" />
              </td>
            </tr>
          </tbody>
        </q-markup-table>
        <div class="text-3 text-caption q-mt-sm">還沒公告的年度不必建：計算時自動暫用最近一個已公告年度。能源署公告新年度後在這裡新增即可（碳排是查詢時計算，不必重算）。</div>
      </div>
    </div>

    <div class="app-card q-pa-md">
      <div class="card-head"><div class="card-title">重算</div></div>
      <div class="row q-col-gutter-md">
        <div class="col-12 col-md-6">
          <div class="text-2 q-mb-sm">從原始讀值重算每小時用電、日／月／年彙總與 15 分鐘需量，完成後重新補記歷史告警。改了時段方案、離峰日之後要做。</div>
          <q-btn outline no-caps color="primary" :icon="mdiRefresh" label="全部重算彙總" :loading="busy.recompute" @click="recompute" />
        </div>
        <div class="col-12 col-md-6">
          <div class="text-2 q-mb-sm">只重新補記歷史告警（斷線、超約、資料缺漏、電度倒退）。歷史告警都標「系統已確認」，不計入鈴鐺。</div>
          <q-btn outline no-caps color="primary" :icon="mdiBellCogOutline" label="重新補記歷史告警" :loading="busy.alarms" @click="rebuildAlarms" />
        </div>
      </div>
    </div>

    <ContractDialog v-model="contractOpen" :contract="contract" @saved="(c) => (contract = c)" />

    <q-dialog v-model="factorDlg.open">
      <q-card style="width: 420px; max-width: 92vw">
        <q-card-section class="text-h6">排碳係數</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <q-input v-model.number="factorDlg.v.year" dense outlined type="number" label="年度（西元）" :hint="factorDlg.v.year ? `民國 ${factorDlg.v.year - 1911} 年度` : ''" />
          <q-input v-model.number="factorDlg.v.factor" dense outlined type="number" step="0.001" label="係數 kg CO2e/kWh" />
          <q-input v-model="factorDlg.v.source" dense outlined label="來源（文件名稱、公告日期）" />
          <q-checkbox v-model="factorDlg.v.isProvisional" dense label="暫定值（還沒正式公告）" />
          <div v-if="factorDlg.error" class="text-negative text-caption">{{ factorDlg.error }}</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="取消" />
          <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" label="儲存" @click="saveFactor" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Dialog, Notify } from 'quasar'
import { mdiBellCogOutline, mdiDeleteOutline, mdiPencil, mdiPlus, mdiRefresh } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import ContractDialog from '@/components/demand/ContractDialog.vue'
import { $deleteEmissionFactor, $getContract, $getEmissionFactors, $putEmissionFactor, $recompute } from '@/api'
import http, { errorMessage } from '@/api/http'
import { useTheme } from '@/theme/useTheme'
import { fmtNum, fmtTime } from '@/utils/format'
import type { Contract, DemandLevel, EmissionFactor } from '@/types/api'

const { mode } = useTheme()
const contract = ref<Contract | null>(null)
const contractOpen = ref(false)
const factors = ref<EmissionFactor[]>([])
const error = ref('')
const busy = reactive({ recompute: false, alarms: false })

const thresholds = computed(() => {
  const c = contract.value
  if (!c) return []
  return [
    { k: 'warn' as DemandLevel, label: '預警', pct: c.warnPct },
    { k: 'shed_aircon' as DemandLevel, label: '卸載：空調', pct: c.shedAirconPct },
    { k: 'shed_fan' as DemandLevel, label: '卸載：進排風', pct: c.shedFanPct },
    { k: 'shed_light' as DemandLevel, label: '卸載：照明', pct: c.shedLightPct },
  ]
})

async function load() {
  try {
    const [c, f] = await Promise.all([$getContract(), $getEmissionFactors()])
    contract.value = c.data ?? null
    factors.value = f.data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取設定失敗')
  }
}

const factorDlg = reactive({ open: false, error: '', v: { year: 0, factor: 0, source: '', isProvisional: false } })
function openFactor(f?: EmissionFactor) {
  const next = (factors.value.at(-1)?.year ?? 2025) + 1
  factorDlg.v = f ? { year: f.year, factor: f.factor, source: f.source, isProvisional: f.isProvisional } : { year: next, factor: 0, source: `經濟部能源署 ${next - 1911}年度電力排碳係數`, isProvisional: false }
  factorDlg.error = ''
  factorDlg.open = true
}
async function saveFactor() {
  try {
    await $putEmissionFactor(factorDlg.v)
    factorDlg.open = false
    Notify.create({ type: 'positive', message: '已儲存排碳係數' })
    await load()
  } catch (e) {
    factorDlg.error = errorMessage(e, '儲存失敗')
  }
}
function removeFactor(f: EmissionFactor) {
  Dialog.create({ title: '刪除排碳係數', message: `${f.year - 1911} 年度 ${f.factor}`, cancel: true }).onOk(async () => {
    try {
      await $deleteEmissionFactor(f.year)
      await load()
    } catch (e) {
      error.value = errorMessage(e, '刪除失敗')
    }
  })
}

async function recompute() {
  busy.recompute = true
  try {
    await $recompute()
    Notify.create({ type: 'info', message: '已開始重算，約 10–30 秒完成' })
  } catch (e) {
    error.value = errorMessage(e, '重算失敗')
  } finally {
    busy.recompute = false
  }
}
async function rebuildAlarms() {
  busy.alarms = true
  try {
    await http.post('/api/Alarms/RebuildHistory')
    Notify.create({ type: 'positive', message: '已重新補記歷史告警' })
    window.dispatchEvent(new Event('ems-alarms-changed'))
  } catch (e) {
    error.value = errorMessage(e, '補記失敗')
  } finally {
    busy.alarms = false
  }
}

onMounted(load)
</script>

<style scoped>
.grid2 {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .grid2 {
    grid-template-columns: minmax(0, 1fr);
  }
}
.prov {
  margin-left: 6px;
  padding: 1px 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
}
.wrap {
  white-space: normal;
}
.nowrap {
  white-space: nowrap;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

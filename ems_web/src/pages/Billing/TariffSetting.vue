<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">電價設定</div>
        <div class="page-sub">{{ SCHEME_NAME }} · {{ isAdmin ? '管理者可修改費率、時段方案與離峰日' : '檢視者只能查看' }}</div>
      </div>
      <q-space />
      <template v-if="isAdmin">
        <q-btn flat no-caps class="text-2" :icon="mdiBackupRestore" label="還原台電公告值" :disable="!modifiedCount" @click="restore" />
        <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" :icon="mdiContentSave" label="儲存費率" :disable="!dirty.length" :loading="saving" @click="save" />
      </template>
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>
    <q-banner v-if="needRecompute" class="app-card q-mb-md banner-warn" dense>
      時段方案或離峰日已變更：新資料會立即套用，歷史彙總（用電趨勢、需量、電費）要重算才會更新。
      <template #action>
        <q-btn v-if="isAdmin" flat no-caps label="立即重算" :loading="recomputing" @click="recompute" />
      </template>
    </q-banner>

    <div class="grid2 q-mb-md">
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">基本電費</div>
          <span class="text-3 text-caption">元／kW／月</span>
        </div>
        <q-markup-table flat dense class="app-table">
          <thead><tr><th class="text-left">契約</th><th class="text-right">夏月</th><th class="text-right">非夏月</th></tr></thead>
          <tbody>
            <tr v-for="item in BASIC" :key="item">
              <td>{{ ITEM_LABEL[item] }}</td>
              <td v-for="s in SEASONS" :key="s" class="text-right">
                <RateCell :rate="rateOf(s, item)" :edit="isAdmin" @update="(v) => setRate(s, item, v)" />
              </td>
            </tr>
          </tbody>
        </q-markup-table>
      </div>
      <div class="app-card q-pa-md">
        <div class="card-head">
          <div class="card-title">流動電費</div>
          <span class="text-3 text-caption">元／度</span>
        </div>
        <q-markup-table flat dense class="app-table">
          <thead><tr><th class="text-left">時段</th><th class="text-right">夏月</th><th class="text-right">非夏月</th></tr></thead>
          <tbody>
            <tr v-for="e in ENERGY" :key="e.item">
              <td><StatusTag group="tou" :k="e.tou" :label="ITEM_LABEL[e.item]" /></td>
              <td v-for="s in SEASONS" :key="s" class="text-right">
                <RateCell :rate="rateOf(s, e.item)" :edit="isAdmin" @update="(v) => setRate(s, e.item, v)" />
              </td>
            </tr>
          </tbody>
        </q-markup-table>
        <div class="text-3 text-caption q-mt-sm">非夏月沒有尖峰時段。電價已內含 5% 營業稅（詳細電價表第五章八(一)）。</div>
      </div>
    </div>
    <div v-if="modifiedCount" class="text-2 text-caption q-mb-md">
      <span class="mod-dot" /> {{ modifiedCount }} 項費率已手動修改（與台電公告值不同）；滑過數字可看公告值、修改者與時間
    </div>

    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">時段</div>
        <span class="text-3 text-caption">夏月 5/16–10/15；週日與離峰日全日離峰</span>
        <q-space />
        <q-btn-toggle
          :model-value="scheme?.scheme"
          :disable="!isAdmin"
          unelevated
          no-caps
          toggle-color="primary"
          :toggle-text-color="mode === 'dark' ? 'black' : 'white'"
          class="level-toggle"
          :options="[{ label: '三段式（尖峰時間固定）', value: 'HV3' }, { label: '二段式', value: 'HV2' }]"
          @update:model-value="changeScheme"
        />
      </div>
      <q-banner v-if="scheme?.scheme === 'HV2'" class="banner-warn q-mb-sm" dense>二段式時段：平日有電價的時段全部是尖峰。費率表只有三段式，電費估算在二段式時不提供試算。</q-banner>
      <q-markup-table flat dense class="app-table">
        <thead><tr><th class="text-left">日別</th><th class="text-left">夏月</th><th class="text-left">非夏月</th></tr></thead>
        <tbody>
          <tr v-for="r in bands" :key="r.day">
            <td>{{ r.day }}</td>
            <td class="wrap"><span v-for="s in r.summer" :key="s.t + s.h" class="band"><StatusTag group="tou" :k="s.t" /> {{ s.h }}</span></td>
            <td class="wrap"><span v-for="s in r.non" :key="s.t + s.h" class="band"><StatusTag group="tou" :k="s.t" /> {{ s.h }}</span></td>
          </tr>
        </tbody>
      </q-markup-table>
      <div v-if="scheme?.updatedBy" class="text-3 text-caption q-mt-sm">最後切換：{{ fmtTime(scheme.updatedAt) }}（{{ scheme.updatedBy }}）</div>
    </div>

    <div class="app-card q-pa-md q-mb-md">
      <div class="card-head">
        <div class="card-title">離峰日（全日離峰）</div>
        <span class="text-3 text-caption">詳細電價表第五章四 註1；國曆固定日期每年都是，農曆日期每年依人事行政總處公告新增</span>
        <q-space />
        <q-btn v-if="isAdmin" flat dense no-caps color="primary" :icon="mdiPlus" label="新增離峰日" @click="addDay.open = true" />
      </div>
      <div class="days">
        <div v-for="d in days" :key="d.id ?? d.date" class="day" :class="{ 'is-fixed': d.fixed }">
          <span class="num">{{ d.fixed ? `每年 ${d.date.slice(5).replace('-', '/')}` : d.date }}</span>
          <span class="text-2">{{ d.name }}</span>
          <q-btn v-if="isAdmin && !d.fixed" flat round dense size="xs" :icon="mdiClose" class="text-3" @click="removeDay(d)" />
        </div>
      </div>
    </div>

    <div class="app-card q-pa-md">
      <div class="card-head"><div class="card-title">計算規則與出處</div></div>
      <q-markup-table flat dense class="app-table">
        <thead><tr><th class="text-left">項目</th><th class="text-left">條文</th><th class="text-left">規則</th></tr></thead>
        <tbody>
          <tr v-for="r in RULES" :key="r.item">
            <td class="nowrap">{{ r.item }}</td>
            <td class="nowrap text-2">{{ r.ref }}</td>
            <td class="wrap">{{ r.rule }}</td>
          </tr>
        </tbody>
      </q-markup-table>
      <div class="text-3 text-caption q-mt-md">
        <div>費率來源：{{ sources.join('；') || '—' }}</div>
        <div>計算規則：詳細電價表（114/11/17 經濟部備查）第五章；時段：〈112 年時間電價新時間帶及高壓以上擴大夏月期間說明〉。</div>
        <div>電價費率審議會 115/3/27、115/9/18 均決議維持現行費率（2026-10-05 查證）。</div>
      </div>
    </div>

    <q-dialog v-model="addDay.open">
      <q-card style="width: 360px; max-width: 92vw">
        <q-card-section class="text-h6">新增離峰日</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <q-input v-model="addDay.date" dense outlined type="date" label="日期" />
          <q-input v-model="addDay.name" dense outlined label="名稱（例：春節、端午節）" />
          <div v-if="addDay.error" class="text-negative text-caption">{{ addDay.error }}</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="取消" />
          <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" label="新增" @click="saveDay" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { computed, defineComponent, h, onMounted, reactive, ref } from 'vue'
import { Dialog, Notify, QInput, QTooltip } from 'quasar'
import { mdiBackupRestore, mdiClose, mdiContentSave, mdiPlus } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import { $addOffPeakDay, $deleteOffPeakDay, $getOffPeakDays, $getScheme, $getTariffAdmin, $putScheme, $putTariff, $recompute, $restoreTariff } from '@/api'
import { errorMessage } from '@/api/http'
import { auth } from '@/stores/auth'
import { useTheme } from '@/theme/useTheme'
import { fmtNum, fmtTime } from '@/utils/format'
import { ITEM_LABEL, SCHEME_NAME } from '@/utils/tariff'
import type { OffPeakDay, SchemeInfo, TariffRateAdmin, Tou } from '@/types/api'

const SEASONS = ['summer', 'nonsummer'] as const
const BASIC = ['basic_regular', 'basic_half', 'basic_sat_half', 'basic_off']
const ENERGY: { item: string; tou: Tou }[] = [
  { item: 'energy_peak', tou: 'peak' },
  { item: 'energy_half', tou: 'half' },
  { item: 'energy_sat_half', tou: 'sat_half' },
  { item: 'energy_off', tou: 'off' },
]
type Band = { day: string; summer: { t: Tou; h: string }[]; non: { t: Tou; h: string }[] }
const SAT_SUN: Band[] = [
  {
    day: '週六',
    summer: [{ t: 'sat_half', h: '09:00–24:00' }, { t: 'off', h: '00:00–09:00' }],
    non: [{ t: 'sat_half', h: '06:00–11:00、14:00–24:00' }, { t: 'off', h: '00:00–06:00、11:00–14:00' }],
  },
  { day: '週日、離峰日', summer: [{ t: 'off', h: '全日' }], non: [{ t: 'off', h: '全日' }] },
]
// 與後端 TouCalendar 一致（docs/實作/02 §2.4、03 §5.1）
const BANDS: Record<'HV3' | 'HV2', Band[]> = {
  HV3: [
    {
      day: '週一～週五',
      summer: [{ t: 'peak', h: '16:00–22:00' }, { t: 'half', h: '09:00–16:00、22:00–24:00' }, { t: 'off', h: '00:00–09:00' }],
      non: [{ t: 'half', h: '06:00–11:00、14:00–24:00' }, { t: 'off', h: '00:00–06:00、11:00–14:00' }],
    },
    ...SAT_SUN,
  ],
  HV2: [
    {
      day: '週一～週五',
      summer: [{ t: 'peak', h: '09:00–24:00' }, { t: 'off', h: '00:00–09:00' }],
      non: [{ t: 'peak', h: '06:00–11:00、14:00–24:00' }, { t: 'off', h: '00:00–06:00、11:00–14:00' }],
    },
    ...SAT_SUN,
  ],
}
const RULES = [
  { item: '基本電費', ref: '五(一)2', rule: '經常契約電價×經常契約容量＋半尖峰契約電價×半尖峰契約容量＋週六半尖峰或離峰契約電價×〔(週六半尖峰＋離峰契約容量)－(經常＋半尖峰契約容量)×0.5〕，最後一項為負按 0。' },
  { item: '超約瓩數', ref: '七(一)2', rule: '尖峰＝尖峰最高需量－經常；半尖峰＝半尖峰最高需量－(經常＋半尖峰)；週六半尖峰、離峰依序再加上自己的契約。後面時段要扣掉前面時段已超出瓩數的較大者（不重複計算）。' },
  { item: '超約附加費', ref: '七(二)', rule: '契約容量 10% 以下部分按 2 倍、超過 10% 部分按 3 倍計收基本電費；超約部分不給予功率因數扣減。' },
  { item: '功率因數', ref: '六', rule: '月平均功率因數以 80% 為準，每低 1% 基本電費加 0.1%，每高 1% 減 0.1%；95% 以上不再扣減。' },
  { item: '營業稅', ref: '八(一)', rule: '電價已內含 5% 營業稅；畫面上的營業稅是從總額拆出（總額 ÷ 1.05 × 0.05），不另外加。' },
  { item: '離峰日', ref: '四 註1', rule: '國定假日等離峰日全日按離峰計價。' },
]

/** 費率格：管理者可直接改；與公告值不同時加註 */
const RateCell = defineComponent({
  props: { rate: { type: Object as () => (TariffRateAdmin & { draft?: number }) | undefined, default: undefined }, edit: Boolean },
  emits: ['update'],
  setup(props, { emit }) {
    return () => {
      const r = props.rate
      if (!r) return h('span', { class: 'text-3' }, '—')
      const value = r.draft ?? r.rate
      const tip = r.modified || r.draft != null
        ? h(QTooltip, null, () => `台電公告值 ${fmtNum(r.officialRate, 2)}${r.updatedBy ? `；${r.updatedBy} 於 ${fmtTime(r.updatedAt)} 修改` : ''}`)
        : null
      const changed = (r.draft != null && r.draft !== r.rate) || (r.draft == null && r.modified)
      if (!props.edit) return h('span', { class: ['num', changed ? 'modified' : ''] }, [fmtNum(value, 2), tip])
      return h(QInput, {
        modelValue: value, type: 'number', dense: true, outlined: true, step: '0.01', inputClass: 'text-right num',
        class: ['rate-input', changed ? 'modified' : ''],
        'onUpdate:modelValue': (v: string | number | null) => emit('update', Number(v)),
      }, { default: () => tip })
    }
  },
})

const { mode } = useTheme()
const isAdmin = computed(() => auth.user?.role === 'admin')
const rates = ref<(TariffRateAdmin & { draft?: number })[]>([])
const scheme = ref<SchemeInfo | null>(null)
const days = ref<OffPeakDay[]>([])
const saving = ref(false)
const recomputing = ref(false)
const needRecompute = ref(false)
const error = ref('')

const rateOf = (season: string, item: string) => rates.value.find((r) => r.season === season && r.item === item)
const setRate = (season: string, item: string, v: number) => {
  const r = rateOf(season, item)
  if (r) r.draft = Number.isFinite(v) ? v : r.rate
}
const dirty = computed(() => rates.value.filter((r) => r.draft != null && r.draft !== r.rate))
const modifiedCount = computed(() => rates.value.filter((r) => r.modified).length)
const sources = computed(() => [...new Set(rates.value.map((r) => r.source))])
const bands = computed(() => BANDS[scheme.value?.scheme ?? 'HV3'])

async function load() {
  try {
    const [r, s, d] = await Promise.all([$getTariffAdmin(), $getScheme(), $getOffPeakDays()])
    rates.value = r.data ?? []
    scheme.value = s.data ?? null
    days.value = d.data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取電價設定失敗')
  }
}

async function save() {
  saving.value = true
  try {
    rates.value = (await $putTariff(dirty.value.map((r) => ({ season: r.season, item: r.item, rate: r.draft! })))).data ?? []
    Notify.create({ type: 'positive', message: '已更新電價，電費估算立即套用' })
  } catch (e) {
    error.value = errorMessage(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}

function restore() {
  Dialog.create({ title: '還原台電公告值', message: `${modifiedCount.value} 項手動修改的費率會改回台電公告值。`, cancel: true }).onOk(async () => {
    try {
      await $restoreTariff()
      await load()
      Notify.create({ type: 'positive', message: '已還原台電公告值' })
    } catch (e) {
      error.value = errorMessage(e, '還原失敗')
    }
  })
}

function changeScheme(v: 'HV3' | 'HV2') {
  if (v === scheme.value?.scheme) return
  Dialog.create({
    title: '切換時段方案',
    message: `切換成「${v === 'HV3' ? '三段式' : '二段式'}」後，新的資料立即套用新時段；歷史彙總要重算才會更新。${v === 'HV2' ? '二段式時電費估算不提供試算。' : ''}`,
    cancel: true,
  }).onOk(async () => {
    try {
      scheme.value = (await $putScheme(v)).data ?? scheme.value
      needRecompute.value = true
    } catch (e) {
      error.value = errorMessage(e, '切換失敗')
    }
  })
}

async function recompute() {
  recomputing.value = true
  try {
    await $recompute()
    needRecompute.value = false
    Notify.create({ type: 'info', message: '已開始重算，約 10–30 秒完成，完成後重新整理頁面即可看到' })
  } catch (e) {
    error.value = errorMessage(e, '重算失敗')
  } finally {
    recomputing.value = false
  }
}

const addDay = reactive({ open: false, date: '', name: '', error: '' })
async function saveDay() {
  if (!addDay.date || !addDay.name) return (addDay.error = '請輸入日期與名稱')
  try {
    await $addOffPeakDay(addDay.date, addDay.name)
    Object.assign(addDay, { open: false, date: '', name: '', error: '' })
    needRecompute.value = true
    await load()
  } catch (e) {
    addDay.error = errorMessage(e, '新增失敗')
  }
}
function removeDay(d: OffPeakDay) {
  Dialog.create({ title: '刪除離峰日', message: `${d.date} ${d.name}`, cancel: true }).onOk(async () => {
    try {
      await $deleteOffPeakDay(d.id!)
      needRecompute.value = true
      await load()
    } catch (e) {
      error.value = errorMessage(e, '刪除失敗')
    }
  })
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
.rate-input {
  width: 110px;
  margin-left: auto;
}
:deep(.modified),
:deep(.modified input) {
  color: var(--st-alarm-warning-text) !important;
  font-weight: 600;
}
.mod-dot {
  display: inline-block;
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: var(--st-alarm-warning);
}
.level-toggle {
  border: 1px solid var(--card-border);
  border-radius: 8px;
}
.band {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  margin: 2px 14px 2px 0;
  white-space: nowrap;
}
.days {
  display: flex;
  flex-wrap: wrap;
  gap: 8px;
}
.day {
  display: inline-flex;
  align-items: center;
  gap: 8px;
  padding: 4px 10px;
  font-size: 13px;
  border-radius: var(--radius-sm);
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.day.is-fixed {
  background: transparent;
}
.nowrap {
  white-space: nowrap;
}
.wrap {
  white-space: normal;
  line-height: 1.6;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
.banner-warn {
  color: var(--st-alarm-warning-text);
  background: var(--st-alarm-warning-soft);
}
</style>

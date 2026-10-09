<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">告警事件</div>
        <div class="page-sub">同一件事只有一筆（有開始、有結束）· 每分鐘檢查 · 歷史告警為重算歷史資料時補記</div>
      </div>
      <q-space />
      <q-btn flat dense no-caps class="text-2" :icon="mdiRefresh" label="重新整理" :loading="loading" @click="load" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="kpis q-mb-md">
      <div v-for="l in LEVELS" :key="l" class="app-card kpi clickable" :class="{ picked: filter.level === l }" @click="filter.level = filter.level === l ? null : l">
        <div class="kpi-label"><StatusTag group="alarm" :k="l" /></div>
        <div class="kpi-value num">{{ summary?.byLevel[l] ?? 0 }}<small>未確認</small></div>
      </div>
      <div class="app-card kpi">
        <div class="kpi-label">進行中</div>
        <div class="kpi-value num">{{ activeCount }}<small>筆</small></div>
        <div class="kpi-sub ellipsis">{{ activeText || '目前沒有進行中的告警' }}</div>
      </div>
    </div>

    <div class="app-card q-pa-md">
      <div class="row items-center q-col-gutter-sm q-mb-md">
        <div class="col-12 col-sm-auto">
          <q-btn-toggle
            v-model="filter.status"
            unelevated
            no-caps
            toggle-color="primary"
            :toggle-text-color="mode === 'dark' ? 'black' : 'white'"
            class="level-toggle"
            :options="[{ label: '未確認', value: 'unack' }, { label: '進行中', value: 'active' }, { label: '全部', value: 'all' }]"
          />
        </div>
        <div class="col-6 col-sm-2">
          <q-select v-model="filter.type" dense outlined clearable emit-value map-options :options="typeOptions" label="類型" />
        </div>
        <div class="col-6 col-sm-2">
          <q-select v-model="filter.level" dense outlined clearable emit-value map-options :options="levelOptions" label="等級" />
        </div>
        <div class="col-6 col-sm-2"><q-input v-model="filter.from" dense outlined type="date" label="起" clearable /></div>
        <div class="col-6 col-sm-2"><q-input v-model="filter.to" dense outlined type="date" label="迄" clearable /></div>
        <q-space />
        <div class="col-auto">
          <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" :icon="mdiCheckAll"
            :label="`確認選取（${selected.length}）`" :disable="!selected.length" :loading="acking" @click="ack(selected.map((a) => a.id))" />
        </div>
      </div>
      <q-table
        v-model:selected="selected"
        class="app-table sticky"
        flat
        dense
        row-key="id"
        selection="multiple"
        :rows="rows"
        :columns="columns"
        :pagination="{ rowsPerPage: 20 }"
        :loading="loading"
        no-data-label="沒有符合條件的告警"
      >
        <template #body-cell-level="p">
          <q-td :props="p"><StatusTag group="alarm" :k="p.row.level" /></q-td>
        </template>
        <template #body-cell-endedAt="p">
          <q-td :props="p">
            <span v-if="p.row.endedAt">{{ fmtTime(p.row.endedAt) }}</span>
            <span v-else-if="AUTO_END.includes(p.row.type)" class="ongoing">進行中</span>
            <span v-else class="text-3">需人工確認</span>
          </q-td>
        </template>
        <template #body-cell-message="p">
          <q-td :props="p" class="wrap">
            {{ p.row.message }}
            <span v-if="p.row.source === 'history'" class="hist">歷史</span>
          </q-td>
        </template>
        <template #body-cell-ack="p">
          <q-td :props="p">
            <span v-if="p.row.acknowledgedAt" class="text-2">{{ p.row.acknowledgedBy }}<br /><span class="text-3">{{ fmtTime(p.row.acknowledgedAt) }}</span></span>
            <q-btn v-else flat dense no-caps color="primary" label="確認" @click="ack([p.row.id])" />
          </q-td>
        </template>
      </q-table>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, reactive, ref, watch } from 'vue'
import { Notify, type QTableColumn } from 'quasar'
import { mdiCheckAll, mdiRefresh } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import { $ackAlarms, $getAlarmSummary, $getAlarms } from '@/api'
import { errorMessage } from '@/api/http'
import { statusLabel } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtTime } from '@/utils/format'
import { ALARM_TYPE_LABEL, duration } from '@/utils/alarm'
import { addDays } from '@/utils/energy'
import type { Alarm, AlarmLevel, AlarmSummary, AlarmType } from '@/types/api'

const LEVELS: AlarmLevel[] = ['critical', 'error', 'warning', 'info']
/** 會自動結束的類型（電度倒退、資料缺漏要人工確認） */
const AUTO_END: AlarmType[] = ['demand_warn', 'over_contract', 'meter_offline', 'modbus_down']
const REFRESH_MS = 60_000

const { mode } = useTheme()
const rows = ref<Alarm[]>([])
const selected = ref<Alarm[]>([])
const summary = ref<AlarmSummary | null>(null)
const loading = ref(false)
const acking = ref(false)
const error = ref('')
const filter = reactive({ status: 'unack' as 'unack' | 'active' | 'all', type: null as AlarmType | null, level: null as AlarmLevel | null, from: '', to: '' })

const typeOptions = (Object.keys(ALARM_TYPE_LABEL) as AlarmType[]).map((t) => ({ value: t, label: ALARM_TYPE_LABEL[t] }))
const levelOptions = LEVELS.map((l) => ({ value: l, label: statusLabel('alarm', l) }))
const activeCount = computed(() => Object.values(summary.value?.activeByType ?? {}).reduce((a, b) => a + (b ?? 0), 0))
const activeText = computed(() =>
  (Object.entries(summary.value?.activeByType ?? {}) as [AlarmType, number][])
    .filter(([t]) => AUTO_END.includes(t))
    .map(([t, n]) => `${ALARM_TYPE_LABEL[t]} ${n}`)
    .join('、'),
)

const columns: QTableColumn<Alarm>[] = [
  { name: 'level', label: '等級', field: 'level', align: 'left', sortable: true },
  { name: 'type', label: '類型', field: 'type', align: 'left', format: (v: AlarmType) => ALARM_TYPE_LABEL[v], sortable: true },
  { name: 'startedAt', label: '開始', field: 'startedAt', align: 'left', format: (v) => fmtTime(v), sortable: true },
  { name: 'endedAt', label: '結束', field: 'endedAt', align: 'left' },
  { name: 'duration', label: '持續', field: (r) => r.startedAt, align: 'left', format: (_v, r) => (AUTO_END.includes(r.type) ? duration(r.startedAt, r.endedAt) : '—') },
  { name: 'meter', label: '電表', field: (r) => r.meterCode ?? '', align: 'left', format: (_v, r) => (r.meterCode ? `${r.meterCode}` : r.subject.startsWith('modbus:') ? r.subject.slice(7) : '全校') },
  { name: 'message', label: '訊息', field: 'message', align: 'left' },
  { name: 'ack', label: '確認', field: 'acknowledgedAt', align: 'left' },
]

async function load() {
  loading.value = true
  try {
    const [r, s] = await Promise.all([
      $getAlarms({
        status: filter.status, type: filter.type, level: filter.level, limit: 1000,
        // 日期以台北時間整天計
        from: filter.from ? `${filter.from}T00:00:00+08:00` : undefined,
        to: filter.to ? `${addDays(filter.to, 1)}T00:00:00+08:00` : undefined,
      }),
      $getAlarmSummary(),
    ])
    rows.value = r.data ?? []
    summary.value = s.data ?? null
    selected.value = selected.value.filter((x) => rows.value.some((y) => y.id === x.id && !y.acknowledgedAt))
    error.value = ''
  } catch (e) {
    error.value = errorMessage(e, '讀取告警失敗')
  } finally {
    loading.value = false
  }
}

async function ack(ids: number[]) {
  acking.value = true
  try {
    const n = (await $ackAlarms(ids)).data ?? 0
    Notify.create({ type: 'positive', message: `已確認 ${n} 筆告警` })
    selected.value = []
    window.dispatchEvent(new Event('ems-alarms-changed'))
    await load()
  } catch (e) {
    error.value = errorMessage(e, '確認失敗')
  } finally {
    acking.value = false
  }
}

watch(filter, load)
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
  grid-template-columns: repeat(5, minmax(0, 1fr));
  gap: 16px;
}
@media (max-width: 1023px) {
  .kpis {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}
.clickable {
  cursor: pointer;
}
.picked {
  box-shadow: inset 0 0 0 2px var(--accent);
}
.level-toggle {
  border: 1px solid var(--card-border);
  border-radius: 8px;
}
.wrap {
  white-space: normal;
  min-width: 260px;
}
.ongoing {
  font-weight: 600;
  color: var(--st-alarm-error-text);
}
.hist {
  margin-left: 6px;
  padding: 0 6px;
  font-size: 11px;
  border-radius: 999px;
  color: var(--text-3);
  background: var(--tile-bg);
  box-shadow: inset 0 0 0 1px var(--tile-border);
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

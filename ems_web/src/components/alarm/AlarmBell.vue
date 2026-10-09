<template>
  <q-btn flat round dense class="text-2" :icon="mdiBellOutline" aria-label="告警">
    <q-badge v-if="summary?.unacknowledged" floating rounded :style="{ background: `var(--st-alarm-${topLevel})` }" class="badge">
      {{ summary.unacknowledged > 99 ? '99+' : summary.unacknowledged }}
    </q-badge>
    <q-tooltip>未確認告警 {{ summary?.unacknowledged ?? 0 }} 筆</q-tooltip>
    <q-menu anchor="bottom right" self="top right" :offset="[0, 8]" @show="load">
      <div class="bell-menu">
        <div class="row items-center q-px-md q-py-sm">
          <b>未確認告警</b>
          <span class="text-3 q-ml-sm">{{ summary?.unacknowledged ?? 0 }} 筆</span>
          <q-space />
          <q-btn v-close-popup flat dense no-caps color="primary" label="全部告警" :to="pagePath('Alarm', 'AlarmEvents')" />
        </div>
        <q-separator />
        <q-list dense separator>
          <q-item v-for="a in summary?.latest ?? []" :key="a.id" v-close-popup clickable :to="pagePath('Alarm', 'AlarmEvents')">
            <q-item-section avatar style="min-width: 0">
              <StatusTag group="alarm" :k="a.level" />
            </q-item-section>
            <q-item-section>
              <q-item-label lines="2">{{ a.message }}</q-item-label>
              <q-item-label caption>{{ ALARM_TYPE_LABEL[a.type] }} · {{ fmtTime(a.startedAt) }}{{ a.endedAt ? ' · 已結束' : ' · 進行中' }}</q-item-label>
            </q-item-section>
          </q-item>
          <q-item v-if="!summary?.latest.length"><q-item-section class="text-3">沒有未確認的告警</q-item-section></q-item>
        </q-list>
      </div>
    </q-menu>
  </q-btn>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { mdiBellOutline } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import { $getAlarmSummary } from '@/api'
import { pagePath } from '@/router/menu'
import { ALARM_TYPE_LABEL } from '@/utils/alarm'
import { fmtTime } from '@/utils/format'
import type { AlarmLevel, AlarmSummary } from '@/types/api'

const REFRESH_MS = 30_000
const summary = ref<AlarmSummary | null>(null)
/** 徽章顏色＝未確認中最嚴重的等級 */
const topLevel = computed<AlarmLevel>(() => (['critical', 'error', 'warning', 'info'] as const).find((l) => summary.value?.byLevel[l]) ?? 'info')

async function load() {
  try {
    summary.value = (await $getAlarmSummary()).data ?? null
  } catch {
    // 鈴鐺讀不到不打擾使用者，下一輪再試
  }
}

// 告警頁確認之後通知鈴鐺立即更新
const onChanged = () => load()
let timer = 0
onMounted(() => {
  load()
  timer = window.setInterval(load, REFRESH_MS)
  window.addEventListener('ems-alarms-changed', onChanged)
})
onUnmounted(() => {
  clearInterval(timer)
  window.removeEventListener('ems-alarms-changed', onChanged)
})
</script>

<style scoped>
.badge {
  color: #fff;
  font-size: 10px;
}
.bell-menu {
  width: 380px;
  max-width: 92vw;
}
</style>

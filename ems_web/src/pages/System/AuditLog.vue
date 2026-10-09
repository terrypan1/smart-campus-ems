<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">稽核紀錄</div>
        <div class="page-sub">設定類的修改（電價、時段方案、離峰日、契約、排碳係數、電表、使用者）· 最近 300 筆</div>
      </div>
      <q-space />
      <q-btn flat dense no-caps class="text-2" :icon="mdiRefresh" label="重新整理" :loading="loading" @click="load" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="app-card q-pa-md">
      <q-table class="app-table" flat dense row-key="id" :rows="rows" :columns="columns" :pagination="{ rowsPerPage: 30 }" :loading="loading" no-data-label="還沒有修改紀錄">
        <template #body-cell-before="p">
          <q-td :props="p" class="json"><pre v-if="p.row.before">{{ pretty(p.row.before) }}</pre><span v-else class="text-3">—</span></q-td>
        </template>
        <template #body-cell-after="p">
          <q-td :props="p" class="json"><pre v-if="p.row.after">{{ pretty(p.row.after) }}</pre><span v-else class="text-3">—</span></q-td>
        </template>
      </q-table>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { onMounted, ref } from 'vue'
import type { QTableColumn } from 'quasar'
import { mdiRefresh } from '@quasar/extras/mdi-v7'
import { $getAudit } from '@/api'
import { errorMessage } from '@/api/http'
import { fmtTime } from '@/utils/format'
import type { AuditLog } from '@/types/api'

const rows = ref<AuditLog[]>([])
const loading = ref(false)
const error = ref('')
const pretty = (json: string) => {
  try {
    return JSON.stringify(JSON.parse(json), null, 1)
  } catch {
    return json
  }
}

const columns: QTableColumn<AuditLog>[] = [
  { name: 'at', label: '時間', field: 'at', align: 'left', format: (v) => fmtTime(v), sortable: true },
  { name: 'account', label: '帳號', field: 'account', align: 'left', sortable: true },
  { name: 'item', label: '項目', field: 'item', align: 'left', sortable: true },
  { name: 'before', label: '修改前', field: 'before', align: 'left' },
  { name: 'after', label: '修改後', field: 'after', align: 'left' },
]

async function load() {
  loading.value = true
  try {
    rows.value = (await $getAudit(300)).data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取稽核紀錄失敗')
  } finally {
    loading.value = false
  }
}

onMounted(load)
</script>

<style scoped>
.json pre {
  max-width: 420px;
  max-height: 160px;
  margin: 0;
  overflow: auto;
  font-family: var(--font-mono);
  font-size: 11px;
  white-space: pre-wrap;
  word-break: break-all;
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

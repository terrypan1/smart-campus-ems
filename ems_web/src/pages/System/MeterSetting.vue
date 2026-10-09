<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">電表設定</div>
        <div class="page-sub">接真實電表時只要改這裡的 Modbus 連線 · 不提供刪除（有歷史資料），只能停用 · 修改會寫入稽核紀錄</div>
      </div>
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="app-card q-pa-md">
      <div class="row items-center q-col-gutter-sm q-mb-md">
        <div class="col-12 col-sm-4 col-md-3">
          <q-input v-model="filter" dense outlined clearable placeholder="搜尋編號、名稱、大樓、host">
            <template #prepend><q-icon :name="mdiMagnify" size="18px" /></template>
          </q-input>
        </div>
        <q-space />
        <div class="col-auto text-2 text-caption">共 {{ meters.length }} 顆，停用 {{ meters.filter((m) => !m.isEnabled).length }} 顆</div>
      </div>
      <q-table class="app-table sticky" flat dense row-key="id" :rows="meters" :columns="columns" :filter="filter" :pagination="{ rowsPerPage: 0 }" hide-pagination :loading="loading">
        <template #body-cell-category="p">
          <q-td :props="p"><StatusTag group="category" :k="p.row.category" /></q-td>
        </template>
        <template #body-cell-isEnabled="p">
          <q-td :props="p">
            <StatusTag v-if="p.row.isEnabled" group="conn" k="online" label="啟用" />
            <StatusTag v-else group="conn" k="offline" label="停用" />
          </q-td>
        </template>
        <template #body-cell-actions="p">
          <q-td :props="p"><q-btn flat dense no-caps color="primary" :icon="mdiPencil" label="編輯" @click="openEdit(p.row)" /></q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="dlg.open">
      <q-card style="width: 520px; max-width: 94vw">
        <q-card-section class="text-h6">{{ dlg.code }} · {{ dlg.building }}</q-card-section>
        <q-card-section class="q-pt-none">
          <div class="form-grid">
            <q-input v-model="dlg.v.name" dense outlined label="名稱" class="span2" />
            <q-select v-model="dlg.v.category" dense outlined emit-value map-options :options="categoryOptions" label="用途" />
            <q-toggle v-model="dlg.v.isEnabled" label="啟用（停用＝不再採集、不算進全校目前需量）" />
            <q-input v-model="dlg.v.modbusHost" dense outlined label="Modbus host" />
            <q-input v-model.number="dlg.v.modbusPort" dense outlined type="number" label="Port" />
            <q-input v-model.number="dlg.v.unitId" dense outlined type="number" label="Unit ID" />
            <div class="row items-center">
              <q-btn outline no-caps color="primary" :icon="mdiLanConnect" label="測試連線" :loading="dlg.testing" @click="test" />
            </div>
          </div>
          <div v-if="dlg.test" class="test q-mt-sm" :class="dlg.test.ok ? 'ok' : 'fail'">
            {{ dlg.test.message }}（{{ dlg.test.elapsedMs }} ms）
            <template v-if="dlg.test.ok">
              · {{ fmtNum(dlg.test.kw, 1) }} kW · PF {{ fmtNum(dlg.test.pf, 3) }} · {{ fmtNum(dlg.test.vavg, 0) }} V · 累積 {{ fmtNum(dlg.test.kwhTotal, 2) }} kWh
            </template>
          </div>
          <div class="text-3 text-caption q-mt-sm">測試連線用上面填的值立即讀一次，不會寫入資料庫；還沒儲存也可以測。</div>
          <div v-if="dlg.error" class="text-negative text-caption q-mt-sm">{{ dlg.error }}</div>
        </q-card-section>
        <q-card-actions align="right">
          <q-btn v-close-popup flat no-caps label="取消" />
          <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" label="儲存" :loading="dlg.saving" @click="save" />
        </q-card-actions>
      </q-card>
    </q-dialog>
  </q-page>
</template>

<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { Notify, type QTableColumn } from 'quasar'
import { mdiLanConnect, mdiMagnify, mdiPencil } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import { $getMetersAdmin, $putMeter, $testMeter } from '@/api'
import { errorMessage } from '@/api/http'
import { STATUS, type StatusKey } from '@/theme/status'
import { useTheme } from '@/theme/useTheme'
import { fmtNum } from '@/utils/format'
import type { MeterAdmin, MeterTest, MeterUpdate } from '@/types/api'

const { mode } = useTheme()
const meters = ref<MeterAdmin[]>([])
const filter = ref('')
const loading = ref(false)
const error = ref('')
const categoryOptions = (Object.keys(STATUS.category) as StatusKey<'category'>[]).map((k) => ({ value: k, label: STATUS.category[k].label }))

const columns: QTableColumn<MeterAdmin>[] = [
  { name: 'code', label: '編號', field: 'code', align: 'left', sortable: true },
  { name: 'name', label: '名稱', field: 'name', align: 'left' },
  { name: 'buildingName', label: '大樓', field: 'buildingName', align: 'left', sortable: true },
  { name: 'category', label: '用途', field: 'category', align: 'left', sortable: true },
  { name: 'ratedKw', label: '額定 kW', field: 'ratedKw', classes: 'num' },
  { name: 'modbus', label: 'Modbus', field: (r) => `${r.modbusHost}:${r.modbusPort}`, align: 'left', classes: 'mono' },
  { name: 'unitId', label: 'Unit', field: 'unitId', classes: 'num' },
  { name: 'isEnabled', label: '狀態', field: 'isEnabled', align: 'left', sortable: true },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

async function load() {
  loading.value = true
  try {
    meters.value = (await $getMetersAdmin()).data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取電表失敗')
  } finally {
    loading.value = false
  }
}

const dlg = reactive({ open: false, id: 0, code: '', building: '', saving: false, testing: false, error: '', test: null as MeterTest | null, v: {} as MeterUpdate })
function openEdit(m: MeterAdmin) {
  Object.assign(dlg, {
    open: true, id: m.id, code: m.code, building: m.buildingName, error: '', test: null,
    v: { name: m.name, category: m.category, isEnabled: m.isEnabled, modbusHost: m.modbusHost, modbusPort: m.modbusPort, unitId: m.unitId },
  })
}
async function test() {
  dlg.testing = true
  try {
    dlg.test = (await $testMeter(dlg.id, dlg.v)).data ?? null
  } catch (e) {
    dlg.error = errorMessage(e, '測試失敗')
  } finally {
    dlg.testing = false
  }
}
async function save() {
  dlg.saving = true
  try {
    await $putMeter(dlg.id, dlg.v)
    dlg.open = false
    Notify.create({ type: 'positive', message: `已更新 ${dlg.code}，下一輪採集套用` })
    await load()
  } catch (e) {
    dlg.error = errorMessage(e, '儲存失敗')
  } finally {
    dlg.saving = false
  }
}

onMounted(load)
</script>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
.span2 {
  grid-column: 1 / -1;
}
.test {
  padding: 8px 12px;
  font-size: 13px;
  border-radius: var(--radius-sm);
}
.test.ok {
  color: var(--st-conn-online-text);
  background: var(--st-conn-online-soft);
}
.test.fail {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

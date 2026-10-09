<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">使用者</div>
        <div class="page-sub">角色：管理者（可改設定）／檢視者（只能看）· 不能停用或降級最後一個管理者 · 修改會寫入稽核紀錄</div>
      </div>
      <q-space />
      <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" :icon="mdiAccountPlusOutline" label="新增使用者" @click="openCreate" />
    </div>

    <q-banner v-if="error" class="app-card q-mb-md banner-error" dense>{{ error }}</q-banner>

    <div class="app-card q-pa-md">
      <q-table class="app-table" flat dense row-key="id" :rows="users" :columns="columns" :pagination="{ rowsPerPage: 0 }" hide-pagination :loading="loading">
        <template #body-cell-role="p">
          <q-td :props="p">{{ p.row.role === 'admin' ? '管理者' : '檢視者' }}</q-td>
        </template>
        <template #body-cell-isActive="p">
          <q-td :props="p">
            <StatusTag v-if="p.row.isActive" group="conn" k="online" label="啟用" />
            <StatusTag v-else group="conn" k="offline" label="停用" />
          </q-td>
        </template>
        <template #body-cell-actions="p">
          <q-td :props="p">
            <q-btn flat dense no-caps color="primary" :icon="mdiPencil" label="編輯" @click="openEdit(p.row)" />
            <q-btn flat dense no-caps class="text-2" :icon="mdiLockReset" label="重設密碼" @click="openReset(p.row)" />
          </q-td>
        </template>
      </q-table>
    </div>

    <q-dialog v-model="dlg.open">
      <q-card style="width: 420px; max-width: 92vw">
        <q-card-section class="text-h6">{{ dlg.mode === 'create' ? '新增使用者' : dlg.mode === 'edit' ? `編輯 ${dlg.account}` : `重設 ${dlg.account} 的密碼` }}</q-card-section>
        <q-card-section class="q-pt-none column q-gutter-sm">
          <q-input v-if="dlg.mode === 'create'" v-model="dlg.account" dense outlined label="帳號（英數字與 _ . -）" />
          <template v-if="dlg.mode !== 'reset'">
            <q-input v-model="dlg.displayName" dense outlined label="顯示名稱" />
            <q-select v-model="dlg.role" dense outlined emit-value map-options :options="[{ value: 'admin', label: '管理者' }, { value: 'viewer', label: '檢視者' }]" label="角色" />
            <q-toggle v-if="dlg.mode === 'edit'" v-model="dlg.isActive" label="啟用" />
          </template>
          <q-input v-if="dlg.mode !== 'edit'" v-model="dlg.password" dense outlined type="text" :label="dlg.mode === 'create' ? '初始密碼（至少 8 個字元）' : '臨時密碼（至少 8 個字元）'" autocomplete="new-password" />
          <div v-if="dlg.mode === 'reset'" class="text-3 text-caption">請把臨時密碼告訴使用者，登入後從右上角選單自行變更。</div>
          <div v-if="dlg.error" class="text-negative text-caption">{{ dlg.error }}</div>
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
import { mdiAccountPlusOutline, mdiLockReset, mdiPencil } from '@quasar/extras/mdi-v7'
import StatusTag from '@/components/status/StatusTag.vue'
import { $createUser, $getUsers, $putUser, $resetPassword } from '@/api'
import { errorMessage } from '@/api/http'
import { useTheme } from '@/theme/useTheme'
import { fmtTime } from '@/utils/format'
import type { Role, UserAdmin } from '@/types/api'

const { mode } = useTheme()
const users = ref<UserAdmin[]>([])
const loading = ref(false)
const error = ref('')

const columns: QTableColumn<UserAdmin>[] = [
  { name: 'account', label: '帳號', field: 'account', align: 'left', sortable: true },
  { name: 'displayName', label: '顯示名稱', field: 'displayName', align: 'left' },
  { name: 'role', label: '角色', field: 'role', align: 'left', sortable: true },
  { name: 'isActive', label: '狀態', field: 'isActive', align: 'left' },
  { name: 'createdAt', label: '建立時間', field: 'createdAt', align: 'left', format: (v) => fmtTime(v) },
  { name: 'actions', label: '', field: 'id', align: 'right' },
]

async function load() {
  loading.value = true
  try {
    users.value = (await $getUsers()).data ?? []
  } catch (e) {
    error.value = errorMessage(e, '讀取使用者失敗')
  } finally {
    loading.value = false
  }
}

const dlg = reactive({
  open: false, mode: 'create' as 'create' | 'edit' | 'reset', id: 0, account: '', displayName: '', role: 'viewer' as Role,
  isActive: true, password: '', saving: false, error: '',
})
const openCreate = () => Object.assign(dlg, { open: true, mode: 'create', id: 0, account: '', displayName: '', role: 'viewer', isActive: true, password: '', error: '' })
const openEdit = (u: UserAdmin) => Object.assign(dlg, { open: true, mode: 'edit', id: u.id, account: u.account, displayName: u.displayName, role: u.role, isActive: u.isActive, password: '', error: '' })
const openReset = (u: UserAdmin) => Object.assign(dlg, { open: true, mode: 'reset', id: u.id, account: u.account, password: '', error: '' })

async function save() {
  dlg.saving = true
  try {
    if (dlg.mode === 'create') await $createUser({ account: dlg.account, displayName: dlg.displayName, role: dlg.role, password: dlg.password })
    else if (dlg.mode === 'edit') await $putUser(dlg.id, { displayName: dlg.displayName, role: dlg.role, isActive: dlg.isActive })
    else await $resetPassword(dlg.id, dlg.password)
    dlg.open = false
    Notify.create({ type: 'positive', message: dlg.mode === 'reset' ? '已重設密碼' : '已儲存' })
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
.banner-error {
  color: var(--st-alarm-error-text);
  background: var(--st-alarm-error-soft);
}
</style>

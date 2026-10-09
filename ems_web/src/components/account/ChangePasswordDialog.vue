<template>
  <q-dialog v-model="open">
    <q-card style="width: 380px; max-width: 92vw">
      <q-card-section class="text-h6">變更密碼</q-card-section>
      <q-card-section class="q-pt-none column q-gutter-sm">
        <q-input v-model="f.old" dense outlined type="password" label="目前的密碼" autocomplete="current-password" />
        <q-input v-model="f.new1" dense outlined type="password" label="新密碼（至少 8 個字元）" autocomplete="new-password" />
        <q-input v-model="f.new2" dense outlined type="password" label="再輸入一次新密碼" autocomplete="new-password" />
        <div v-if="f.error" class="text-negative text-caption">{{ f.error }}</div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn v-close-popup flat no-caps label="取消" />
        <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" label="變更" :loading="f.saving" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
import { reactive, watch } from 'vue'
import { Notify } from 'quasar'
import { $changePassword } from '@/api'
import { errorMessage } from '@/api/http'
import { useTheme } from '@/theme/useTheme'

const open = defineModel<boolean>({ required: true })
const { mode } = useTheme()
const f = reactive({ old: '', new1: '', new2: '', error: '', saving: false })
watch(open, (v) => v && Object.assign(f, { old: '', new1: '', new2: '', error: '' }))

async function save() {
  if (f.new1 !== f.new2) return (f.error = '兩次輸入的新密碼不一樣')
  f.saving = true
  try {
    await $changePassword(f.old, f.new1)
    open.value = false
    Notify.create({ type: 'positive', message: '已變更密碼' })
  } catch (e) {
    f.error = errorMessage(e, '變更失敗')
  } finally {
    f.saving = false
  }
}
</script>

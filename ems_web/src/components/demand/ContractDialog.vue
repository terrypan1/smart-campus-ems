<template>
  <q-dialog v-model="open">
    <q-card style="width: 460px; max-width: 92vw">
      <q-card-section class="text-h6">修改契約容量與預警值</q-card-section>
      <q-card-section class="q-pt-none">
        <div class="form-grid">
          <q-input v-model.number="v.regularKw" type="number" dense outlined label="經常契約 kW" />
          <q-input v-model.number="v.halfPeakKw" type="number" dense outlined label="半尖峰契約 kW" />
          <q-input v-model.number="v.satHalfKw" type="number" dense outlined label="週六半尖峰契約 kW" />
          <q-input v-model.number="v.offPeakKw" type="number" dense outlined label="離峰契約 kW" />
          <q-input v-model.number="v.warnPct" type="number" dense outlined label="預警 %" />
          <q-input v-model.number="v.shedAirconPct" type="number" dense outlined label="卸載：空調 %" />
          <q-input v-model.number="v.shedFanPct" type="number" dense outlined label="卸載：進排風 %" />
          <q-input v-model.number="v.shedLightPct" type="number" dense outlined label="卸載：照明 %" />
        </div>
        <div class="text-3 text-caption q-mt-sm">儲存後會依新的契約重新補記歷史超約事件與告警；電費試算也會改用新契約。修改會寫入稽核紀錄。</div>
        <div v-if="err" class="text-negative text-caption q-mt-sm">{{ err }}</div>
      </q-card-section>
      <q-card-actions align="right">
        <q-btn v-close-popup flat no-caps label="取消" />
        <q-btn unelevated no-caps color="primary" :text-color="mode === 'dark' ? 'black' : 'white'" label="儲存" :loading="saving" @click="save" />
      </q-card-actions>
    </q-card>
  </q-dialog>
</template>

<script setup lang="ts">
// 契約容量與警戒門檻（需量監控頁、系統設定頁共用；PUT /api/Demand/Contract，限 admin）
import { ref, watch } from 'vue'
import { Notify } from 'quasar'
import { $putContract } from '@/api'
import { errorMessage } from '@/api/http'
import { useTheme } from '@/theme/useTheme'
import type { Contract, ContractUpdate } from '@/types/api'

const open = defineModel<boolean>({ required: true })
const props = defineProps<{ contract: Contract | null }>()
const emit = defineEmits<{ saved: [c: Contract] }>()
const { mode } = useTheme()
const v = ref({} as ContractUpdate)
const err = ref('')
const saving = ref(false)

watch(open, (o) => {
  const c = props.contract
  if (!o || !c) return
  v.value = {
    regularKw: c.regularKw, halfPeakKw: c.halfPeakKw, satHalfKw: c.satHalfKw, offPeakKw: c.offPeakKw,
    warnPct: c.warnPct, shedAirconPct: c.shedAirconPct, shedFanPct: c.shedFanPct, shedLightPct: c.shedLightPct,
  }
  err.value = ''
})

async function save() {
  saving.value = true
  try {
    const c = (await $putContract(v.value)).data
    open.value = false
    Notify.create({ type: 'positive', message: '已更新契約設定，歷史超約事件與告警在背景重新補記' })
    if (c) emit('saved', c)
  } catch (e) {
    err.value = errorMessage(e, '儲存失敗')
  } finally {
    saving.value = false
  }
}
</script>

<style scoped>
.form-grid {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: 12px;
}
</style>

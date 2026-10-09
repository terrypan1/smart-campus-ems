<template>
  <span class="anum">
    <span class="num">{{ shown }}</span>
    <transition name="delta">
      <span v-if="delta" :key="deltaKey" class="delta" :style="{ color: `var(--st-trend-${delta > 0 ? 'up' : 'down'}-text)` }">
        {{ delta > 0 ? '▲' : '▼' }} {{ fmtNum(Math.abs(delta), digits) }}
      </span>
    </transition>
  </span>
</template>

<script setup lang="ts">
// 即時數字：值變化時 0.6 秒從舊值滾到新值，旁邊顯示這次的增減 3 秒（用電增加紅、減少綠）
import { computed, onUnmounted, ref, watch } from 'vue'
import { fmtNum } from '@/utils/format'

const props = withDefaults(defineProps<{ value: number | null | undefined; digits?: number; showDelta?: boolean }>(), { digits: 0, showDelta: true })

const current = ref(props.value ?? null)
const delta = ref(0)
const deltaKey = ref(0)
let raf = 0
let timer = 0

const shown = computed(() => fmtNum(current.value, props.digits))

watch(
  () => props.value,
  (to, from) => {
    if (to == null || from == null) return void (current.value = to ?? null)
    const step = 10 ** -props.digits
    if (Math.abs(to - from) < step / 2) return
    if (props.showDelta) {
      delta.value = to - from
      deltaKey.value++
      clearTimeout(timer)
      timer = window.setTimeout(() => (delta.value = 0), 3000)
    }
    cancelAnimationFrame(raf)
    const start = performance.now()
    const run = (now: number) => {
      const t = Math.min(1, (now - start) / 600)
      current.value = from + (to - from) * (1 - (1 - t) ** 3)
      if (t < 1) raf = requestAnimationFrame(run)
    }
    raf = requestAnimationFrame(run)
  },
)

onUnmounted(() => {
  cancelAnimationFrame(raf)
  clearTimeout(timer)
})
</script>

<style scoped>
.anum {
  display: inline-flex;
  align-items: baseline;
  gap: 8px;
}
.delta {
  font-size: 14px;
  font-weight: 600;
}
.delta-enter-active,
.delta-leave-active {
  transition: opacity 0.4s;
}
.delta-enter-from,
.delta-leave-to {
  opacity: 0;
}
</style>

<template>
  <span class="st" :style="style">{{ label ?? statusLabel(group, String(k)) }}</span>
</template>

<script setup lang="ts" generic="G extends StatusGroup">
import { computed } from 'vue'
import { statusLabel, type StatusGroup, type StatusKey } from '@/theme/status'

const props = defineProps<{ group: G; k: StatusKey<G>; label?: string }>()
const style = computed(() => {
  const v = `--st-${props.group}-${String(props.k)}`
  return { color: `var(${v}-text)`, background: `var(${v}-soft)`, boxShadow: `inset 0 0 0 1px var(${v}-border)` }
})
</script>

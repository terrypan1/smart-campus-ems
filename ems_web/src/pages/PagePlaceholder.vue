<template>
  <q-page class="app-page">
    <div class="page-head">
      <div>
        <div class="page-title">{{ route.meta.title }}</div>
        <div class="page-sub">{{ route.meta.categoryName }}</div>
      </div>
    </div>
    <div class="app-card placeholder column items-center justify-center text-center">
      <q-icon :name="mdiHammerWrench" size="40px" class="text-3" />
      <div class="text-subtitle1 q-mt-md text-1">這一頁還在規劃中</div>
      <div class="text-2 q-mt-xs">會在後續階段實作，目前可先使用「即時監控 › 電表清單」。</div>
    </div>
  </q-page>
</template>

<script setup lang="ts">
import { useRoute } from 'vue-router'
import { mdiHammerWrench } from '@quasar/extras/mdi-v7'

const route = useRoute()
</script>

<style scoped>
.placeholder {
  min-height: 320px;
  padding: 32px;
}
</style>

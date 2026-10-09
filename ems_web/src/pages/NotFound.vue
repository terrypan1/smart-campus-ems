<template>
  <div class="fullscreen column items-center justify-center text-center q-pa-md">
    <div class="text-h3 text-weight-bold text-1">404</div>
    <div class="text-2 q-mt-sm">找不到這個頁面</div>
    <q-btn class="q-mt-lg btn-primary" unelevated no-caps label="回首頁" :to="HOME_PATH" />
  </div>
</template>

<script setup lang="ts">
import { HOME_PATH } from '@/router/menu'
</script>

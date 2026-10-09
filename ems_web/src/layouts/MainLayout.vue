<template>
  <q-layout view="lHh LpR lFf">
    <q-header class="app-chrome app-header">
      <q-toolbar class="q-px-md" style="height: var(--header-h)">
        <q-btn flat round dense :icon="mdiMenu" aria-label="開關側欄" class="text-2" @click="drawerOpen = !drawerOpen" />

        <div class="row items-center no-wrap q-ml-md crumbs ellipsis">
          <span class="text-3">{{ route.meta.categoryName }}</span>
          <q-icon :name="mdiChevronRight" size="16px" class="text-3 q-mx-xs" />
          <span class="text-weight-semibold text-1">{{ route.meta.title }}</span>
        </div>

        <q-space />

        <div class="row items-center no-wrap q-gutter-sm">
          <span class="mono text-2 q-mx-sm clock gt-sm">{{ clock }}</span>
          <AlarmBell />
          <q-btn flat round dense class="text-2" :icon="mode === 'dark' ? mdiWeatherSunny : mdiWeatherNight" :aria-label="themeLabel" @click="toggle">
            <q-tooltip>{{ themeLabel }}</q-tooltip>
          </q-btn>
          <q-btn flat dense no-caps class="q-pl-xs q-pr-sm text-1" aria-label="使用者">
            <q-icon :name="mdiAccountCircleOutline" size="24px" />
            <span class="q-ml-sm gt-xs">{{ auth.user?.displayName }}</span>
            <q-menu anchor="bottom right" self="top right" :offset="[0, 8]">
              <q-list dense style="min-width: 180px">
                <q-item>
                  <q-item-section>
                    <q-item-label>{{ auth.user?.account }}</q-item-label>
                    <q-item-label caption>{{ auth.user?.role === 'admin' ? '管理者' : '檢視者' }}</q-item-label>
                  </q-item-section>
                </q-item>
                <q-separator />
                <q-item v-close-popup clickable @click="pwdOpen = true">
                  <q-item-section avatar><q-icon :name="mdiKeyVariant" size="18px" /></q-item-section>
                  <q-item-section>變更密碼</q-item-section>
                </q-item>
                <q-item v-close-popup clickable @click="logout">
                  <q-item-section avatar><q-icon :name="mdiLogout" size="18px" /></q-item-section>
                  <q-item-section>登出</q-item-section>
                </q-item>
              </q-list>
            </q-menu>
          </q-btn>
        </div>
      </q-toolbar>
    </q-header>

    <AppSidebar v-model:open="drawerOpen" />
    <ChangePasswordDialog v-model="pwdOpen" />

    <q-page-container>
      <router-view />
    </q-page-container>
  </q-layout>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { mdiMenu, mdiChevronRight, mdiWeatherSunny, mdiWeatherNight, mdiAccountCircleOutline, mdiLogout, mdiKeyVariant } from '@quasar/extras/mdi-v7'
import AppSidebar from '@/components/navigation/AppSidebar.vue'
import AlarmBell from '@/components/alarm/AlarmBell.vue'
import ChangePasswordDialog from '@/components/account/ChangePasswordDialog.vue'
import { useTheme } from '@/theme/useTheme'
import { auth, clearAuth } from '@/stores/auth'

const route = useRoute()
const router = useRouter()
const { mode, toggle } = useTheme()
const themeLabel = computed(() => (mode.value === 'dark' ? '切換為淺色' : '切換為深色'))

// 寬度 > 1024 才固定在左邊，否則是浮層；一開始就打開的話窄螢幕會蓋住內容
const drawerOpen = ref(window.innerWidth > 1024)
const pwdOpen = ref(false)

const clock = ref('')
let timer = 0
const tick = () => (clock.value = new Date().toLocaleTimeString('zh-TW', { hour12: false, timeZone: 'Asia/Taipei' }))
onMounted(() => {
  tick()
  timer = window.setInterval(tick, 1000)
})
onUnmounted(() => clearInterval(timer))

function logout() {
  clearAuth()
  router.push('/login')
}
</script>

<style scoped>
.app-header {
  border-bottom: 1px solid var(--chrome-border);
}
.crumbs {
  font-size: 14px;
}
.clock {
  font-size: 13px;
}
</style>

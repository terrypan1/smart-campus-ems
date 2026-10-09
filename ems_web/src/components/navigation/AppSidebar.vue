<template>
  <q-drawer
    :model-value="open"
    show-if-above
    :width="240"
    :breakpoint="1024"
    class="app-chrome app-sidebar"
    @update:model-value="emit('update:open', $event)"
  >
    <router-link :to="HOME_PATH" class="row items-center no-wrap brand">
      <img src="/logo.svg" alt="EMS" width="30" height="30" />
      <div class="q-ml-sm">
        <div class="text-weight-bold text-1 brand-name">校園能源管理</div>
        <div class="text-3 brand-sub">Energy Management System</div>
      </div>
    </router-link>

    <q-scroll-area class="nav-scroll">
      <q-list class="q-pb-md">
        <q-expansion-item
          v-for="c in menu"
          :key="c.category"
          :icon="c.icon"
          :label="c.name"
          :default-opened="c.category === currentCategory"
          :header-class="['nav-item', { active: c.category === currentCategory }]"
          dense
        >
          <q-item v-for="p in c.pages" :key="p.key" clickable :to="pagePath(c.category, p.key)" class="nav-sub" active-class="active" dense>
            <q-item-section>{{ p.name }}</q-item-section>
          </q-item>
        </q-expansion-item>
      </q-list>
    </q-scroll-area>
  </q-drawer>
</template>

<script setup lang="ts">
import { computed } from 'vue'
import { useRoute } from 'vue-router'
import { auth } from '@/stores/auth'
import { HOME_PATH, pagePath, visibleMenu } from '@/router/menu'

defineProps<{ open: boolean }>()
const emit = defineEmits<{ 'update:open': [boolean] }>()

const route = useRoute()
const menu = computed(() => visibleMenu(auth.user?.role))
const currentCategory = computed(() => route.meta.category)
</script>

<style scoped>
.app-sidebar {
  border-right: 1px solid var(--chrome-border);
}
.brand {
  text-decoration: none;
  height: var(--header-h);
  padding: 0 18px;
  border-bottom: 1px solid var(--chrome-border);
}
.brand-name {
  font-size: 15px;
  line-height: 1.15;
}
.brand-sub {
  font-size: 11px;
}
.nav-scroll {
  height: calc(100% - var(--header-h));
}
.q-list {
  padding-top: 10px;
}
:deep(.nav-item) {
  margin: 1px 10px;
  border-radius: var(--radius-md);
  min-height: 40px;
  color: var(--text-2);
  font-weight: 500;
}
:deep(.nav-item:hover) {
  background: var(--nav-hover-bg);
}
:deep(.nav-item.active) {
  color: var(--accent-strong);
}
:deep(.nav-item .q-item__section--avatar) {
  min-width: 36px;
}
:deep(.q-expansion-item__toggle-icon) {
  font-size: 18px;
  color: var(--text-3);
}
.nav-sub {
  margin: 1px 10px 1px 22px;
  padding-left: 36px;
  border-radius: var(--radius-md);
  min-height: 34px;
  font-size: 13px;
  color: var(--text-2);
}
.nav-sub:hover {
  background: var(--nav-hover-bg);
}
.nav-sub.active {
  background: var(--nav-active-bg);
  color: var(--nav-active-text);
  font-weight: 600;
}
</style>

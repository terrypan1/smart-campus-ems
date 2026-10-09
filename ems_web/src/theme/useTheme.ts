import { computed, ref } from 'vue'
import { Dark } from 'quasar'
import { applyStatusVars, type ThemeMode } from './status'

const KEY = 'ems-theme'

// 唯一的主題狀態。<html data-theme> 跟著它走，CSS token 只看 data-theme。
// EMS 的 light 是真正的淺底深字，Quasar 元件（輸入框、表格、選單）要跟著切換 light／dark。
const mode = ref<ThemeMode>('light')

export function setTheme(m: ThemeMode) {
  mode.value = m
  Dark.set(m === 'dark')
  document.documentElement.dataset.theme = m
  applyStatusVars(m)
  localStorage.setItem(KEY, m)
}

export function initTheme() {
  setTheme(localStorage.getItem(KEY) === 'dark' ? 'dark' : 'light')
}

export function useTheme() {
  const toggle = () => setTheme(mode.value === 'dark' ? 'light' : 'dark')
  return { mode: computed(() => mode.value), toggle, setTheme }
}

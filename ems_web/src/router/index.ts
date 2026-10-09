import { createRouter, createWebHistory, type RouteRecordRaw } from 'vue-router'
import { LoadingBar, Notify } from 'quasar'
import { auth, isLoggedIn } from '@/stores/auth'
import { HOME_PATH, MENU, canSee, pagePath } from './menu'

const pageModules = import.meta.glob('/src/pages/*/*.vue')

// 選單裡每一頁都建路由；還沒做的頁面掛 PagePlaceholder
const menuRoutes: RouteRecordRaw[] = MENU.flatMap((c) =>
  c.pages.map((p) => {
    const page = pageModules[`/src/pages/${c.category}/${p.key}.vue`]
    return {
      path: pagePath(c.category, p.key).slice(1),
      name: `${c.category}_${p.key}`,
      component: page ?? (() => import('@/pages/PagePlaceholder.vue')),
      meta: { title: p.name, category: c.category, categoryName: c.name, roles: c.roles, implemented: !!page },
    } satisfies RouteRecordRaw
  }),
)

const router = createRouter({
  history: createWebHistory(),
  routes: [
    { path: '/login', name: 'Login', component: () => import('@/pages/LoginPage.vue'), meta: { title: '登入' } },
    { path: '/', component: () => import('@/layouts/MainLayout.vue'), redirect: HOME_PATH, children: menuRoutes },
    { path: '/:pathMatch(.*)*', name: 'NotFound', component: () => import('@/pages/NotFound.vue'), meta: { title: '找不到頁面' } },
  ],
})

router.beforeEach((to) => {
  if (!LoadingBar.isActive) LoadingBar.start()
  if (to.name === 'Login') return isLoggedIn() ? HOME_PATH : true
  if (to.name === 'NotFound') return true
  if (!isLoggedIn()) return { name: 'Login', query: to.fullPath !== '/' ? { redirect: to.fullPath } : {} }

  // 角色不符（例：viewer 直接打系統設定的網址）→ 回首頁
  const category = MENU.find((c) => c.category === to.meta.category)
  if (category && !canSee(category, auth.user?.role)) return HOME_PATH
})

router.afterEach((to) => {
  LoadingBar.stop()
  document.title = to.meta.title ? `${to.meta.title} · EMS` : 'EMS'
})

/* ---------- 部署後的舊分頁----------
 * 部署前就開著的分頁還拿著舊的入口檔，換頁時要的分頁檔（舊檔名）已經被新版換掉，會載入失敗
 * （2026-10-05 實際遇到：登入成功後停在登入頁、沒有任何提示）。遇到就整頁重新整理到原本要去的地方，拿新版。
 * 10 秒內只重整一次：真的載不到（例如伺服器掛了）時不會無限重整，改跳常駐提示。 */
const CHUNK_RELOAD_KEY = 'ems-chunk-reload-at'
export const isChunkLoadError = (e: unknown) =>
  /Failed to fetch dynamically imported module|Importing a module script failed|error loading dynamically imported module|is not a valid JavaScript MIME type/i.test(
    String((e as Error)?.message ?? e),
  )

export function reloadForNewBuild(path?: string) {
  const last = Number(sessionStorage.getItem(CHUNK_RELOAD_KEY) ?? 0)
  if (Date.now() - last < 10_000) return false
  sessionStorage.setItem(CHUNK_RELOAD_KEY, String(Date.now()))
  if (path) window.location.assign(path)
  else window.location.reload()
  return true
}

router.onError((err, to) => {
  LoadingBar.stop()
  if (!isChunkLoadError(err) || reloadForNewBuild(to?.fullPath)) return
  Notify.create({
    type: 'negative',
    message: '頁面載入失敗：可能剛更新版本或與伺服器連線中斷，請重新整理',
    timeout: 0,
    actions: [{ label: '重新整理', color: 'white', handler: () => window.location.reload() }],
  })
})
// Vite 預先載入相依檔失敗時發的事件（同一個原因）
window.addEventListener('vite:preloadError', (e) => {
  if (reloadForNewBuild()) e.preventDefault()
})

export default router

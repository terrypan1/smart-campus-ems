import 'vue-router'
import type { Role } from './api'

declare module 'vue-router' {
  interface RouteMeta {
    title?: string
    category?: string
    categoryName?: string
    roles?: Role[]
    /** false＝頁面還沒做，掛 PagePlaceholder */
    implemented?: boolean
  }
}

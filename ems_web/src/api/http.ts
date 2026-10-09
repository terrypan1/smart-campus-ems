import axios, { type AxiosError } from 'axios'
import type { ApiResponse } from '@/types/api'
import { clearAuth, getToken } from '@/stores/auth'

// 一律走同源相對路徑：開發由 vite proxy、部署由 nginx 轉到 ems_backend（見 vite.config.ts）
const http = axios.create({ timeout: 30000 })

http.interceptors.request.use((config) => {
  const token = getToken()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// 401：token 過期或無效 → 清掉登入資料回登入頁（登入本身的 401 交給登入頁顯示訊息）
http.interceptors.response.use(undefined, (error: AxiosError) => {
  if (error.code === 'ECONNABORTED') return Promise.reject(new Error('連線逾時，請稍後再試'))
  const url = (error.config?.url ?? '').toLowerCase()
  if (error.response?.status === 401 && !url.includes('/auth/login')) {
    clearAuth()
    if (location.pathname !== '/login') location.href = `/login?redirect=${encodeURIComponent(location.pathname)}`
  }
  return Promise.reject(error)
})

export const $get = <T>(url: string, params?: object) => http.get<ApiResponse<T>>(url, { params }).then((r) => r.data)
export const $post = <T, B = unknown>(url: string, body?: B) => http.post<ApiResponse<T>>(url, body).then((r) => r.data)
export const $put = <T, B = unknown>(url: string, body?: B) => http.put<ApiResponse<T>>(url, body).then((r) => r.data)
export const $delete = <T>(url: string) => http.delete<ApiResponse<T>>(url).then((r) => r.data)

/** 下載檔案（要帶 token，不能直接開網址）：檔名用後端 Content-Disposition */
export async function $download(url: string, params?: object) {
  const r = await http.get<Blob>(url, { params, responseType: 'blob' })
  const cd = String(r.headers['content-disposition'] ?? '')
  const name = decodeURIComponent(/filename\*=UTF-8''([^;]+)/i.exec(cd)?.[1] ?? /filename="?([^";]+)"?/i.exec(cd)?.[1] ?? 'download')
  const a = document.createElement('a')
  a.href = URL.createObjectURL(r.data)
  a.download = name
  a.click()
  URL.revokeObjectURL(a.href)
}

/** 從 axios 錯誤或後端回應裡取出給人看的訊息 */
export function errorMessage(e: unknown, fallback = '發生錯誤，請稍後再試') {
  const err = e as AxiosError<{ message?: string }> & { message?: string }
  if (err.response?.data?.message) return err.response.data.message
  if (err.response?.status === 502 || err.response?.status === 504 || err.code === 'ERR_NETWORK') return '無法連線到伺服器'
  return err.message || fallback
}

export default http

import { reactive } from 'vue'
import type { LoginResponse, UserInfo } from '@/types/api'

// 登入狀態（reactive＋localStorage）。key 加 ems- 前綴，同一台機器同時開其他系統不會互相覆蓋
const TOKEN = 'ems-token'
const USER = 'ems-user'
const EXPIRES = 'ems-expires-at'

const readUser = (): UserInfo | null => {
  try {
    return JSON.parse(localStorage.getItem(USER) ?? 'null')
  } catch {
    return null
  }
}

export const auth = reactive({ user: readUser() })

export const getToken = () => localStorage.getItem(TOKEN)

/** token 存在且未過期（Demo 不做 refresh token，過期就重新登入） */
export function isLoggedIn() {
  const expires = Date.parse(localStorage.getItem(EXPIRES) ?? '')
  return !!getToken() && !!auth.user && Number.isFinite(expires) && expires > Date.now()
}

export function saveAuth(d: LoginResponse) {
  localStorage.setItem(TOKEN, d.token)
  localStorage.setItem(EXPIRES, d.expiresAt)
  localStorage.setItem(USER, JSON.stringify(d.user))
  auth.user = d.user
}

export function clearAuth() {
  ;[TOKEN, USER, EXPIRES].forEach((k) => localStorage.removeItem(k))
  auth.user = null
}

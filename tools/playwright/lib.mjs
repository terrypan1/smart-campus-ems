// 共用：登入、主題。預設打部署環境 http://localhost:3020（EMS_BASE 可改，例：開發 3021）
import { chromium } from 'playwright'

export const BASE = process.env.EMS_BASE ?? 'http://localhost:3020'
export const ACCOUNTS = {
  sysadmin: process.env.EMS_SYSADMIN_PASSWORD ?? 'EmsSysadmin2026!',
  admin: process.env.EMS_ADMIN_PASSWORD ?? 'EmsAdmin2026!',
  viewer: process.env.EMS_VIEWER_PASSWORD ?? 'EmsViewer2026!',
}

export const launch = () => chromium.launch({ headless: true })

/** 開一個新的 context（主題各自獨立），登入後回傳 page 與收集到的錯誤 */
export async function session(browser, { theme = 'light', account = 'sysadmin', width = 1440, height = 900 } = {}) {
  const ctx = await browser.newContext({ viewport: { width, height } })
  const page = await ctx.newPage()
  const errors = []
  const failed = []
  page.on('console', (m) => m.type() === 'error' && errors.push(m.text()))
  page.on('pageerror', (e) => errors.push(String(e)))
  page.on('response', (r) => r.url().includes('/api/') && r.status() >= 400 && failed.push(`${r.status()} ${r.request().method()} ${new URL(r.url()).pathname}`))
  await page.goto(`${BASE}/login`)
  await page.evaluate((t) => localStorage.setItem('ems-theme', t), theme)
  await page.getByLabel('帳號').fill(account)
  // 登入頁有「顯示密碼」按鈕，label 會撞名，要 exact
  await page.getByLabel('密碼', { exact: true }).fill(ACCOUNTS[account])
  await page.keyboard.press('Enter')
  await page.waitForURL((u) => !u.pathname.startsWith('/login'))
  return { ctx, page, errors, failed }
}

export async function open(page, path, wait = 1500) {
  await page.goto(BASE + path)
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(wait)
}

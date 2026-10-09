// 開一個有畫面的瀏覽器給人看（自動登入），視窗關掉才結束
// 用法：node open.mjs [帳號] [路徑] [light|dark]，例：node open.mjs viewer /Demand/DemandMonitor dark
import { chromium } from 'playwright'
import { ACCOUNTS, BASE } from './lib.mjs'

const [account = 'sysadmin', path = '/Overview/EnergyOverview', theme = 'light'] = process.argv.slice(2)
const browser = await chromium.launch({ headless: false, args: ['--start-maximized'] })
const ctx = await browser.newContext({ viewport: null })
const page = await ctx.newPage()
await page.goto(`${BASE}/login`)
await page.evaluate((t) => localStorage.setItem('ems-theme', t), theme)
await page.getByLabel('帳號').fill(account)
await page.getByLabel('密碼', { exact: true }).fill(ACCOUNTS[account])
await page.keyboard.press('Enter')
await page.waitForURL((u) => !u.pathname.startsWith('/login'))
await page.goto(BASE + path)
console.log(`已開啟 ${BASE}${path}（${account}，${theme}）；關掉視窗即結束`)
await new Promise((resolve) => browser.on('disconnected', resolve))

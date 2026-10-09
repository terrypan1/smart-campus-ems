// 06：在告警頁勾選整頁、批次確認，確認未確認數減少、鈴鐺跟著更新
import { launch, open, session } from './lib.mjs'

const browser = await launch()
const { ctx, page, errors, failed } = await session(browser, { theme: 'light' })
await open(page, '/Alarm/AlarmEvents')
const badge = async () => Number((await page.locator('.q-badge').first().innerText().catch(() => '0')).replace('+', ''))
const before = await badge()
await page.locator('.q-table thead .q-checkbox').click()
const btn = page.getByRole('button', { name: /確認選取/ })
const label = await btn.innerText()
await btn.click()
await page.waitForTimeout(2000)
console.log(`鈴鐺 ${before} → ${await badge()}（${label.trim()}）`, 'console 錯誤:', errors, 'API 失敗:', failed)
await ctx.close()
await browser.close()

// 04 §8 #1 燈號驗證：攔截 /api/Demand/Realtime，把需量換成四種情境（不改資料庫的契約，避免留下測試事件）
// 用法：node status04.mjs <輸出資料夾>
import { launch, open, session } from './lib.mjs'

const out = process.argv[2]
const CASES = [
  { name: 'normal', kw: 1200, level: null, status: 'normal' },
  { name: 'warn', kw: 1450, level: 'warn', status: 'warn' },
  { name: 'shed', kw: 1720, level: 'shed_fan', status: 'warn' },
  { name: 'over', kw: 2010, level: 'over_contract', status: 'over' },
]
const browser = await launch()
for (const theme of ['light', 'dark']) {
  const { ctx, page, errors } = await session(browser, { theme })
  for (const c of CASES) {
    await page.unroute('**/api/Demand/Realtime')
    await page.route('**/api/Demand/Realtime', async (route) => {
      const res = await route.fetch()
      const body = await res.json()
      Object.assign(body.data, { netKw: c.kw, level: c.level, status: c.status, metersOnline: 51 })
      await route.fulfill({ response: res, json: body })
    })
    await open(page, '/Overview/EnergyOverview')
    const card = page.locator('.status-card')
    console.log(theme, c.name, '→', (await card.locator('.verdict').innerText()).trim())
    await card.screenshot({ path: `${out}/status-${c.name}-${theme}.png` })
  }
  console.log(theme, 'console 錯誤:', errors)
  await ctx.close()
}
await browser.close()

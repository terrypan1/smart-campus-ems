// 04 §8 #4：有超約的日子（預設 2026-07-01），截需量監控的 15 分鐘曲線卡
import { launch, open, session } from './lib.mjs'

const [out, date = '2026-07-01'] = process.argv.slice(2)
const browser = await launch()
for (const theme of ['light', 'dark']) {
  const { ctx, page, errors } = await session(browser, { theme })
  await open(page, '/Demand/DemandMonitor')
  await page.locator('input[type=date]').fill(date)
  await page.waitForLoadState('networkidle')
  await page.waitForTimeout(1200)
  await page.locator('.app-card', { hasText: '15 分鐘需量' }).first().screenshot({ path: `${out}/overday-${theme}.png` })
  console.log(theme, 'console 錯誤:', errors)
  await ctx.close()
}
await browser.close()

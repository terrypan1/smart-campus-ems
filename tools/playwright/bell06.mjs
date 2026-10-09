// 06：頂欄鈴鐺展開的樣子＋告警頁「進行中」
import { launch, open, session } from './lib.mjs'

const out = process.argv[2]
const browser = await launch()
for (const theme of ['light', 'dark']) {
  const { ctx, page, errors, failed } = await session(browser, { theme })
  await open(page, '/Alarm/AlarmEvents')
  await page.getByRole('button', { name: '進行中' }).click()
  await page.waitForTimeout(1200)
  await page.screenshot({ path: `${out}/alarm-active-${theme}.png`, fullPage: true })
  await page.getByRole('button', { name: '告警', exact: true }).click()
  await page.waitForTimeout(800)
  await page.screenshot({ path: `${out}/bell-${theme}.png` })
  console.log(theme, 'console 錯誤:', errors, 'API 失敗:', failed)
  await ctx.close()
}
await browser.close()

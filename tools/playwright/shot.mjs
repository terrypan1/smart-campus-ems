// 用法：node shot.mjs <輸出資料夾> name=/路徑 [name=/路徑 ...]
// 每頁截 light、dark 整頁圖，印出 console 錯誤與 API 4xx/5xx
import { launch, open, session } from './lib.mjs'

const [out, ...pages] = process.argv.slice(2)
const browser = await launch()
for (const theme of ['light', 'dark']) {
  const { ctx, page, errors, failed } = await session(browser, { theme })
  for (const p of pages) {
    const [name, path] = p.split('=')
    await open(page, path)
    await page.screenshot({ path: `${out}/${name}-${theme}.png`, fullPage: true })
  }
  console.log(theme, 'console 錯誤:', errors.length, errors.slice(0, 3), 'API 失敗:', failed)
  await ctx.close()
}
await browser.close()

// 05 §6 #5：在碳排放頁按「匯出 Excel」，確認瀏覽器真的下載到 xlsx
import { launch, open, session } from './lib.mjs'

const out = process.argv[2]
const browser = await launch()
const { ctx, page, errors } = await session(browser, { theme: 'light' })
await open(page, '/Esg/CarbonEmission')
const [dl] = await Promise.all([page.waitForEvent('download'), page.getByRole('button', { name: '匯出 Excel' }).click()])
await dl.saveAs(`${out}/${dl.suggestedFilename()}`)
console.log('下載檔名:', dl.suggestedFilename(), 'console 錯誤:', errors)
await ctx.close()
await browser.close()

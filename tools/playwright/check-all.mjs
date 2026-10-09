// 07 全頁回歸檢查（docs/實作/07 §2.1）：sysadmin、viewer × 淺色、深色 × 選單每一頁
// 檢查：頁面標題出現、沒有 console 錯誤、沒有 API 4xx/5xx；viewer 直接打系統設定網址要被導回首頁
// 用法：node check-all.mjs <截圖資料夾>；有任何失敗時 exit code 1
import { launch, open, session } from './lib.mjs'

const out = process.argv[2]
// 跟 ems_web/src/router/menu.ts 一致；admin＝只有管理者看得到
const PAGES = [
  ['Overview/EnergyOverview', '用電總覽'],
  ['Monitor/MeterList', '電表清單'],
  ['Analysis/UsageTrend', '用電趨勢'],
  ['Analysis/UsageStructure', '用電結構'],
  ['Demand/DemandMonitor', '需量監控'],
  ['Billing/BillEstimate', '電費估算'],
  ['Billing/TariffSetting', '電價設定'],
  ['Esg/CarbonEmission', '碳排放'],
  ['Alarm/AlarmEvents', '告警事件'],
  ['System/SystemParams', '契約與係數', 'admin'],
  ['System/MeterSetting', '電表設定', 'admin'],
  ['System/UserSetting', '使用者', 'admin'],
  ['System/AuditLog', '稽核紀錄', 'admin'],
]

const browser = await launch()
const problems = []
let checked = 0
for (const account of ['sysadmin', 'viewer']) {
  for (const theme of ['light', 'dark']) {
    const { ctx, page, errors, failed } = await session(browser, { theme, account })
    for (const [path, title, role] of PAGES) {
      errors.length = 0
      failed.length = 0
      await open(page, `/${path}`)
      const where = `${account}/${theme} ${path}`
      if (role === 'admin' && account === 'viewer') {
        // 沒有權限：導回首頁、側欄沒有這一頁
        if (!page.url().endsWith('/Overview/EnergyOverview')) problems.push(`${where}：viewer 沒有被導回首頁（${page.url()}）`)
        continue
      }
      const shown = await page.locator('.page-title').first().innerText().catch(() => '')
      if (!shown.startsWith(title)) problems.push(`${where}：標題不是「${title}」（${shown || '沒有標題'}）`)
      if (errors.length) problems.push(`${where}：console 錯誤 ${JSON.stringify(errors.slice(0, 2))}`)
      if (failed.length) problems.push(`${where}：API 失敗 ${failed.join('、')}`)
      await page.screenshot({ path: `${out}/${account}-${theme}-${path.replace('/', '_')}.png`, fullPage: true })
      checked++
    }
    const menuItems = await page.locator('.q-drawer .nav-item').count()
    console.log(`${account}/${theme}：側欄 ${menuItems} 個項目`)
    await ctx.close()
  }
}
await browser.close()
console.log(`檢查 ${checked} 頁，問題 ${problems.length} 個`)
for (const p of problems) console.log(' ✗', p)
process.exit(problems.length ? 1 : 0)

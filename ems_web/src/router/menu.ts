import {
  mdiViewDashboardOutline, mdiMonitorEye, mdiChartLine, mdiSpeedometer, mdiCashMultiple,
  mdiLeaf, mdiBellAlertOutline, mdiCogOutline,
} from '@quasar/extras/mdi-v7'
import type { Role } from '@/types/api'

// 選單固定寫在前端（也可以登入時由後端依角色給，Demo 簡化）。
// 頁面檔名＝ pages/{category}/{key}.vue；檔案還不存在就掛 PagePlaceholder，網址照常可進。
export interface MenuPage {
  key: string
  name: string
}
export interface MenuCategory {
  category: string
  name: string
  icon: string
  /** 沒寫＝所有角色都看得到 */
  roles?: Role[]
  pages: MenuPage[]
}

export const MENU: MenuCategory[] = [
  { category: 'Overview', name: '首頁', icon: mdiViewDashboardOutline, pages: [{ key: 'EnergyOverview', name: '用電總覽' }] },
  { category: 'Monitor', name: '即時監控', icon: mdiMonitorEye, pages: [{ key: 'MeterList', name: '電表清單' }] },
  {
    category: 'Analysis', name: '用電分析', icon: mdiChartLine,
    pages: [{ key: 'UsageTrend', name: '用電趨勢' }, { key: 'UsageStructure', name: '用電結構' }],
  },
  { category: 'Demand', name: '需量管理', icon: mdiSpeedometer, pages: [{ key: 'DemandMonitor', name: '需量監控' }] },
  {
    category: 'Billing', name: '電費', icon: mdiCashMultiple,
    pages: [{ key: 'BillEstimate', name: '電費估算' }, { key: 'TariffSetting', name: '電價設定' }],
  },
  { category: 'Esg', name: 'ESG', icon: mdiLeaf, pages: [{ key: 'CarbonEmission', name: '碳排放' }] },
  { category: 'Alarm', name: '告警', icon: mdiBellAlertOutline, pages: [{ key: 'AlarmEvents', name: '告警事件' }] },
  {
    category: 'System', name: '系統設定', icon: mdiCogOutline, roles: ['admin'],
    pages: [
      { key: 'SystemParams', name: '契約與係數' },
      { key: 'MeterSetting', name: '電表設定' },
      { key: 'UserSetting', name: '使用者' },
      { key: 'AuditLog', name: '稽核紀錄' },
    ],
  },
]

export const pagePath = (category: string, key: string) => `/${category}/${key}`

export const canSee = (c: MenuCategory, role: Role | undefined) => !c.roles || (!!role && c.roles.includes(role))

export const visibleMenu = (role: Role | undefined) => MENU.filter((c) => canSee(c, role))

/** 登入後的預設頁：用電總覽（docs/實作/04 §6） */
export const HOME_PATH = pagePath('Overview', 'EnergyOverview')

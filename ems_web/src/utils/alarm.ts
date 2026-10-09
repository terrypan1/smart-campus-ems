import type { AlarmType } from '@/types/api'

// 告警類型（docs/實作/06 §2.1）；等級的顏色與名稱走 status.ts 的 alarm 群組
export const ALARM_TYPE_LABEL: Record<AlarmType, string> = {
  demand_warn: '需量預警',
  over_contract: '超約',
  meter_offline: '電表斷線',
  modbus_down: 'Modbus 連線中斷',
  counter_decrease: '電度倒退',
  data_gap: '資料缺漏',
}

/** 持續時間（未結束算到現在） */
export function duration(startIso: string, endIso: string | null) {
  const ms = (endIso ? Date.parse(endIso) : Date.now()) - Date.parse(startIso)
  const m = Math.max(0, Math.round(ms / 60_000))
  if (m < 60) return `${m} 分`
  const h = Math.floor(m / 60)
  if (h < 48) return `${h} 小時 ${m % 60} 分`
  return `${Math.floor(h / 24)} 天 ${h % 24} 小時`
}

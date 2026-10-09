import type { HourStatus } from '@/types/api'

/** 小時狀態的說明（小時邊界規則：整點前後 20 分鐘內找不到讀值就無法計算） */
export const HOUR_STATUS_LABEL: Record<HourStatus, string> = {
  ok: '算得出來',
  no_start: '無法計算：這個小時開始的前後 20 分鐘內沒有讀值',
  no_end: '無法計算：這個小時結束的前後 20 分鐘內沒有讀值',
  no_both: '無法計算：這個小時開始和結束時都沒有讀值',
  counter_decrease: '無法計算：電表累積度數變小了（可能換表或資料異常）',
  pending: '還沒算到（這個小時結束 20 分鐘後才計算）',
}

/** 台北今天的日期字串 yyyy-MM-dd */
export function taipeiToday() {
  return new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Taipei' }).format(new Date())
}

export function addDays(date: string, days: number) {
  const d = new Date(`${date}T00:00:00Z`)
  d.setUTCDate(d.getUTCDate() + days)
  return d.toISOString().slice(0, 10)
}

export function addMonths(date: string, months: number) {
  const d = new Date(`${date.slice(0, 7)}-01T00:00:00Z`)
  d.setUTCMonth(d.getUTCMonth() + months)
  return d.toISOString().slice(0, 10)
}

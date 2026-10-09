// 數字、時間格式：顯示一律用台北時間（後端存 UTC）

export const fmtNum = (v: number | null | undefined, digits = 1) =>
  v == null || !Number.isFinite(v) ? '—' : v.toLocaleString('zh-TW', { minimumFractionDigits: digits, maximumFractionDigits: digits })

const timeFmt = new Intl.DateTimeFormat('zh-TW', {
  timeZone: 'Asia/Taipei', hour12: false, month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit',
})

export const fmtTime = (iso: string | null | undefined) => (iso ? timeFmt.format(new Date(iso)) : '—')

// 後端統一回應格式（ems_backend Utils/ApiResponse.cs）
export interface ApiResponse<T> {
  success: boolean
  message: string
  data?: T
  status: number
}

export type Role = 'admin' | 'viewer'

export interface UserInfo {
  id: number
  account: string
  displayName: string
  role: Role
}

export interface LoginResponse {
  token: string
  expiresAt: string
  user: UserInfo
}

/** 台電時間電價時段（週六半尖峰獨立，docs/實作/03 §5.1） */
export type Tou = 'peak' | 'half' | 'sat_half' | 'off'

export type MeterCategory = 'lighting_socket' | 'aircon' | 'power' | 'other' | 'solar'

export interface Meter {
  id: number
  code: string
  name: string
  category: MeterCategory
  ratedKw: number
  unitId: number
  isEnabled: boolean
  buildingId: number
  buildingCode: string
  buildingName: string
  spaceType: string
}

/** 一個期間（日／月）的用電：C 方案，見 docs/實作/02 §2.3 */
export interface EnergyPeriod {
  periodStart: string
  totalKwh: number
  measuredKwh: number
  peakKwh: number
  halfKwh: number
  satHalfKwh: number
  offKwh: number
  unassignedKwh: number
  coverage: number
  incompleteMeters: number
  meterCount: number
  solarKwh: number
}

export type HourStatus = 'ok' | 'no_start' | 'no_end' | 'no_both' | 'counter_decrease' | 'pending'

export interface HourlyEnergy {
  hourStart: string
  hour: number
  kwh: number | null
  status: HourStatus
  tou: Tou | ''
  startAt: string | null
  endAt: string | null
}

export interface LatestReading {
  meterId: number
  sampledAt: string | null
  online: boolean
  kw: number | null
  kvar: number | null
  pf: number | null
  demandKw: number | null
  vavg: number | null
  iavg: number | null
  hz: number | null
  kwhTotal: number | null
}

/* ---------- 需量（docs/實作/03 §5.4） ---------- */

export interface Contract {
  regularKw: number
  halfPeakKw: number
  satHalfKw: number
  offPeakKw: number
  warnPct: number
  shedAirconPct: number
  shedFanPct: number
  shedLightPct: number
  updatedAt: string
  updatedBy: string
}

export type ContractUpdate = Omit<Contract, 'updatedAt' | 'updatedBy'>

export type DemandLevel = 'warn' | 'shed_aircon' | 'shed_fan' | 'shed_light' | 'over_contract'

export interface RealtimeDemand {
  at: string
  blockStart: string
  elapsedSeconds: number
  loadKw: number
  solarKw: number
  /** 目前區塊已過部分的平均淨需量（用電－太陽能） */
  netKw: number
  instantNetKw: number
  tou: Tou
  capacityKw: number
  status: 'normal' | 'warn' | 'over'
  level: DemandLevel | null
  metersOnline: number
  metersTotal: number
  contract: Contract
}

export interface DemandBlock {
  blockStart: string
  tou: Tou
  loadKw: number | null
  solarKw: number | null
  netKw: number | null
  metersOk: number
  metersTotal: number
}

export interface TouMax {
  tou: Tou
  kw: number
  at: string
  capacityKw: number
  overKw: number
}

export interface MonthlyDemand {
  month: string
  byTou: TouMax[]
  maxKw: number
  maxAt: string | null
  overKw: number
  overBlocks: number
}

export interface DemandEvent {
  id: number
  occurredAt: string
  blockStart: string
  level: DemandLevel
  demandKw: number
  thresholdKw: number
  contractKw: number
  action: string
  source: 'live' | 'history'
}

/* ---------- 電費（docs/實作/03 §3） ---------- */

export type Season = 'summer' | 'nonsummer'

export interface BasicLine {
  season: Season
  item: string
  kw: number
  rate: number
  dayFraction: number
  amount: number
}

export interface OverageLine {
  tou: Tou
  maxDemandKw: number
  capacityKw: number
  overKw: number
  within10Kw: number
  beyond10Kw: number
  rate: number
  amount: number
}

export interface EnergyLine {
  season: Season
  tou: Tou
  kwh: number
  rate: number
  amount: number
}

export interface BillResult {
  summerDays: number
  nonSummerDays: number
  basic: BasicLine[]
  basicTotal: number
  powerFactorPct: number | null
  powerFactorAdjustPct: number
  powerFactorAdjustment: number
  overage: OverageLine[]
  overageTotal: number
  energy: EnergyLine[]
  energyTotal: number
  total: number
  taxIncluded: number
}

export interface BillEstimate {
  month: string
  isPartial: boolean
  daysWithData: number
  scheme: string
  peakKwh: number
  halfKwh: number
  satHalfKwh: number
  offKwh: number
  totalKwh: number
  allocatedKwh: number
  solarKwh: number
  contract: Contract
  maxDemands: { tou: Tou; kw: number; date: string }[]
  avgPowerFactor: number | null
  bill: BillResult
  solarSaving: number
}

export interface BillHistory {
  month: string
  isPartial: boolean
  totalKwh: number
  basicTotal: number
  powerFactorAdjustment: number
  overageTotal: number
  energyTotal: number
  total: number
  taxIncluded: number
  solarSaving: number
}

export interface TariffRate {
  season: Season
  item: string
  rate: number
  source: string
}

export interface Tariff {
  scheme: string
  schemeName: string
  rates: TariffRate[]
}

/* ---------- 總覽（docs/實作/04） ---------- */

/** 淨用電與去年同期：期間頭尾累積電度相減；lastYear 為 null＝去年同期沒有資料 */
export interface EnergyCompare {
  /** null＝一半以上電表期間頭尾讀值找不到，無法計算 */
  current: number | null
  lastYear: number | null
  from: string
  to: string
  lastYearFrom: string
  lastYearTo: string
  metersMissing: number
}

export interface Overview {
  generatedAt: string
  today: {
    maxKw: number | null
    maxAt: string | null
    maxTou: Tou | null
    capacityKw: number
    overBlocks: number
    blocksDone: number
    events: Partial<Record<DemandLevel, number>>
  }
  todayEnergy: EnergyCompare
  monthEnergy: EnergyCompare
  monthMax: { kw: number | null; at: string | null; overKw: number; lastYearKw: number | null; lastYearAt: string | null }
  /** null＝時段方案是二段式，不提供電費試算 */
  bill: { current: number; lastYear: number | null; through: string } | null
  carbon: { currentTon: number; lastYearTon: number | null; factor: number; factorYear: number; provisional: boolean }
  solar: { todayKwh: number | null; monthKwh: number; monthSaving: number }
  system: { metersOffline: number; metersTotal: number; monthCoverage: number; unackAlarms: number; activeAlarms: number }
}

/* ---------- 碳排與用電結構（docs/實作/05） ---------- */

export interface FactorUse {
  year: number
  factor: number
  factorYear: number
  provisional: boolean
  source: string
}

export interface CarbonMonth {
  month: string
  isPartial: boolean
  loadKwh: number
  solarKwh: number
  netKwh: number
  factor: number
  factorYear: number
  provisional: boolean
  emissionTon: number
  reductionTon: number
}

export interface CarbonSummary {
  year: number
  from: string
  to: string
  netKwh: number
  solarKwh: number
  emissionTon: number
  reductionTon: number
  factor: FactorUse
  lastYearFrom: string
  lastYearTo: string
  /** 去年同期實際有資料的第一天；晚於 lastYearFrom＝不完整 */
  lastYearDataFrom: string | null
  lastYearEmissionTon: number | null
  lastYearFactor: FactorUse
}

export interface EmissionFactor {
  year: number
  factor: number
  source: string
  isProvisional: boolean
}

export type StructureGroupBy = 'spaceType' | 'building' | 'category' | 'meter'

export interface StructureRow {
  key: string
  label: string
  spaceType: string | null
  buildingId: number | null
  kwh: number
  lastYearKwh: number | null
  share: number
  rank: number
  byCategory: Record<string, number>
  meterCount: number
}

export interface Structure {
  level: 'day' | 'month'
  from: string
  to: string
  lastYearFrom: string
  lastYearTo: string
  groupBy: StructureGroupBy
  totalKwh: number
  lastYearTotalKwh: number | null
  solarKwh: number
  rows: StructureRow[]
}

/* ---------- 告警與系統設定（docs/實作/06） ---------- */

export type AlarmType = 'demand_warn' | 'over_contract' | 'meter_offline' | 'modbus_down' | 'counter_decrease' | 'data_gap'
export type AlarmLevel = 'info' | 'warning' | 'error' | 'critical'

export interface Alarm {
  id: number
  type: AlarmType
  level: AlarmLevel
  subject: string
  meterId: number | null
  meterCode: string | null
  meterName: string | null
  startedAt: string
  endedAt: string | null
  lastSeenAt: string
  message: string
  detail: string
  acknowledgedAt: string | null
  acknowledgedBy: string | null
  source: 'live' | 'history'
}

export interface AlarmSummary {
  unacknowledged: number
  byLevel: Partial<Record<AlarmLevel, number>>
  activeByType: Partial<Record<AlarmType, number>>
  latest: Alarm[]
}

export interface TariffRateAdmin {
  season: Season
  item: string
  rate: number
  officialRate: number
  modified: boolean
  source: string
  updatedAt: string | null
  updatedBy: string | null
}

export interface SchemeInfo {
  scheme: 'HV3' | 'HV2'
  updatedAt: string | null
  updatedBy: string | null
}

export interface OffPeakDay {
  id: number | null
  date: string
  name: string
  /** 國曆固定日期（條文固定，每年都是） */
  fixed: boolean
}

export interface MeterAdmin {
  id: number
  code: string
  name: string
  category: MeterCategory
  ratedKw: number
  buildingId: number
  buildingName: string
  modbusHost: string
  modbusPort: number
  unitId: number
  isEnabled: boolean
}

export type MeterUpdate = Pick<MeterAdmin, 'name' | 'category' | 'isEnabled' | 'modbusHost' | 'modbusPort' | 'unitId'>

export interface MeterTest {
  ok: boolean
  message: string
  elapsedMs: number
  kw: number | null
  pf: number | null
  vavg: number | null
  kwhTotal: number | null
}

export interface UserAdmin {
  id: number
  account: string
  displayName: string
  role: Role
  isActive: boolean
  createdAt: string
}

export interface AuditLog {
  id: number
  at: string
  account: string
  item: string
  before: string | null
  after: string | null
}

import { $delete, $download, $get, $post, $put } from './http'
import type {
  Alarm, AlarmSummary, AuditLog, BillEstimate, BillHistory, CarbonMonth, MeterAdmin, MeterTest, MeterUpdate, OffPeakDay, SchemeInfo, TariffRateAdmin, UserAdmin, CarbonSummary, Contract, EmissionFactor, Structure, StructureGroupBy, ContractUpdate, DemandBlock, DemandEvent, EnergyPeriod, HourlyEnergy, LatestReading,
  LoginResponse, Meter, MonthlyDemand, Overview, RealtimeDemand, Tariff, UserInfo,
} from '@/types/api'

export const $login = (account: string, password: string) => $post<LoginResponse>('/api/Auth/Login', { account, password })
export const $me = () => $get<UserInfo>('/api/Auth/Me')

export const $getMeters = () => $get<Meter[]>('/api/Meters')
export const $getLatestReadings = () => $get<LatestReading[]>('/api/Meters/Latest')

export const $getEnergySummary = (level: 'day' | 'month', from: string, to: string, buildingId?: number | null) =>
  $get<EnergyPeriod[]>('/api/Energy/Summary', { level, from, to, buildingId: buildingId ?? undefined })
export const $getHourlyEnergy = (meterId: number, date: string) => $get<HourlyEnergy[]>('/api/Energy/Hourly', { meterId, date })

export const $getRealtimeDemand = () => $get<RealtimeDemand>('/api/Demand/Realtime')
export const $getDemandDay = (date: string) => $get<DemandBlock[]>('/api/Demand/Day', { date })
export const $getMonthlyDemand = (months = 13) => $get<MonthlyDemand[]>('/api/Demand/Monthly', { months })
export const $getDemandEvents = (from?: string, to?: string) => $get<DemandEvent[]>('/api/Demand/Events', { from, to })
export const $getContract = () => $get<Contract>('/api/Demand/Contract')
export const $putContract = (c: ContractUpdate) => $put<Contract>('/api/Demand/Contract', c)

export const $getBillEstimate = (month: string) => $get<BillEstimate>('/api/Billing/Estimate', { month })
export const $getBillHistory = (months = 13) => $get<BillHistory[]>('/api/Billing/History', { months })
export const $getTariff = () => $get<Tariff>('/api/Billing/Tariff')

export const $getOverview = () => $get<Overview>('/api/Overview')

export const $getCarbonMonthly = (months = 13) => $get<CarbonMonth[]>('/api/Carbon/Monthly', { months })
export const $getCarbonSummary = (year?: number) => $get<CarbonSummary>('/api/Carbon/Summary', { year })
export const $getEmissionFactors = () => $get<EmissionFactor[]>('/api/Carbon/Factors')
export const $exportCarbon = (months = 13) => $download('/api/Carbon/Export', { months })

export const $getStructure = (level: 'day' | 'month', from: string, to: string, groupBy: StructureGroupBy, buildingId?: number | null) =>
  $get<Structure>('/api/Energy/Structure', { level, from, to, groupBy, buildingId: buildingId ?? undefined })

export const $getAlarms = (q: { status?: 'active' | 'unack' | 'all'; type?: string | null; level?: string | null; from?: string; to?: string; limit?: number }) =>
  $get<Alarm[]>('/api/Alarms', { ...q, type: q.type ?? undefined, level: q.level ?? undefined })
export const $getAlarmSummary = () => $get<AlarmSummary>('/api/Alarms/Summary')
export const $ackAlarms = (ids: number[]) => $post<number>('/api/Alarms/Ack', { ids })

export const $getTariffAdmin = () => $get<TariffRateAdmin[]>('/api/Settings/Tariff')
export const $putTariff = (edits: { season: string; item: string; rate: number }[]) => $put<TariffRateAdmin[]>('/api/Settings/Tariff', edits)
export const $restoreTariff = () => $post<number>('/api/Settings/Tariff/Restore')
export const $getScheme = () => $get<SchemeInfo>('/api/Settings/Scheme')
export const $putScheme = (scheme: 'HV3' | 'HV2') => $put<SchemeInfo>('/api/Settings/Scheme', { scheme })
export const $getOffPeakDays = () => $get<OffPeakDay[]>('/api/Settings/OffPeakDays')
export const $addOffPeakDay = (date: string, name: string) => $post<OffPeakDay>('/api/Settings/OffPeakDays', { date, name })
export const $deleteOffPeakDay = (id: number) => $delete<null>(`/api/Settings/OffPeakDays/${id}`)
export const $recompute = () => $post<null>('/api/Energy/Recompute')
export const $putEmissionFactor = (f: { year: number; factor: number; source: string; isProvisional: boolean }) => $put<null>('/api/Settings/EmissionFactors', f)
export const $deleteEmissionFactor = (year: number) => $delete<null>(`/api/Settings/EmissionFactors/${year}`)

export const $getMetersAdmin = () => $get<MeterAdmin[]>('/api/Settings/Meters')
export const $putMeter = (id: number, m: MeterUpdate) => $put<null>(`/api/Settings/Meters/${id}`, m)
export const $testMeter = (id: number, m: MeterUpdate) => $post<MeterTest>(`/api/Settings/Meters/${id}/Test`, m)

export const $getUsers = () => $get<UserAdmin[]>('/api/Settings/Users')
export const $createUser = (u: { account: string; displayName: string; role: string; password: string }) => $post<UserAdmin>('/api/Settings/Users', u)
export const $putUser = (id: number, u: { displayName: string; role: string; isActive: boolean }) => $put<null>(`/api/Settings/Users/${id}`, u)
export const $resetPassword = (id: number, password: string) => $post<null>(`/api/Settings/Users/${id}/ResetPassword`, { password })
export const $changePassword = (oldPassword: string, newPassword: string) => $post<null>('/api/Settings/ChangePassword', { oldPassword, newPassword })
export const $getAudit = (limit = 300) => $get<AuditLog[]>('/api/Settings/Audit', { limit })

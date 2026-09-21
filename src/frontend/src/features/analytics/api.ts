import { authenticatedRequest } from '../auth/session'
import { ApiError } from '../../lib/api'

export const periods = [
  ['last30', 'Últimos 30 dias'], ['currentMonth', 'Mês atual'],
  ['previousMonth', 'Mês anterior'], ['last90', 'Últimos 90 dias'],
] as const
export type Comparison = { current: number; previous: number; changePercent: number | null }
export type FinancialPoint = { date: string; received: number; paid: number }
export type Dashboard = {
  asOf: string
  period: { key: string; from: string; to: string; previousFrom: string; previousTo: string; granularity: 'day' | 'month'; timeZone: string }
  financial: { received: number; paid: number; balance: number; receivable: number; payable: number; overdueReceivable: number; overduePayable: number; overdueCount: number }
  comparisons: { received: Comparison; paid: Comparison }
  financialSeries: FinancialPoint[]
  inventory: { activeProducts: number; lowStockProducts: number; zeroStockProducts: number; estimatedCostValue: number; staleProducts: number }
  movements: { entries: number; exits: number; count: number }
  lowStock: { id: string; name: string; sku: string; currentStock: number; minimumStock: number }[]
  topStockOut: { id: string; name: string; sku: string; quantity: number }[]
  staleProducts: { id: string; name: string; sku: string; currentStock: number; lastMovementAt: string | null }[]
  insights: { code: string; level: 'attention' | 'info' | 'positive'; message: string; evidence: string }[]
}
export const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
export const quantity = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
const percentage = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 1, signDisplay: 'exceptZero' })
export const percent = (value: number) => percentage.format(value) + '%'
export const date = (value: string) => value.split('-').reverse().join('/')
export function getDashboard(period: string, signal: AbortSignal) {
  return authenticatedRequest<Dashboard>('/api/dashboard?' + new URLSearchParams({ period }), { signal })
}
export function analysisError(error: unknown) {
  if (error instanceof ApiError && error.status === 400) return 'Selecione um período válido.'
  if (error instanceof ApiError && error.status === 403) return 'O acesso à empresa não está disponível.'
  return 'Não foi possível carregar os indicadores. Confira sua conexão e tente novamente.'
}

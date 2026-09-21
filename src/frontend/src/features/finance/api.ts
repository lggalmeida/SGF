import { authenticatedRequest } from '../auth/session'
import { ApiError } from '../../lib/api'

export type EntryType = 'Income' | 'Expense'
export type FinancialEntry = {
  id: string; type: EntryType; description: string; category: string | null
  amount: number; dueDate: string; paidAt: string | null; status: 'Pending' | 'Paid'
  notes: string | null; createdAt: string; updatedAt: string
}
export type FinanceInput = Pick<FinancialEntry, 'type' | 'description' | 'category' | 'amount' | 'dueDate' | 'notes'>
export type FinancePage = { items: FinancialEntry[]; page: number; pageSize: number; totalCount: number }
export type FinanceSummary = { received: number; paid: number; receivable: number; payable: number; balance: number }
export const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })
export function dateLabel(value: string) { return value.split('-').reverse().join('/') }
export function listFinance(query: string, signal: AbortSignal) {
  return authenticatedRequest<FinancePage>('/api/finance?' + query, { signal })
}
export function summary(signal: AbortSignal) { return authenticatedRequest<FinanceSummary>('/api/finance/summary', { signal }) }
export function saveEntry(data: FinanceInput, id?: string) {
  const { type, ...editable } = data
  return authenticatedRequest<FinancialEntry>(id ? '/api/finance/' + id : '/api/finance', {
    method: id ? 'PUT' : 'POST', body: JSON.stringify(id ? editable : { type, ...editable }),
  })
}
export function payEntry(id: string) {
  return authenticatedRequest<FinancialEntry>('/api/finance/' + id + '/pay', { method: 'PATCH' })
}
export function financeError(error: unknown, writing = false) {
  if (error instanceof ApiError) {
    if (error.code === 'finance_paid') return 'Este lançamento já foi pago e não pode ser editado.'
    if (error.status === 400) return 'Confira os campos, as datas e o valor positivo com até duas casas decimais.'
    if (error.status === 404) return 'Lançamento não encontrado. Atualize a lista.'
    if (error.status === 409) return 'O lançamento mudou. Atualize a lista antes de continuar.'
    if (error.status === 403) return 'Sua conta não tem acesso a esta empresa.'
  }
  return writing ? 'Não foi possível confirmar a operação. Confira a lista antes de repetir.' : 'Não foi possível carregar os dados. Confira sua conexão.'
}

import { authenticatedRequest } from '../auth/session'
import { ApiError } from '../../lib/api'

export type InventoryItem = { productId: string; name: string; sku: string; currentStock: number; minimumStock: number; isLowStock: boolean; isActive: boolean }
export type MovementType = 'Entry' | 'Exit'
export type Movement = { id: string; productId: string; productName: string; sku: string; type: MovementType; quantity: number; notes: string | null; createdAt: string; userId: string; userName: string | null }
export type InventoryPage = { items: InventoryItem[]; page: number; pageSize: number; totalCount: number }
export type MovementPage = { items: Movement[]; page: number; pageSize: number; totalCount: number }
export type Receipt = { id: string; productId: string; type: MovementType; quantity: number; currentStock: number }
export const quantityFormat = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: 3 })
export const dateFormat = new Intl.DateTimeFormat('pt-BR', { dateStyle: 'short', timeStyle: 'short' })

export function inventory(page: number, search: string, lowStock: boolean, signal: AbortSignal) {
  const params = new URLSearchParams({ page: String(page), pageSize: '20', search, lowStock: String(lowStock) })
  return authenticatedRequest<InventoryPage>('/api/inventory?' + params, { signal })
}
export function movements(page: number, productId: string, type: string, signal: AbortSignal) {
  const params = new URLSearchParams({ page: String(page), pageSize: '20' })
  if (productId) params.set('productId', productId)
  if (type) params.set('type', type)
  return authenticatedRequest<MovementPage>('/api/inventory/movements?' + params, { signal })
}
export function move(type: MovementType, productId: string, quantity: number, notes: string) {
  return authenticatedRequest<Receipt>('/api/inventory/' + (type === 'Entry' ? 'entries' : 'exits'), {
    method: 'POST', body: JSON.stringify({ productId, quantity, notes: notes.trim() || null }),
  })
}
export function inventoryError(error: unknown, writing = false) {
  if (error instanceof ApiError) {
    if (error.code === 'insufficient_stock') return 'Estoque insuficiente para esta saída.'
    if (error.code === 'stock_conflict') return 'O saldo ou status mudou. Atualize e confira os dados antes de tentar novamente.'
    if (error.code === 'inactive_product') return 'Este produto está inativo e não pode ser movimentado.'
    if (error.code === 'stock_limit') return 'A entrada ultrapassa o limite de saldo permitido.'
    if (error.status === 404) return 'Produto não encontrado.'
    if (error.status === 400) return 'Confira a quantidade: maior que zero, até três casas decimais. A observação aceita até 1.000 caracteres.'
  }
  return writing
    ? 'Não foi possível confirmar a operação. Confira o histórico antes de repetir.'
    : 'Não foi possível concluir. Confira sua conexão e tente novamente.'
}

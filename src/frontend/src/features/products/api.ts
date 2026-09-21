import { authenticatedRequest } from '../auth/session'
import { ApiError } from '../../lib/api'

export type Product = {
  id: string; name: string; sku: string; description: string | null
  costPrice: number; salePrice: number; isActive: boolean; createdAt: string; updatedAt: string
  currentStock: number; minimumStock: number
}
export type ProductInput = Pick<Product, 'name' | 'sku' | 'description' | 'costPrice' | 'salePrice' | 'minimumStock'>
export type ProductPage = { items: Product[]; page: number; pageSize: number; totalCount: number }

export function listProducts(page: number, pageSize: number, search: string, status: string, signal: AbortSignal) {
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize), search })
  if (status !== 'all') query.set('isActive', status)
  return authenticatedRequest<ProductPage>('/api/products?' + query, { signal })
}
export function saveProduct(data: ProductInput, id?: string) {
  return authenticatedRequest<Product>(id ? '/api/products/' + id : '/api/products', {
    method: id ? 'PUT' : 'POST', body: JSON.stringify(data),
  })
}
export function setProductStatus(id: string, isActive: boolean) {
  return authenticatedRequest<Product>('/api/products/' + id + '/status', {
    method: 'PATCH', body: JSON.stringify({ isActive }),
  })
}
export function productError(error: unknown) {
  if (error instanceof ApiError) {
    if (error.code === 'duplicate_sku') return 'Este SKU já está cadastrado na sua empresa, inclusive entre produtos inativos.'
    if (error.status === 400) return 'Confira nome, SKU, descrição, preços não negativos (duas casas decimais) e estoque mínimo não negativo (três casas decimais).'
    if (error.status === 404) return 'Produto não encontrado. Atualize a lista e tente novamente.'
    if (error.status === 409) return 'O produto mudou. Atualize a lista e tente novamente.'
  }
  return 'Não foi possível concluir. Confira sua conexão e tente novamente.'
}

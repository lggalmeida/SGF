import { useRef, useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ChevronLeft, ChevronRight, LoaderCircle, Package, Pencil, Plus, Power, Search } from 'lucide-react'
import { PageHeader, EmptyState } from '../../components/PageContent'
import { useAuth } from '../auth/AuthProvider'
import { listProducts, productError, setProductStatus } from './api'
import type { Product } from './api'
import { ProductForm } from './ProductForm'
import './products.css'

const money = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' })

export function ProductsPage() {
  const { user } = useAuth()
  const cache = useQueryClient()
  const [page, setPage] = useState(1)
  const [pageSize, setPageSize] = useState(20)
  const [search, setSearch] = useState('')
  const [draft, setDraft] = useState('')
  const [status, setStatus] = useState('all')
  const [editor, setEditor] = useState<{ product: Product | null } | null>(null)
  const [notice, setNotice] = useState('')
  const opener = useRef<HTMLElement | null>(null)
  // Company identifies the cache only; it is never sent to the API.
  const queryKey = ['products', user!.company.id]
  const query = useQuery({
    queryKey: [...queryKey, page, pageSize, search, status],
    queryFn: ({ signal }) => listProducts(page, pageSize, search, status, signal),
    retry: false,
  })
  const statusMutation = useMutation({
    mutationFn: ({ id, isActive }: { id: string; isActive: boolean }) => setProductStatus(id, isActive),
    onSuccess: async product => {
      setNotice(product.isActive ? 'Produto reativado.' : 'Produto desativado.')
      setPage(1)
      await cache.invalidateQueries({ queryKey })
      await cache.invalidateQueries({ queryKey: ['inventory', user!.company.id] })
    },
  })
  function open(product: Product | null) {
    opener.current = document.activeElement as HTMLElement
    setNotice('')
    setEditor({ product })
  }
  function close() {
    setEditor(null)
    opener.current?.focus()
  }
  async function saved(editing: boolean) {
    setPage(1)
    await cache.invalidateQueries({ queryKey })
    await cache.invalidateQueries({ queryKey: ['inventory', user!.company.id] })
    close()
    setNotice(editing ? 'Produto atualizado com sucesso.' : 'Produto cadastrado com sucesso.')
  }
  const data = query.data
  const pages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / pageSize))
  return <section className="products-page">
    <PageHeader title="Produtos" description="Gerencie o catálogo de produtos da sua empresa.">
      <button className="primary product-create" onClick={() => open(null)}><Plus size={18} aria-hidden="true" />Novo produto</button>
    </PageHeader>
    {notice && <p className="message success" role="status">{notice}</p>}
    {statusMutation.isError && <p className="message error" role="alert">{productError(statusMutation.error)}</p>}
    <div className="products-toolbar">
      <form className="product-search" role="search" onSubmit={event => { event.preventDefault(); setSearch(draft.trim()); setPage(1) }}>
        <input aria-label="Buscar por nome ou SKU" placeholder="Buscar por nome ou SKU" maxLength={200} value={draft} onChange={event => setDraft(event.target.value)} />
        <button className="icon-button" type="submit" aria-label="Buscar produtos" title="Buscar produtos"><Search size={18} /></button>
      </form>
      <label className="product-status-filter">Status<select value={status} onChange={event => { setStatus(event.target.value); setPage(1) }}><option value="all">Todos</option><option value="true">Ativos</option><option value="false">Inativos</option></select></label>
    </div>
    {query.isPending ? <div className="loading" role="status"><LoaderCircle className="loading-icon" />Carregando produtos…</div>
      : query.isError ? <div className="products-error"><p className="message error" role="alert">{productError(query.error)}</p><button className="secondary" onClick={() => void query.refetch()}>Tentar novamente</button></div>
      : data && <>
        {data.items.length === 0 ? <div className="products-empty">
          <EmptyState icon={Package} title={search || status !== 'all' || page > 1 ? 'Nenhum produto encontrado' : 'Nenhum produto cadastrado'}
            description={search || status !== 'all' || page > 1 ? 'Tente outro nome, SKU ou status.' : 'Cadastre seu primeiro produto para começar.'} />
          {search || status !== 'all' || page > 1 ? <button className="secondary" onClick={() => { setDraft(''); setSearch(''); setStatus('all'); setPage(1) }}>Limpar filtros</button>
            : <button className="primary product-create" onClick={() => open(null)}><Plus size={18} />Novo produto</button>}
        </div> : <table className="products-table">
          <caption className="visually-hidden">Produtos da empresa atual</caption>
          <thead><tr><th scope="col">Nome</th><th scope="col">SKU</th><th scope="col" className="product-cost">Custo</th><th scope="col">Venda</th><th scope="col">Status</th><th scope="col"><span className="visually-hidden">Ações</span></th></tr></thead>
          <tbody>{data.items.map(product => <tr key={product.id}>
            <td className="product-name"><strong>{product.name}</strong></td>
            <td className="product-sku" data-label="SKU">{product.sku}</td>
            <td className="product-cost" data-label="Custo">{money.format(product.costPrice)}</td>
            <td className="product-sale" data-label="Venda">{money.format(product.salePrice)}</td>
            <td className="product-status"><span className={product.isActive ? 'status-badge active' : 'status-badge inactive'}>{product.isActive ? 'Ativo' : 'Inativo'}</span></td>
            <td className="product-actions">
              <button className="icon-button" title="Editar produto" aria-label={'Editar ' + product.name} onClick={() => open(product)}><Pencil size={17} /></button>
              <button className="icon-button" title={product.isActive ? 'Desativar produto' : 'Reativar produto'} aria-label={(product.isActive ? 'Desativar ' : 'Reativar ') + product.name} disabled={statusMutation.isPending}
                onClick={() => { if (!product.isActive || window.confirm('Desativar "' + product.name + '"? O produto continuará no catálogo como inativo.')) statusMutation.mutate({ id: product.id, isActive: !product.isActive }) }}><Power size={17} /></button>
            </td>
          </tr>)}</tbody>
        </table>}
        <div className="products-pagination">
          <span>{data.totalCount} {data.totalCount === 1 ? 'produto' : 'produtos'}</span>
          <label>Por página<select value={pageSize} onChange={event => { setPageSize(Number(event.target.value)); setPage(1) }}><option>20</option><option>50</option><option>100</option></select></label>
          <div><button className="icon-button" title="Página anterior" aria-label="Página anterior" disabled={page <= 1 || query.isFetching} onClick={() => setPage(page - 1)}><ChevronLeft size={18} /></button>
            <span>Página {page} de {pages}</span>
            <button className="icon-button" title="Próxima página" aria-label="Próxima página" disabled={page >= pages || query.isFetching} onClick={() => setPage(page + 1)}><ChevronRight size={18} /></button></div>
        </div>
      </>}
    {editor && <ProductForm product={editor.product} onClose={close} onSaved={saved} />}
  </section>
}

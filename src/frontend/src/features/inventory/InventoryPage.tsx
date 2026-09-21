import { useEffect, useRef, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowDownToLine, ArrowUpFromLine, ChevronLeft, ChevronRight, History, Package, Search } from 'lucide-react'
import { EmptyState, PageHeader } from '../../components/PageContent'
import { useAuth } from '../auth/AuthProvider'
import { inventory, movements, inventoryError, dateFormat, quantityFormat } from './api'
import type { InventoryItem, MovementType } from './api'
import { MovementForm } from './MovementForm'
import './inventory.css'

function Pagination({ page, total, loading, onChange }: { page: number; total: number; loading: boolean; onChange: (page: number) => void }) {
  const pages = Math.max(1, Math.ceil(total / 20))
  return <div className="inventory-pagination"><span>{total} {total === 1 ? 'registro' : 'registros'}</span><div>
    <button className="icon-button" aria-label="Página anterior" title="Página anterior" disabled={page === 1 || loading} onClick={() => onChange(page - 1)}><ChevronLeft size={18} /></button>
    <span>Página {page} de {pages}</span>
    <button className="icon-button" aria-label="Próxima página" title="Próxima página" disabled={page >= pages || loading} onClick={() => onChange(page + 1)}><ChevronRight size={18} /></button>
  </div></div>
}

export function InventoryPage() {
  const { user } = useAuth()
  const [params, setParams] = useSearchParams()
  const history = params.get('view') === 'movements'
  const productId = params.get('productId') ?? ''
  const page = Math.max(1, Math.min(1000000, Number(params.get('page')) || 1))
  const type = params.get('type') ?? ''
  const search = params.get('search') ?? ''
  const low = params.get('lowStock') === 'true'
  const [draft, setDraft] = useState(search)
  useEffect(() => { setDraft(search) }, [search])
  const [notice, setNotice] = useState('')
  const [editor, setEditor] = useState<{ item: InventoryItem; type: MovementType } | null>(null)
  const opener = useRef<HTMLElement | null>(null)
  function change(values: Record<string, string>) {
    const next = new URLSearchParams(params)
    for (const [key, value] of Object.entries(values)) { if (value) next.set(key, value); else next.delete(key) }
    setParams(next)
  }
  const stock = useQuery({
    queryKey: ['inventory', user!.company.id, 'stock', page, search, low],
    queryFn: ({ signal }) => inventory(page, search, low, signal), enabled: !history, retry: false,
  })
  const log = useQuery({
    queryKey: ['inventory', user!.company.id, 'movements', page, productId, type],
    queryFn: ({ signal }) => movements(page, productId, type, signal), enabled: history, retry: false,
  })
  const active = history ? log : stock
  function open(item: InventoryItem, kind: MovementType) {
    opener.current = document.activeElement as HTMLElement
    setNotice('')
    setEditor({ item, type: kind })
  }
  function close() { setEditor(null); opener.current?.focus() }
  return <section className="inventory-page">
    <PageHeader title="Estoque" description="Acompanhe o saldo dos produtos da sua empresa." />
    <nav className="inventory-tabs" aria-label="Seções de estoque">
      <Link to="/app/inventory" aria-current={!history ? 'page' : undefined}><Package size={17} />Estoque</Link>
      <Link to="/app/inventory?view=movements" aria-current={history ? 'page' : undefined}><History size={17} />Movimentações</Link>
    </nav>
    {notice && <p className="message success" role="status">{notice}</p>}
    {!history ? <div className="inventory-toolbar">
      <form role="search" onSubmit={e => { e.preventDefault(); change({ search: draft.trim(), page: '1' }) }}>
        <input aria-label="Buscar por nome ou SKU" placeholder="Buscar por nome ou SKU" maxLength={200} value={draft} onChange={e => setDraft(e.target.value)} />
        <button className="icon-button" aria-label="Buscar estoque" title="Buscar estoque"><Search size={18} /></button>
      </form>
      <label className="checkbox-label"><input type="checkbox" checked={low} onChange={e => change({ lowStock: e.target.checked ? 'true' : '', page: '1' })} />Somente estoque baixo</label>
    </div> : <div className="inventory-toolbar">
      <div>{productId ? <Link to="/app/inventory?view=movements">Ver todos os produtos</Link> : <span>Todos os produtos</span>}</div>
      <label className="inventory-type">Tipo<select value={type} onChange={e => change({ type: e.target.value, page: '1' })}><option value="">Todos</option><option value="Entry">Entrada</option><option value="Exit">Saída</option></select></label>
    </div>}
    {active.isPending ? <div className="loading" role="status">Carregando estoque…</div>
      : active.isError ? <div><p className="message error" role="alert">{inventoryError(active.error)}</p><button className="secondary" onClick={() => void active.refetch()}>Tentar novamente</button></div>
      : history && log.data ? <>
        {log.data.items.length === 0 ? <EmptyState icon={History} title={type ? 'Nenhuma movimentação encontrada.' : 'Nenhuma movimentação registrada.'} description={type ? 'Tente outro filtro.' : 'Registre uma entrada de estoque para começar.'} />
          : <table className="inventory-table movement-table"><caption className="visually-hidden">Histórico de movimentações</caption><thead><tr><th>Data</th><th>Produto</th><th>Tipo</th><th>Quantidade</th><th>Usuário</th><th>Observação</th></tr></thead>
            <tbody>{log.data.items.map(m => <tr key={m.id}>
              <td data-label="Data"><time dateTime={m.createdAt}>{dateFormat.format(new Date(m.createdAt))}</time></td>
              <td className="inventory-name"><strong>{m.productName}</strong><small>{m.sku}</small></td>
              <td data-label="Tipo"><span className={'movement-badge ' + m.type}>{m.type === 'Entry' ? <ArrowDownToLine size={14} /> : <ArrowUpFromLine size={14} />}{m.type === 'Entry' ? 'Entrada' : 'Saída'}</span></td>
              <td data-label="Quantidade">{quantityFormat.format(m.quantity)}</td>
              <td data-label="Usuário">{m.userName ?? 'Usuário indisponível'}</td>
              <td data-label="Observação" className="movement-notes">{m.notes || 'Sem observação'}</td>
            </tr>)}</tbody></table>}
        <Pagination page={page} total={log.data.totalCount} loading={log.isFetching} onChange={p => change({ page: String(p) })} />
      </> : stock.data && <>
        {stock.data.items.length === 0 ? <div className="inventory-empty"><EmptyState icon={Package}
          title={search || low ? 'Nenhum produto encontrado' : 'Nenhum produto cadastrado'}
          description={search || low ? 'Tente outro filtro de estoque.' : 'Cadastre um produto para registrar sua primeira entrada.'} />
          {search || low ? <button className="secondary" onClick={() => { setDraft(''); change({ search: '', lowStock: '', page: '1' }) }}>Limpar filtros</button> : <Link to="/app/products">Ir para Produtos</Link>}</div>
          : <table className="inventory-table stock-table"><caption className="visually-hidden">Saldo dos produtos</caption><thead><tr><th>Produto / SKU</th><th>Atual</th><th>Mínimo</th><th>Situação</th><th><span className="visually-hidden">Ações</span></th></tr></thead>
            <tbody>{stock.data.items.map(item => <tr key={item.productId}>
              <td className="inventory-name"><strong>{item.name}</strong><small>{item.sku}</small></td>
              <td data-label="Atual" className="stock-current">{quantityFormat.format(item.currentStock)}</td>
              <td data-label="Mínimo">{quantityFormat.format(item.minimumStock)}</td>
              <td data-label="Situação"><span className={'stock-badge ' + (item.isLowStock ? 'low' : '')}>{!item.isActive ? 'Inativo' : item.isLowStock ? 'Estoque baixo' : 'Normal'}</span></td>
              <td className="inventory-actions">
                <button className="icon-button" title="Entrada de estoque" aria-label={'Entrada de ' + item.name} disabled={!item.isActive} onClick={() => open(item, 'Entry')}><ArrowDownToLine size={18} /></button>
                <button className="icon-button" title="Saída de estoque" aria-label={'Saída de ' + item.name} disabled={!item.isActive} onClick={() => open(item, 'Exit')}><ArrowUpFromLine size={18} /></button>
                <Link className="icon-button" title="Ver histórico" aria-label={'Histórico de ' + item.name} to={'/app/inventory?view=movements&productId=' + item.productId}><History size={18} /></Link>
              </td>
            </tr>)}</tbody></table>}
        <Pagination page={page} total={stock.data.totalCount} loading={stock.isFetching} onChange={p => change({ page: String(p) })} />
      </>}
    {editor && <MovementForm item={editor.item} type={editor.type} onClose={close} onSaved={receipt => {
      close()
      setNotice((receipt.type === 'Entry' ? 'Entrada registrada.' : 'Saída registrada.') + ' Saldo atual: ' + quantityFormat.format(receipt.currentStock))
    }} />}
  </section>
}

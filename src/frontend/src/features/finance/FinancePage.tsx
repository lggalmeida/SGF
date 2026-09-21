import { useRef, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { ArrowDownLeft, ArrowUpRight, Check, ChevronLeft, ChevronRight, Eye, LoaderCircle, Pencil, Plus, Search, Wallet } from 'lucide-react'
import { PageHeader, EmptyState } from '../../components/PageContent'
import { useAuth } from '../auth/AuthProvider'
import { dateLabel, financeError, listFinance, money, payEntry, summary } from './api'
import type { EntryType, FinancialEntry, FinanceSummary } from './api'
import { FinanceForm } from './FinanceForm'
import '../products/products.css'
import './finance.css'

const totals: { key: keyof FinanceSummary; label: string }[] = [
  { key: 'balance', label: 'Saldo realizado' }, { key: 'received', label: 'Recebido' },
  { key: 'paid', label: 'Pago' }, { key: 'receivable', label: 'A receber' }, { key: 'payable', label: 'A pagar' },
]

export function FinancePage() {
  const { user } = useAuth()
  const cache = useQueryClient()
  const [params, setParams] = useSearchParams()
  const [editor, setEditor] = useState<{ entry: FinancialEntry | null; type: EntryType } | null>(null)
  const [notice, setNotice] = useState('')
  const opener = useRef<HTMLElement | null>(null)
  const key = ['finance', user!.company.id]
  const query = useQuery({ queryKey: [...key, 'list', params.toString()], queryFn: ({ signal }) => listFinance(params.toString(), signal), retry: false })
  const overview = useQuery({ queryKey: [...key, 'summary'], queryFn: ({ signal }) => summary(signal), retry: false })
  async function invalidate() { await cache.invalidateQueries({ queryKey: key }) }
  const payment = useMutation({
    mutationFn: payEntry, retry: false,
    onSuccess: async entry => { setNotice(entry.type === 'Income' ? 'Recebimento confirmado.' : 'Pagamento confirmado.'); await invalidate() },
    onError: invalidate,
  })
  function open(type: EntryType, entry: FinancialEntry | null = null) {
    opener.current = document.activeElement as HTMLElement
    setNotice('')
    setEditor({ entry, type })
  }
  function close() { setEditor(null); opener.current?.focus() }
  async function saved() {
    const editing = Boolean(editor?.entry)
    await invalidate()
    close()
    setNotice(editing ? 'Lançamento atualizado.' : 'Lançamento cadastrado.')
  }
  function changePage(page: number) { const next = new URLSearchParams(params); next.set('page', String(page)); setParams(next) }
  const data = query.data
  const pages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? 20)))
  return <section className="finance-page">
    <PageHeader title="Financeiro" description="Organize suas receitas, despesas e compromissos.">
      <div className="finance-create"><button className="primary" onClick={() => open('Income')}><Plus size={17} />Nova receita</button>
        <button className="secondary" onClick={() => open('Expense')}><Plus size={17} />Nova despesa</button></div>
    </PageHeader>
    {notice && <p className="message success" role="status">{notice}</p>}
    {payment.isError && <p className="message error" role="alert">{financeError(payment.error, true)}</p>}
    <section aria-label="Totais gerais" className="finance-overview">
      <h2>Totais gerais</h2>
      {overview.isError ? <div><p className="message error" role="alert">{financeError(overview.error)}</p><button className="secondary" onClick={() => void overview.refetch()}>Recarregar totais</button></div>
        : <div className="finance-summary" aria-busy={overview.isPending}>{totals.map(({ key, label }) =>
          <article key={key} className={'finance-stat ' + key} aria-label={label}><h3>{label}</h3>
            <p>{overview.isPending ? <span aria-label="Carregando total">…</span> : money.format(overview.data![key])}</p></article>)}</div>}
    </section>
    <form className="finance-filters" key={params.toString()} aria-label="Filtrar lançamentos" onSubmit={event => {
      event.preventDefault()
      const fields = new FormData(event.currentTarget)
      const next = new URLSearchParams()
      for (const name of ['search', 'type', 'status', 'category', 'from', 'to']) {
        const value = String(fields.get(name) ?? '').trim()
        if (value) next.set(name, value)
      }
      setParams(next)
    }}>
      <label className="finance-search">Buscar descrição<input name="search" placeholder="Buscar lançamento" maxLength={200} defaultValue={params.get('search') ?? ''} /></label>
      <label>Tipo<select name="type" defaultValue={params.get('type') ?? ''}><option value="">Todos</option><option value="Income">Receita</option><option value="Expense">Despesa</option></select></label>
      <label>Status<select name="status" defaultValue={params.get('status') ?? ''}><option value="">Todos</option><option value="Pending">Pendente</option><option value="Paid">Pago / Recebido</option></select></label>
      <label>Categoria<input name="category" maxLength={100} placeholder="Todas" defaultValue={params.get('category') ?? ''} /></label>
      <label>Vencimento de<input name="from" type="date" max="9999-12-31" defaultValue={params.get('from') ?? ''} /></label>
      <label>Até<input name="to" type="date" max="9999-12-31" defaultValue={params.get('to') ?? ''} /></label>
      <div className="finance-filter-actions"><button className="secondary" type="submit"><Search size={16} />Filtrar</button>
        {params.size > 0 && <button className="text-button" type="button" onClick={() => setParams({})}>Limpar</button>}</div>
    </form>
    {query.isPending ? <div className="loading" role="status"><LoaderCircle className="loading-icon" />Carregando lançamentos…</div>
      : query.isError ? <div><p className="message error" role="alert">{financeError(query.error)}</p><button className="secondary" onClick={() => void query.refetch()}>Tentar novamente</button></div>
      : data && <>
        {data.items.length === 0 ? <EmptyState icon={Wallet} title={params.size ? 'Nenhum lançamento encontrado' : 'Nenhum lançamento financeiro'}
          description={params.size ? 'Revise os filtros da busca.' : 'Cadastre sua primeira receita ou despesa.'} />
          : <table className="finance-table"><caption className="visually-hidden">Lançamentos da empresa atual</caption>
            <thead><tr><th>Descrição</th><th>Tipo</th><th>Categoria</th><th>Valor</th><th>Vencimento</th><th>Status</th><th><span className="visually-hidden">Ações</span></th></tr></thead>
            <tbody>{data.items.map(entry => <tr key={entry.id}>
              <td className="finance-description"><strong>{entry.description}</strong></td>
              <td className="finance-type"><span className={'finance-badge ' + entry.type}>{entry.type === 'Income' ? <ArrowDownLeft size={13} /> : <ArrowUpRight size={13} />}{entry.type === 'Income' ? 'Receita' : 'Despesa'}</span></td>
              <td className="finance-category" data-label="Categoria">{entry.category ?? 'Sem categoria'}</td>
              <td className="finance-amount" data-label="Valor">{money.format(entry.amount)}</td>
              <td className="finance-due" data-label="Vencimento">{dateLabel(entry.dueDate)}</td>
              <td className="finance-status"><span className={'finance-badge ' + entry.status}>{entry.status === 'Pending' ? 'Pendente' : entry.type === 'Income' ? 'Recebido' : 'Pago'}</span></td>
              <td className="finance-actions">
                <button className="icon-button" title={entry.status === 'Pending' ? 'Editar lançamento' : 'Ver detalhes'} aria-label={(entry.status === 'Pending' ? 'Editar ' : 'Ver detalhes de ') + entry.description} onClick={() => open(entry.type, entry)}>
                  {entry.status === 'Pending' ? <Pencil size={17} /> : <Eye size={17} />}</button>
                {entry.status === 'Pending' && <button className="icon-button" disabled={payment.isPending}
                  title={entry.type === 'Income' ? 'Marcar como recebido' : 'Marcar como pago'}
                  aria-label={(entry.type === 'Income' ? 'Receber ' : 'Pagar ') + entry.description}
                  onClick={() => { if (window.confirm('Confirmar ' + (entry.type === 'Income' ? 'recebimento' : 'pagamento') + ' de ' + money.format(entry.amount) + '? O lançamento não poderá mais ser editado.')) { setNotice(''); payment.mutate(entry.id) } }}>
                  {payment.isPending && payment.variables === entry.id ? <LoaderCircle size={17} className="loading-icon" /> : <Check size={17} />}</button>}
              </td>
            </tr>)}</tbody>
          </table>}
        <div className="finance-pagination"><span>{data.totalCount} {data.totalCount === 1 ? 'lançamento' : 'lançamentos'}</span>
          <div><button className="icon-button" title="Página anterior" aria-label="Página anterior" disabled={data.page <= 1 || query.isFetching} onClick={() => changePage(data.page - 1)}><ChevronLeft size={18} /></button>
            <span>Página {data.page} de {pages}</span><button className="icon-button" title="Próxima página" aria-label="Próxima página" disabled={data.page >= pages || query.isFetching} onClick={() => changePage(data.page + 1)}><ChevronRight size={18} /></button></div></div>
      </>}
    {editor && <FinanceForm entry={editor.entry} type={editor.type} onClose={close} onSaved={saved} />}
  </section>
}

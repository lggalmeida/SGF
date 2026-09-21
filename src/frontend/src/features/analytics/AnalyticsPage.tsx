import { lazy, Suspense } from 'react'
import type { ReactNode } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import { ArrowDownLeft, ArrowUpRight, ArrowRight, ChartNoAxesCombined, CircleAlert, Info, LoaderCircle, Package, RefreshCw, Wallet } from 'lucide-react'
import { PageHeader, EmptyState } from '../../components/PageContent'
import { useAuth } from '../auth/AuthProvider'
import { analysisError, date, getDashboard, money, percent, periods, quantity } from './api'
import type { Comparison, Dashboard } from './api'
import './analytics.css'

const FinancialChart = lazy(() => import('./FinancialChart'))

function Metric({ label, value, note, children, tone = '' }: { label: string; value: string; note?: string; children?: ReactNode; tone?: string }) {
  return <article className={'analysis-metric ' + tone} aria-label={label}><h2>{label}</h2><p className="analysis-value">{value}</p>{note && <p className="metric-note">{note}</p>}{children}</article>
}
function Variation({ value, expense = false }: { value: Comparison; expense?: boolean }) {
  if (value.changePercent === null) return <p className="comparison-unavailable">Sem comparação disponível</p>
  const good = expense ? value.changePercent < 0 : value.changePercent > 0
  return <p className={'metric-change ' + (value.changePercent === 0 ? '' : good ? 'positive' : 'attention')}>{percent(value.changePercent)} <span>vs. período anterior</span></p>
}
function FinancialMetrics({ data }: { data: Dashboard }) {
  const f = data.financial
  return <>
    <div className="analysis-main-metrics">
      <Metric label="Receitas recebidas" value={money.format(f.received)}><Variation value={data.comparisons.received} /></Metric>
      <Metric label="Despesas pagas" value={money.format(f.paid)}><Variation value={data.comparisons.paid} expense /></Metric>
      <Metric label="Saldo realizado" value={money.format(f.balance)} tone="balance" note="Recebido menos pago no período" />
    </div>
    <div className="analysis-secondary-metrics">
      <Metric label="A receber" value={money.format(f.receivable)} note="Todos os pendentes atuais" />
      <Metric label="A pagar" value={money.format(f.payable)} note="Todos os pendentes atuais" />
      <Metric label="Produtos ativos" value={quantity.format(data.inventory.activeProducts)} note="Situação atual" />
      <Metric label="Estoque baixo" value={quantity.format(data.inventory.lowStockProducts)} note="Saldo atual ≤ mínimo" tone={data.inventory.lowStockProducts ? 'warning' : ''} />
    </div>
  </>
}
function FinancialSeries({ data }: { data: Dashboard }) {
  const hasData = data.financialSeries.some(p => p.received !== 0 || p.paid !== 0)
  return <section className="analysis-section" aria-label="Receitas x despesas">
    <div className="analysis-section-title"><h2><ChartNoAxesCombined size={18} />Receitas x despesas</h2><span>{data.period.granularity === 'day' ? 'Por dia' : 'Por mês'} · R$</span></div>
    <p className="analysis-caption">Pagamentos e recebimentos confirmados no período.</p>
    {hasData ? <Suspense fallback={<div className="financial-chart loading" role="status">Carregando gráfico…</div>}><FinancialChart points={data.financialSeries} monthly={data.period.granularity === 'month'} /></Suspense>
      : <EmptyState icon={ChartNoAxesCombined} title="Sem valores realizados no período" description="Os indicadores aparecerão conforme sua empresa registrar movimentações." />}
    {hasData && <details className="analysis-data"><summary>Valores por {data.period.granularity === 'day' ? 'dia' : 'mês'}</summary>
      <table><caption className="visually-hidden">Dados do gráfico financeiro</caption><thead><tr><th>Data</th><th>Recebido</th><th>Pago</th></tr></thead>
        <tbody>{data.financialSeries.map(p => <tr key={p.date}><td>{data.period.granularity === 'month' ? p.date.slice(5, 7) + '/' + p.date.slice(0, 4) : date(p.date)}</td><td>{money.format(p.received)}</td><td>{money.format(p.paid)}</td></tr>)}</tbody></table></details>}
    {data.period.granularity === 'month' && <p className="analysis-caption">Meses nas extremidades incluem somente os dias do intervalo selecionado.</p>}
  </section>
}
function LowStock({ data }: { data: Dashboard }) {
  return <section className="analysis-section" aria-label="Reposição de estoque">
    <div className="analysis-section-title"><h2><Package size={18} />Reposição de estoque</h2><Link to="/app/inventory?lowStock=true" aria-label="Abrir estoque baixo" title="Abrir estoque baixo"><ArrowRight size={18} /></Link></div>
    <p className="analysis-caption">{data.inventory.lowStockProducts} produtos no limite ou abaixo do mínimo · até 10 casos</p>
    {data.lowStock.length ? <ul className="analysis-products">{data.lowStock.map(p => <li key={p.id}>
      <div><strong>{p.name}</strong><small>{p.sku}</small></div><div className="stock-comparison"><strong className={p.currentStock === 0 ? 'zero-stock' : ''}>{quantity.format(p.currentStock)}</strong><small>Mín. {quantity.format(p.minimumStock)}</small></div>
    </li>)}</ul> : <p className="analysis-empty">Nenhum produto ativo com estoque baixo.</p>}
  </section>
}
function TopStockOut({ data }: { data: Dashboard }) {
  const max = data.topStockOut[0]?.quantity ?? 1
  return <section className="analysis-section" aria-label="Produtos com maior saída de estoque">
    <div className="analysis-section-title"><h2>Produtos com maior saída de estoque</h2><span>Até 10 produtos</span></div>
    <p className="analysis-caption">Quantidades registradas no período. Saída física não significa venda.</p>
    {data.topStockOut.length ? <ol className="analysis-ranking">{data.topStockOut.map(p => <li key={p.id}>
      <div><span><strong>{p.name}</strong><small>{p.sku}</small></span><b>{quantity.format(p.quantity)}</b></div>
      <div className="rank-track" aria-hidden="true"><span style={{ width: (p.quantity / max * 100) + '%' }} /></div>
    </li>)}</ol> : <p className="analysis-empty">Nenhuma saída de estoque no período.</p>}
  </section>
}
function Insights({ data }: { data: Dashboard }) {
  return <section className="analysis-section" aria-label="Insights">
    <div className="analysis-section-title"><h2>Insights</h2><span>Regras explicáveis</span></div>
    {data.insights.length ? <ul className="analysis-insights">{data.insights.map(i => <li key={i.code} className={i.level}>
      {i.level === 'attention' ? <CircleAlert size={19} aria-hidden="true" /> : <Info size={19} aria-hidden="true" />}
      <div><p>{i.message}</p><details><summary>Critério</summary><p>{i.evidence}</p></details></div>
    </li>)}</ul> : <p className="analysis-empty">Nenhum insight acionado pelas regras atuais.</p>}
  </section>
}
function Overdue({ data }: { data: Dashboard }) {
  return <section className="analysis-section" aria-label="Contas vencidas"><div className="analysis-section-title"><h2>Contas vencidas</h2><span>{data.financial.overdueCount} lançamentos</span></div>
    <dl className="analysis-facts"><div><dt>A receber vencido</dt><dd>{money.format(data.financial.overdueReceivable)}</dd></div>
      <div><dt>A pagar vencido</dt><dd>{money.format(data.financial.overduePayable)}</dd></div></dl>
    <p className="analysis-caption">Pendentes com vencimento anterior a hoje, independentemente do período selecionado.</p>
  </section>
}
function InventoryMetrics({ data }: { data: Dashboard }) {
  return <><div className="analysis-secondary-metrics">
    <Metric label="Produtos ativos" value={quantity.format(data.inventory.activeProducts)} />
    <Metric label="Estoque baixo" value={quantity.format(data.inventory.lowStockProducts)} />
    <Metric label="Sem estoque" value={quantity.format(data.inventory.zeroStockProducts)} />
    <Metric label="Valor estimado a custo" value={money.format(data.inventory.estimatedCostValue)} note="Saldo × custo atual dos produtos ativos" />
  </div><div className="analysis-main-metrics">
    <Metric label="Entradas no período" value={quantity.format(data.movements.entries)}><ArrowDownLeft size={18} /></Metric>
    <Metric label="Saídas no período" value={quantity.format(data.movements.exits)}><ArrowUpRight size={18} /></Metric>
    <Metric label="Movimentações" value={quantity.format(data.movements.count)} note="Registros no período" />
  </div><p className="analysis-caption">Estimativa operacional, não valor contábil. Quantidades de produtos diferentes não indicam valor econômico.</p></>
}
function StaleProducts({ data }: { data: Dashboard }) {
  return <section className="analysis-section" aria-label="Produtos sem movimentação">
    <div className="analysis-section-title"><h2>Sem movimentação nos últimos 30 dias</h2><span>{data.inventory.staleProducts} produtos</span></div>
    <p className="analysis-caption">Produtos ativos cadastrados há pelo menos 30 dias. Até 10 resultados; situação atual.</p>
    {data.staleProducts.length ? <ul className="analysis-products stale-list">{data.staleProducts.map(p => <li key={p.id}><div><strong>{p.name}</strong><small>{p.sku} · Saldo {quantity.format(p.currentStock)}</small></div>
      <span>{p.lastMovementAt ? new Date(p.lastMovementAt).toLocaleDateString('pt-BR', { timeZone: 'Etc/GMT+3' }) : 'Sem registro anterior'}</span></li>)}</ul>
      : <p className="analysis-empty">Nenhum produto atende ao critério de 30 dias sem movimentação.</p>}
  </section>
}

export function AnalyticsPage({ detailed = false }: { detailed?: boolean }) {
  const { user } = useAuth()
  const [params, setParams] = useSearchParams()
  const period = params.get('period') ?? 'last30'
  const view = params.get('view') ?? 'finance'
  const query = useQuery({ queryKey: ['analytics', user!.company.id, period], queryFn: ({ signal }) => getDashboard(period, signal), retry: false })
  const data = query.data
  return <div className="analytics-page">
    <PageHeader title={detailed ? 'Analytics' : 'Visão geral'} description={detailed ? 'Financeiro, estoque e produtos em perspectiva.' : 'Um retrato da operação da sua empresa.'}>
      <div className="analysis-period"><label>Período<select aria-label="Período" value={period} onChange={e => { const next = new URLSearchParams(params); next.set('period', e.target.value); setParams(next) }}>
        {!periods.some(p => p[0] === period) && <option value={period}>Período inválido</option>}
        {periods.map(([value, label]) => <option key={value} value={value}>{label}</option>)}</select></label>
        <button className="icon-button" title="Atualizar indicadores" aria-label="Atualizar indicadores" disabled={query.isFetching} onClick={() => void query.refetch()}><RefreshCw size={18} /></button></div>
    </PageHeader>
    {detailed && <nav className="analysis-tabs" aria-label="Análises">{[['finance', 'Financeiro'], ['inventory', 'Estoque'], ['products', 'Produtos']].map(([slug, title]) =>
      <Link key={slug} to={'?period=' + encodeURIComponent(period) + '&view=' + slug} aria-current={view === slug ? 'page' : undefined}>{title}</Link>)}</nav>}
    {query.isPending ? <div role="status" className="loading"><LoaderCircle className="loading-icon" />Carregando indicadores…</div>
      : query.isError ? <div><p role="alert" className="message error">{analysisError(query.error)}</p><button className="secondary" onClick={() => void query.refetch()}>Tentar novamente</button></div>
      : data && <>
        <div className="analysis-period-info"><span>{date(data.period.from)} a {date(data.period.to)} · {data.period.timeZone}</span>
          <span>Comparação: {date(data.period.previousFrom)} a {date(data.period.previousTo)}</span></div>
        {data.period.key !== 'previousMonth' && <p className="analysis-caption">Inclui o dia de hoje, ainda em andamento.</p>}
        {(!detailed || view === 'finance' || !['inventory', 'products'].includes(view)) && <>
          <FinancialMetrics data={data} />
          <div className="analysis-columns"><FinancialSeries data={data} />{detailed ? <Overdue data={data} /> : <LowStock data={data} />}</div>
          {detailed && <section className="analysis-section" aria-label="Comparação financeira"><div className="analysis-section-title"><h2>Comparação financeira</h2><span>Intervalos de igual duração</span></div>
            <dl className="analysis-facts"><div><dt>Recebido anteriormente</dt><dd>{money.format(data.comparisons.received.previous)}</dd></div><div><dt>Pago anteriormente</dt><dd>{money.format(data.comparisons.paid.previous)}</dd></div></dl></section>}
        </>}
        {!detailed && <><div className="analysis-columns"><TopStockOut data={data} /><Insights data={data} /></div>
          <div className="analysis-bottom"><span><Wallet size={16} />Estoque estimado a custo: <strong>{money.format(data.inventory.estimatedCostValue)}</strong></span>
            <Link to={'/app/analytics?period=' + period + '&view=inventory'}>Detalhar estoque <ArrowRight size={16} /></Link></div></>}
        {detailed && view === 'inventory' && <><InventoryMetrics data={data} /><div className="analysis-columns"><LowStock data={data} /><TopStockOut data={data} /></div></>}
        {detailed && view === 'products' && <><div className="analysis-main-metrics"><Metric label="Produtos ativos" value={quantity.format(data.inventory.activeProducts)} /><Metric label="Sem estoque" value={quantity.format(data.inventory.zeroStockProducts)} />
          <Metric label="Sem movimentação há 30 dias" value={quantity.format(data.inventory.staleProducts)} /></div><StaleProducts data={data} /><TopStockOut data={data} /></>}
        {detailed && <Insights data={data} />}
        <p className="analysis-updated">Atualizado em {new Date(data.asOf).toLocaleString('pt-BR', { timeZone: 'Etc/GMT+3' })} · UTC−03:00</p>
      </>}
  </div>
}

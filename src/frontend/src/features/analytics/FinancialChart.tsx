import { ResponsiveContainer, BarChart, Bar, XAxis, YAxis, CartesianGrid, Tooltip, Legend } from 'recharts'
import { date, money } from './api'
import type { FinancialPoint } from './api'

export default function FinancialChart({ points, monthly }: { points: FinancialPoint[]; monthly: boolean }) {
  const label = (value: string) => monthly ? value.slice(5, 7) + '/' + value.slice(0, 4) : value.slice(8, 10) + '/' + value.slice(5, 7)
  return <div className="financial-chart" role="img" aria-label="Gráfico de receitas recebidas e despesas pagas">
    <ResponsiveContainer width="100%" height="100%" minWidth={0}>
      <BarChart data={points} margin={{ top: 12, right: 8, left: 0, bottom: 8 }} accessibilityLayer>
        <CartesianGrid stroke="var(--color-chart-grid)" vertical={false} />
        <XAxis dataKey="date" tickFormatter={label} tick={{ fontSize: 11, fill: 'var(--color-muted)' }} minTickGap={28} axisLine={false} tickLine={false} />
        <YAxis width={58} tickFormatter={value => new Intl.NumberFormat('pt-BR', { notation: 'compact', maximumFractionDigits: 1 }).format(Number(value))} tick={{ fontSize: 11, fill: 'var(--color-muted)' }} axisLine={false} tickLine={false} />
        <Tooltip formatter={value => money.format(Number(value))} labelFormatter={value => monthly ? label(String(value)) : date(String(value))}
          contentStyle={{ borderRadius: 6, borderColor: 'var(--color-border)', background: 'var(--color-surface-raised)', color: 'var(--color-text)', fontSize: 12 }}
          cursor={{ fill: 'var(--color-chart-cursor)' }} />
        <Legend wrapperStyle={{ color: 'var(--color-muted)', fontSize: 12 }} />
        <Bar dataKey="received" name="Recebido" fill="var(--color-chart-income)" radius={[3, 3, 0, 0]} maxBarSize={28} isAnimationActive={false} />
        <Bar dataKey="paid" name="Pago" fill="var(--color-chart-expense)" radius={[3, 3, 0, 0]} maxBarSize={28} isAnimationActive={false} />
      </BarChart>
    </ResponsiveContainer>
  </div>
}

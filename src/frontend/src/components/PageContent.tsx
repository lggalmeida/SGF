import type { LucideIcon } from 'lucide-react'
import type { ReactNode } from 'react'

export function PageHeader({ title, description, children }: { title: string; description: string; children?: ReactNode }) {
  return <div className="page-heading"><div><h1>{title}</h1><p>{description}</p></div>{children}</div>
}

export function EmptyState({ icon: Icon, title, description }: { icon: LucideIcon; title: string; description: string }) {
  return <div className="empty-state"><span className="empty-icon"><Icon size={26} aria-hidden="true" /></span><h2>{title}</h2><p>{description}</p></div>
}

export function StatCard({ label, icon: Icon, tone }: { label: string; icon: LucideIcon; tone: string }) {
  // Phase H placeholders: no amounts, variations or inventory counts are fabricated.
  return <article className="stat-card"><div className="stat-label"><h2>{label}</h2><span className={`stat-icon ${tone}`}><Icon size={19} aria-hidden="true" /></span></div><p className="stat-value" aria-label="Indicador ainda indisponível">—</p><p className="stat-note">Sem dados disponíveis</p></article>
}

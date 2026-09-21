import { useLocation } from 'react-router-dom'
import { appNavigation } from '../../app/navigation'
import { PageHeader, EmptyState } from '../../components/PageContent'

export function ModulePage() {
  const { pathname } = useLocation()
  const page = appNavigation.find(item => pathname === `/app/${item.slug}`)
  if (!page) return null
  return <>
    <PageHeader title={page.title} description={page.description} />
    <section className="module-placeholder"><span className="preview-label">Em desenvolvimento</span><EmptyState icon={page.icon} title="Um novo espaço para sua empresa" description="Este módulo será implementado nas próximas fases." /></section>
  </>
}

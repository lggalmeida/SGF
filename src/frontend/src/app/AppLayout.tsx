import { useEffect, useRef } from 'react'
import { Outlet, useLocation } from 'react-router-dom'
import { Building2, Menu } from 'lucide-react'
import { useAuth } from '../features/auth/AuthProvider'
import { Sidebar } from './Sidebar'
import { UserMenu } from './UserMenu'
import { appNavigation } from './navigation'
import './layout.css'

export function AppLayout() {
  const { user } = useAuth()
  const { pathname } = useLocation()
  const drawer = useRef<HTMLDialogElement>(null)
  const trigger = useRef<HTMLButtonElement>(null)
  const page = appNavigation.find(item => pathname === `/app/${item.slug}`) ?? appNavigation[0]
  useEffect(() => {
    drawer.current?.close()
    document.title = `${page.title} | SGF`
    window.scrollTo(0, 0)
  }, [pathname, page.title])
  useEffect(() => {
    const query = window.matchMedia('(min-width: 900px)')
    const closeOnDesktop = () => { if (query.matches) drawer.current?.close() }
    query.addEventListener('change', closeOnDesktop)
    return () => query.removeEventListener('change', closeOnDesktop)
  }, [])
  if (!user) return <div className="loading" role="status">Carregando sua conta…</div>
  return <div className="app-layout">
    <a className="skip-link" href="#app-content">Ir para o conteúdo</a>
    <aside className="desktop-sidebar"><Sidebar /></aside>
    <dialog className="mobile-drawer" ref={drawer} aria-label="Navegação do SGF" onClose={() => trigger.current?.focus()} onClick={event => { if (event.target === event.currentTarget) drawer.current?.close() }}>
      <Sidebar onClose={() => drawer.current?.close()} onNavigate={() => drawer.current?.close()} />
    </dialog>
    <div className="app-workspace">
      <header className="app-header">
        <div className="header-title"><button ref={trigger} className="icon-button mobile-trigger" aria-label="Abrir navegação" title="Abrir navegação" onClick={() => drawer.current?.showModal()}><Menu size={22} /></button><span>{page.title}</span></div>
        <div className="header-account"><div className="current-company" role="group" aria-label="Empresa atual"><Building2 size={17} aria-hidden="true" /><span title={user.company.name}>{user.company.name}</span></div><UserMenu /></div>
      </header>
      <main className="app-content" id="app-content" tabIndex={-1}><Outlet /></main>
      <footer className="app-footer"><span>SGF · Gestão Facilitada</span><span>Seu negócio, mais organizado.</span></footer>
    </div>
  </div>
}

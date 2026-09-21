import { NavLink, useLocation } from 'react-router-dom'
import { PanelLeftClose, Layers } from 'lucide-react'
import { appNavigation } from './navigation'

export function Sidebar({ onNavigate, onClose }: { onNavigate?: () => void; onClose?: () => void }) {
  const { pathname } = useLocation()
  return <div className="sidebar-inner">
    <div className="sidebar-brand"><img src="/sgf-mark.png" alt="" width="34" height="34" /><span>SGF<small>Gestão Facilitada</small></span>{onClose && <button className="icon-button drawer-close" onClick={onClose} aria-label="Fechar navegação" title="Fechar navegação"><PanelLeftClose size={20} /></button>}</div>
    <p className="nav-heading">ESPAÇO DE TRABALHO</p>
    <nav aria-label="Navegação principal">
      {appNavigation.map(({ slug, title, icon: Icon }) => <NavLink key={slug} end to={slug === 'dashboard' && pathname === '/app' ? '/app' : `/app/${slug}`} onClick={onNavigate} className={({ isActive }) => `nav-item ${slug === 'settings' ? 'nav-settings' : ''} ${isActive ? 'active' : ''}`}><Icon size={19} aria-hidden="true" /><span>{title}</span></NavLink>)}
    </nav>
    <div className="sidebar-footer"><Layers size={17} aria-hidden="true" /><div>Seu espaço de gestão<small>SGF · Versão inicial</small></div></div>
  </div>
}

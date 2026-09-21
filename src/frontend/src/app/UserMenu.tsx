import { useEffect, useRef, useState } from 'react'
import { ChevronDown, LogOut, UserRound } from 'lucide-react'
import { Link } from 'react-router-dom'
import { useAuth } from '../features/auth/AuthProvider'

export function UserMenu() {
  const { user, signOut } = useAuth()
  const menu = useRef<HTMLDetailsElement>(null)
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    const outside = (event: PointerEvent) => { if (menu.current && !menu.current.contains(event.target as Node)) menu.current.open = false }
    document.addEventListener('pointerdown', outside)
    return () => document.removeEventListener('pointerdown', outside)
  }, [])
  if (!user) return null
  const initials = user.name.trim().split(/\s+/).filter(Boolean).map(part => part[0]).filter((_, i, parts) => i === 0 || i === parts.length - 1).join('').toUpperCase()
  return <details className="user-menu" ref={menu} onKeyDown={event => {
    if (event.key === 'Escape' && menu.current) { menu.current.open = false; menu.current.querySelector('summary')?.focus() }
  }}>
    <summary aria-label="Menu do usuário" title="Menu do usuário"><span className="avatar" aria-hidden="true">{initials}</span><span className="user-name">{user.name}</span><ChevronDown size={15} aria-hidden="true" /></summary>
    <div className="user-popover"><div className="user-info"><strong>{user.name}</strong><span>{user.email}</span><span className="role-label">{user.role}</span></div>
      <Link to="/app/settings" onClick={() => { if (menu.current) menu.current.open = false }}><UserRound size={17} aria-hidden="true" />Meu perfil</Link>
      <button disabled={busy} onClick={async () => { setBusy(true); await signOut() }}><LogOut size={17} aria-hidden="true" />{busy ? 'Saindo…' : 'Sair'}</button>
    </div>
  </details>
}

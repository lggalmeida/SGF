import { useState } from 'react'
import { Building2, LogOut, Moon, ShieldCheck, Sun, UserRound } from 'lucide-react'
import { useAuth } from '../auth/AuthProvider'
import { PageHeader } from '../../components/PageContent'
import { applyTheme, getTheme, type Theme } from '../../app/theme'
import './settings.css'

const roleNames = { Owner: 'Proprietário', Admin: 'Administrador', Member: 'Membro' } as const

export function SettingsPage() {
  const { user, signOut } = useAuth()
  const [theme, setTheme] = useState<Theme>(getTheme)
  const [leaving, setLeaving] = useState(false)
  if (!user) return null

  function chooseTheme(next: Theme) {
    applyTheme(next)
    setTheme(next)
  }

  async function leave() {
    setLeaving(true)
    await signOut()
  }

  return <div className="settings-page">
    <PageHeader title="Configurações" description="Gerencie suas preferências e informações da conta." />
    <div className="settings-grid">
      <section className="settings-section" aria-labelledby="account-settings-title">
        <header><span className="settings-icon"><UserRound size={19} aria-hidden="true" /></span><div><h2 id="account-settings-title">Minha conta</h2><p>Dados da identidade autenticada.</p></div></header>
        <dl className="settings-details">
          <div><dt>Nome</dt><dd>{user.name}</dd></div>
          <div><dt>E-mail</dt><dd>{user.email}</dd></div>
          <div><dt>Perfil atual</dt><dd><span className="settings-role">{roleNames[user.role]}</span></dd></div>
        </dl>
      </section>

      <section className="settings-section" aria-labelledby="company-settings-title">
        <header><span className="settings-icon"><Building2 size={19} aria-hidden="true" /></span><div><h2 id="company-settings-title">Empresa</h2><p>Espaço de trabalho desta sessão.</p></div></header>
        <dl className="settings-details"><div><dt>Empresa atual</dt><dd>{user.company.name}</dd></div></dl>
        <p className="settings-note">A edição cadastral e a troca de empresa não fazem parte desta versão.</p>
      </section>

      <section className="settings-section" aria-labelledby="appearance-settings-title">
        <header><span className="settings-icon"><Sun size={19} aria-hidden="true" /></span><div><h2 id="appearance-settings-title">Aparência</h2><p>Escolha como o SGF aparece neste navegador.</p></div></header>
        <div className="theme-options" role="radiogroup" aria-label="Tema da aplicação">
          <button type="button" role="radio" aria-checked={theme === 'light'} onClick={() => chooseTheme('light')}><Sun size={19} aria-hidden="true" /><span><strong>Claro</strong><small>Superfícies claras e neutras</small></span></button>
          <button type="button" role="radio" aria-checked={theme === 'dark'} onClick={() => chooseTheme('dark')}><Moon size={19} aria-hidden="true" /><span><strong>Escuro</strong><small>Contraste confortável à noite</small></span></button>
        </div>
      </section>

      <section className="settings-section session-settings" aria-labelledby="session-settings-title">
        <header><span className="settings-icon"><ShieldCheck size={19} aria-hidden="true" /></span><div><h2 id="session-settings-title">Sessão</h2><p>Sua sessão atual está protegida por autenticação segura.</p></div></header>
        <p className="settings-note">Ao sair, o token de renovação é revogado e os dados autenticados são removidos deste navegador.</p>
        <button className="logout-button" type="button" disabled={leaving} onClick={leave}><LogOut size={18} aria-hidden="true" />{leaving ? 'Saindo…' : 'Sair da conta'}</button>
      </section>
    </div>
  </div>
}

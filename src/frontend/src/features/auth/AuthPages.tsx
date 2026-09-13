import { useState } from 'react'
import type { FormEvent } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ApiError } from '../../lib/api'
import { useAuth } from './AuthProvider'
import { register } from './session'

function friendlyError(error: unknown, registration: boolean) {
  if (error instanceof ApiError) {
    if (error.status === 401) return 'E-mail ou senha inválidos.'
    if (error.code === 'company_selection_required') return 'Sua conta possui mais de uma empresa. A seleção de empresa ainda não está disponível.'
    if (error.status === 403) return 'Sua conta não possui acesso ativo a uma empresa. Entre em contato com o responsável.'
    if (registration && error.status === 409) return 'Este e-mail já está cadastrado. Entre com sua conta.'
    if (registration && error.status === 400) return 'Confira os dados: nome e empresa são obrigatórios (até 200 caracteres), e-mail válido e senha com ao menos 8 caracteres, maiúscula, minúscula, número e símbolo.'
  }
  return 'Não foi possível concluir agora. Confira sua conexão e tente novamente.'
}

export function AuthForm({ mode }: { mode: 'login' | 'register' }) {
  const registration = mode === 'register'
  const { signIn, notice } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [visible, setVisible] = useState(false)

  async function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (busy) return
    const form = event.currentTarget
    const data = new FormData(form)
    const email = String(data.get('email')).trim()
    const password = String(data.get('password'))
    setBusy(true)
    setError('')
    try {
      if (registration) {
        await register({ name: String(data.get('name')).trim(), email, password, companyName: String(data.get('companyName')).trim() })
        navigate('/login', { replace: true, state: { registered: true } })
      } else {
        await signIn(email, password)
        navigate('/app', { replace: true })
      }
    } catch (failure) { setError(friendlyError(failure, registration)) }
    finally {
      const passwordField = form.elements.namedItem('password') as HTMLInputElement
      passwordField.value = ''
      setBusy(false)
    }
  }

  return (
    <section className="auth-panel" aria-labelledby="page-title">
      <div className="section-heading">
        <p className="eyebrow">{registration ? 'SUA PRIMEIRA EMPRESA' : 'SUA CONTA'}</p>
        <h1 id="page-title">{registration ? 'Crie sua conta no SGF' : 'Entre no SGF'}</h1>
      </div>
      {!registration && location.state?.registered && <p className="message success" role="status">Conta criada com sucesso. Faça login para continuar.</p>}
      {!registration && notice && <p className="message warning" role="status">{notice}</p>}
      {error && <p className="message error" role="alert">{error}</p>}
      <form onSubmit={submit} aria-busy={busy}>
        <fieldset disabled={busy}>
          {registration && <label>Nome completo<input name="name" autoComplete="name" required maxLength={200} /></label>}
          <label>E-mail<input name="email" type="email" autoComplete="username" required maxLength={256} /></label>
          {registration && <label>Nome da empresa<input name="companyName" autoComplete="organization" required maxLength={200} /></label>}
          <label>Senha<input name="password" type={visible ? 'text' : 'password'} autoComplete={registration ? 'new-password' : 'current-password'} required minLength={registration ? 8 : undefined} aria-describedby={registration ? 'password-requirements' : undefined} /></label>
          {registration && <p className="field-hint" id="password-requirements">No mínimo 8 caracteres, com maiúscula, minúscula, número e símbolo.</p>}
          <label className="checkbox-label"><input type="checkbox" checked={visible} onChange={event => setVisible(event.target.checked)} />Mostrar senha</label>
          <button className="primary" type="submit">{busy ? (registration ? 'Criando conta…' : 'Entrando…') : (registration ? 'Criar conta' : 'Entrar')}</button>
        </fieldset>
      </form>
      <p className="alternate">{registration ? 'Já tem uma conta? ' : 'Ainda não tem conta? '}<Link aria-disabled={busy} onClick={event => { if (busy) event.preventDefault() }} to={registration ? '/login' : '/register'}>{registration ? 'Entrar' : 'Criar conta'}</Link></p>
    </section>
  )
}

export function SessionPage() {
  const { user, signOut } = useAuth()
  const [busy, setBusy] = useState(false)
  if (!user) return <div className="loading" role="status">Carregando sua conta…</div>
  return (
    <section className="session-panel" aria-labelledby="page-title">
      <p className="eyebrow">BEM-VINDO AO SGF</p>
      <h1 id="page-title">Olá, {user.name}</h1>
      <p className="account-email">{user.email}</p>
      <dl><div><dt>Empresa atual</dt><dd>{user.company.name}</dd></div><div><dt>Perfil</dt><dd>{user.role}</dd></div></dl>
      <button className="secondary" disabled={busy} onClick={async () => { setBusy(true); await signOut() }}>{busy ? 'Saindo…' : 'Sair'}</button>
    </section>
  )
}

import { createContext, useContext, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { useQueryClient } from '@tanstack/react-query'
import * as session from './session'

type SessionState = 'restoring' | 'anonymous' | 'authenticated'
type AuthValue = {
  status: SessionState
  user: session.CurrentUser | undefined
  notice: string
  signIn: (email: string, password: string) => Promise<void>
  signOut: () => Promise<void>
}
const AuthContext = createContext<AuthValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const client = useQueryClient()
  const [status, setStatus] = useState<SessionState>('restoring')
  const [user, setUser] = useState<session.CurrentUser>()
  const [notice, setNotice] = useState('')
  const initialTask = useRef<Promise<session.CurrentUser> | null>(null)
  const meOptions = {
    queryKey: ['auth', 'me'],
    queryFn: () => session.authenticatedRequest<session.CurrentUser>('/api/auth/me'),
    retry: false as const,
    staleTime: Infinity,
  }

  useEffect(() => {
    let active = true
    const unsubscribe = session.onSessionLost(() => {
      void client.cancelQueries()
      client.clear()
      setUser(undefined)
      setStatus('anonymous')
    })
    // Share startup work across StrictMode's effect replay.
    initialTask.current ??= session.restoreSession().then(() => client.fetchQuery(meOptions))
    initialTask.current.then(currentUser => {
      if (active) { setUser(currentUser); setStatus('authenticated') }
    }).catch(() => {
      if (active) { session.clearSession(); setStatus('anonymous') }
    })
    return () => { active = false; unsubscribe() }
    // Startup only: query options are deliberately constant for this provider.
  }, [client])

  async function signIn(email: string, password: string) {
    setNotice('')
    try {
      await session.login(email, password)
      const currentUser = await client.fetchQuery(meOptions)
      setUser(currentUser)
      setStatus('authenticated')
    } catch (error) {
      session.clearSession()
      throw error
    }
  }
  async function signOut() {
    setNotice('')
    const task = session.logout()
    setStatus('restoring')
    try { await task }
    catch {
      setNotice('Você saiu deste navegador. Não foi possível confirmar a saída no servidor. Tente novamente quando a conexão voltar.')
    }
    finally { setStatus('anonymous') }
  }
  return <AuthContext.Provider value={{ status, user, notice, signIn, signOut }}>{children}</AuthContext.Provider>
}

export function useAuth() {
  const value = useContext(AuthContext)
  if (!value) throw new Error('AuthProvider is required')
  return value
}

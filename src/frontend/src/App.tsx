import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './features/auth/AuthProvider'
import { AuthForm, SessionPage } from './features/auth/AuthPages'
import './App.css'

function Gate({ authenticated }: { authenticated: boolean }) {
  const { status } = useAuth()
  if (status === 'restoring') return <div className="loading" role="status">Carregando sua sessão…</div>
  if (authenticated && status !== 'authenticated') return <Navigate to="/login" replace />
  if (!authenticated && status === 'authenticated') return <Navigate to="/app" replace />
  return <Outlet />
}

export default function App() {
  return (
    <BrowserRouter>
      <AuthProvider>
        <div className="site-shell">
          <header className="brand-bar"><img src="/sgf-mark.png" width="36" height="36" alt="" /><span>SGF</span><span className="brand-description">Sistema de Gestão Facilitada</span></header>
          <main>
            <Routes>
              <Route element={<Gate authenticated={false} />}>
                <Route path="/login" element={<AuthForm key="login" mode="login" />} />
                <Route path="/register" element={<AuthForm key="register" mode="register" />} />
              </Route>
              <Route element={<Gate authenticated />}>
                <Route path="/app" element={<SessionPage />} />
              </Route>
              <Route path="*" element={<Navigate to="/app" replace />} />
            </Routes>
          </main>
          <footer>SGF · Sistema de Gestão Facilitada</footer>
        </div>
      </AuthProvider>
    </BrowserRouter>
  )
}

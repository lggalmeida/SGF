import { BrowserRouter, Navigate, Outlet, Route, Routes } from 'react-router-dom'
import { LoaderCircle } from 'lucide-react'
import { AuthProvider, useAuth } from './features/auth/AuthProvider'
import { AuthForm } from './features/auth/AuthPages'
import { AppLayout } from './app/AppLayout'
import { AnalyticsPage } from './features/analytics/AnalyticsPage'
import { ProductsPage } from './features/products/ProductsPage'
import { InventoryPage } from './features/inventory/InventoryPage'
import { FinancePage } from './features/finance/FinancePage'
import { SettingsPage } from './features/settings/SettingsPage'
import './App.css'

function Gate({ authenticated }: { authenticated: boolean }) {
  const { status } = useAuth()
  if (status === 'restoring') return <div className="loading" role="status"><LoaderCircle className="loading-icon" size={22} aria-hidden="true" />Carregando sua sessão…</div>
  if (authenticated && status !== 'authenticated') return <Navigate to="/login" replace />
  if (!authenticated && status === 'authenticated') return <Navigate to="/app" replace />
  return <Outlet />
}

function PublicLayout() {
  return <div className="site-shell">
    <header className="brand-bar"><img src="/sgf-mark.png" width="36" height="36" alt="" /><span>SGF</span><span className="brand-description">Sistema de Gestão Facilitada</span></header>
    <main><Outlet /></main>
    <footer>SGF · Sistema de Gestão Facilitada</footer>
  </div>
}

export default function App() {
  return <BrowserRouter><AuthProvider><Routes>
    <Route element={<PublicLayout />}>
      <Route element={<Gate authenticated={false} />}>
        <Route path="/login" element={<AuthForm key="login" mode="login" />} />
        <Route path="/register" element={<AuthForm key="register" mode="register" />} />
      </Route>
    </Route>
    <Route element={<Gate authenticated />}>
      <Route path="/app" element={<AppLayout />}>
        <Route index element={<AnalyticsPage />} />
        <Route path="dashboard" element={<AnalyticsPage />} />
        <Route path="products" element={<ProductsPage />} />
        <Route path="inventory" element={<InventoryPage />} />
        <Route path="finance" element={<FinancePage />} />
        <Route path="analytics" element={<AnalyticsPage detailed />} />
        <Route path="settings" element={<SettingsPage />} />
      </Route>
    </Route>
    <Route path="*" element={<Navigate to="/app" replace />} />
  </Routes></AuthProvider></BrowserRouter>
}

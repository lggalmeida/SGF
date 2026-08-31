import { useQuery } from '@tanstack/react-query'
import './App.css'
import { getApiHealth } from './lib/api'

function App() {
  const healthQuery = useQuery({
    queryKey: ['api-health'],
    queryFn: getApiHealth,
    retry: false,
  })

  return (
    <main className="app-shell">
      <section className="status-panel" aria-labelledby="app-title">
        <p className="eyebrow">Fundacao tecnica</p>
        <h1 id="app-title">SGF</h1>
        <p className="subtitle">Sistema de Gestao Facilitada</p>

        <div className="status-card">
          <span className="status-label">Backend</span>
          <strong>
            {healthQuery.isLoading && 'Verificando...'}
            {healthQuery.isError && 'Indisponivel'}
            {healthQuery.data && healthQuery.data.status}
          </strong>
          <small>
            {healthQuery.data
              ? `${healthQuery.data.service} respondeu com sucesso.`
              : 'Aguardando resposta de /api/health.'}
          </small>
        </div>
      </section>
    </main>
  )
}

export default App

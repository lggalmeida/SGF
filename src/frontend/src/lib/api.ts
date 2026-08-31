export type ApiHealthResponse = {
  status: string
  service: string
  timestamp: string
}

const apiBaseUrl = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5206'

export async function getApiHealth(): Promise<ApiHealthResponse> {
  const response = await fetch(`${apiBaseUrl}/api/health`)

  if (!response.ok) {
    throw new Error('API health check failed.')
  }

  return response.json()
}

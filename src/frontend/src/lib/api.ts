const baseUrl = (import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5206').replace(/\/$/, '')

export class ApiError extends Error {
  status: number
  code?: string
  constructor(status: number, code?: string) {
    super('Request failed')
    this.status = status
    this.code = code
  }
}

export async function request<T>(path: string, init: RequestInit = {}): Promise<T> {
  if (!path.startsWith('/api/')) throw new Error('Invalid API path')
  const response = await fetch(baseUrl + path, {
    ...init,
    credentials: init.credentials ?? 'omit',
    signal: init.signal ?? AbortSignal.timeout(15000),
    headers: { ...(init.body ? { 'Content-Type': 'application/json' } : {}), ...init.headers },
  })
  if (!response.ok) {
    const problem = await response.json().catch(() => ({}))
    throw new ApiError(response.status, problem.code)
  }
  return response.status === 204 ? undefined as T : response.json()
}

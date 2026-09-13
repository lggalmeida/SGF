import { ApiError, request } from '../../lib/api'

export type CurrentUser = {
  userId: string
  name: string
  email: string
  company: { id: string; name: string }
  role: 'Owner' | 'Admin' | 'Member'
}
type TokenResponse = { accessToken: string }

let accessToken: string | null = null
let generation = 0
let refreshPromise: Promise<void> | null = null
const listeners = new Set<() => void>()
const logoutMarker = 'sgf.logoutPending'

export function onSessionLost(listener: () => void) {
  listeners.add(listener)
  return () => { listeners.delete(listener) }
}

export function clearSession() {
  accessToken = null
  generation++
  listeners.forEach(listener => listener())
}

function logoutPending() {
  try { return sessionStorage.getItem(logoutMarker) === 'true' } catch { return false }
}

function markLogout(pending: boolean) {
  // Only a non-sensitive logout flag is persisted, never credentials or user data.
  try {
    if (pending) sessionStorage.setItem(logoutMarker, 'true')
    else sessionStorage.removeItem(logoutMarker)
  } catch { /* Session still clears in memory when storage is unavailable. */ }
}

export function refreshSession(): Promise<void> {
  if (refreshPromise) return refreshPromise
  const started = generation
  refreshPromise = request<TokenResponse>('/api/auth/refresh', { method: 'POST', credentials: 'include' })
    .then(result => {
      if (generation !== started) throw new ApiError(401)
      accessToken = result.accessToken
    })
    .catch(error => {
      if (generation === started) clearSession()
      throw error
    })
    .finally(() => { refreshPromise = null })
  return refreshPromise
}

export async function authenticatedRequest<T>(path: string, init: RequestInit = {}): Promise<T> {
  const started = generation
  const usedToken = accessToken
  if (!usedToken) throw new ApiError(401)
  const send = () => request<T>(path, {
    ...init, credentials: 'omit',
    headers: { ...init.headers, Authorization: `Bearer ${accessToken}` },
  })
  try {
    let result: T
    try { result = await send() }
    catch (error) {
      if (!(error instanceof ApiError) || error.status !== 401 || generation !== started) throw error
      // A late 401 may belong to the old token while another request already renewed it.
      if (accessToken === usedToken) await refreshSession()
      if (generation !== started || !accessToken) throw new ApiError(401)
      result = await send()
    }
    if (generation !== started) throw new ApiError(401)
    return result
  } catch (error) {
    if (generation === started && error instanceof ApiError && [401, 403].includes(error.status)) clearSession()
    throw error
  }
}

export async function login(email: string, password: string) {
  await refreshPromise?.catch(() => {})
  clearSession()
  const started = generation
  const result = await request<TokenResponse>('/api/auth/login', {
    method: 'POST', credentials: 'include', body: JSON.stringify({ email, password }),
  })
  if (started !== generation) throw new ApiError(401)
  accessToken = result.accessToken
  markLogout(false)
}

export async function logout() {
  markLogout(true)
  clearSession()
  // Wait for an in-flight rotation so logout revokes the replacement cookie too.
  await refreshPromise?.catch(() => {})
  await request<void>('/api/auth/logout', { method: 'POST', credentials: 'include' })
  markLogout(false)
}

export async function restoreSession() {
  if (logoutPending()) {
    await logout().catch(() => {})
    throw new ApiError(401)
  }
  await refreshSession()
}

export function register(data: { name: string; email: string; password: string; companyName: string }) {
  return request('/api/auth/register', { method: 'POST', body: JSON.stringify(data) })
}

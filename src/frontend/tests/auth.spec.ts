import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

const api = process.env.SGF_TEST_API_URL ?? 'http://localhost:5206'
const password = 'TesteFrontend123!'
const account = () => ({ name: 'João Teste', email: `frontend-${crypto.randomUUID()}@example.com`, password, companyName: 'Empresa de Teste' })

async function createAndLogin(page: Page) {
  const user = account()
  expect((await page.request.post(api + '/api/auth/register', { data: user })).status()).toBe(201)
  await page.goto('/login')
  await page.getByLabel('E-mail', { exact: true }).fill(user.email)
  await page.getByLabel('Senha', { exact: true }).fill(user.password)
  await page.getByRole('button', { name: 'Entrar', exact: true }).click()
  await expect(page).toHaveURL(/\/app$/)
  await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
  return user
}

test('cadastro, login, F5, rotas publicas e logout', async ({ page }) => {
  const user = account()
  await page.goto('/app')
  await expect(page).toHaveURL(/\/login$/)
  await page.screenshot({ path: 'test-results/login-desktop.png', fullPage: true })
  await page.getByRole('link', { name: 'Criar conta' }).click()
  await page.getByLabel('Nome completo').fill(user.name)
  await page.getByLabel('E-mail', { exact: true }).fill(user.email)
  await page.getByLabel('Nome da empresa').fill(user.companyName)
  await page.getByLabel('Senha', { exact: true }).fill(user.password)
  await page.screenshot({ path: 'test-results/register-desktop.png', fullPage: true })
  await page.getByRole('button', { name: 'Criar conta' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('status')).toHaveText('Conta criada com sucesso. Faça login para continuar.')
  await page.getByLabel('E-mail', { exact: true }).fill(user.email)
  await page.getByLabel('Senha', { exact: true }).fill(user.password)
  await page.getByRole('button', { name: 'Entrar', exact: true }).click()
  await expect(page).toHaveURL(/\/app$/)
  await expect(page.getByText(user.companyName, { exact: true })).toBeVisible()
  await page.getByLabel('Menu do usuário').click()
  await expect(page.getByText('Owner', { exact: true })).toBeVisible()
  await page.getByLabel('Menu do usuário').click()
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
  await expect(page).toHaveURL(/\/app$/)
  for (const path of ['/login', '/register']) {
    await page.goto(path)
    await expect(page).toHaveURL(/\/app$/)
  }
  const cookie = (await page.context().cookies(api + '/api/auth/refresh')).find(c => c.name === 'sgf_refresh_token')
  expect(cookie?.httpOnly).toBe(true)
  expect(await page.evaluate(() => document.cookie)).not.toContain('sgf_refresh_token')
  expect(await page.evaluate(() => JSON.stringify({ ...localStorage, ...sessionStorage }))).toBe('{}')
  await page.screenshot({ path: 'test-results/app-desktop.png', fullPage: true })
  await page.getByLabel('Menu do usuário').click()
  await page.getByRole('button', { name: 'Sair', exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)
  await page.goto('/app')
  await expect(page).toHaveURL(/\/login$/)
})

test('login invalido tem mensagem amigavel e limpa senha', async ({ page }) => {
  await page.goto('/login')
  await page.getByLabel('E-mail', { exact: true }).fill('inexistente@example.com')
  await page.getByLabel('Senha', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Entrar', exact: true }).click()
  await expect(page.getByRole('alert')).toHaveText('E-mail ou senha inválidos.')
  await expect(page.getByLabel('Senha', { exact: true })).toHaveValue('')
})

test('cadastro trata senha fraca e email duplicado', async ({ page }) => {
  const user = account()
  expect((await page.request.post(api + '/api/auth/register', { data: user })).status()).toBe(201)
  await page.goto('/register')
  await page.getByLabel('Nome completo').fill(user.name)
  await page.getByLabel('E-mail', { exact: true }).fill(user.email)
  await page.getByLabel('Nome da empresa').fill(user.companyName)
  await page.getByLabel('Senha', { exact: true }).fill(password)
  await page.getByRole('button', { name: 'Criar conta' }).click()
  await expect(page.getByRole('alert')).toContainText('Este e-mail já está cadastrado')
  await page.getByLabel('E-mail', { exact: true }).fill(account().email)
  await page.getByLabel('Senha', { exact: true }).fill('abcdefgh')
  await page.getByRole('button', { name: 'Criar conta' }).click()
  await expect(page.getByRole('alert')).toContainText('maiúscula')
})

test('401 concorrentes compartilham refresh e repetem apenas uma vez', async ({ page }) => {
  await createAndLogin(page)
  let rejected = 0
  let refreshes = 0
  page.on('request', r => { if (r.url().endsWith('/api/auth/refresh')) refreshes++ })
  await page.route('**/api/auth/me', async route => {
    if (rejected++ < 2) await route.fulfill({ status: 401 })
    else await route.continue()
  })
  const users = await page.evaluate(async () => {
    const modulePath = '/src/features/auth/session.ts'
    const session = await import(modulePath)
    return Promise.all([session.authenticatedRequest('/api/auth/me'), session.authenticatedRequest('/api/auth/me')])
  })
  expect(users).toHaveLength(2)
  expect(refreshes).toBe(1)
})

test('refresh falho limpa sessao e nao entra em loop', async ({ page }) => {
  await createAndLogin(page)
  let refreshes = 0
  await page.route('**/api/auth/me', route => route.fulfill({ status: 401 }))
  await page.route('**/api/auth/refresh', route => {
    refreshes++
    return route.fulfill({ status: 401 })
  })
  await page.evaluate(async () => {
    const modulePath = '/src/features/auth/session.ts'
    const session = await import(modulePath)
    await session.authenticatedRequest('/api/auth/me').catch(() => {})
  })
  await expect(page).toHaveURL(/\/login$/)
  expect(refreshes).toBe(1)
})

test('logout com falha de rede limpa sessao e nao restaura no F5', async ({ page }) => {
  await createAndLogin(page)
  await page.route('**/api/auth/logout', route => route.abort())
  await page.getByLabel('Menu do usuário').click()
  await page.getByRole('button', { name: 'Sair', exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.getByRole('status')).toContainText('Não foi possível confirmar')
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Entre no SGF' })).toBeVisible()
  await page.goto('/app')
  await expect(page).toHaveURL(/\/login$/)
})

test('telas mobile sem overflow', async ({ page }) => {
  await page.setViewportSize({ width: 360, height: 800 })
  for (const path of ['/login', '/register']) {
    await page.goto(path)
    await expect(page.getByRole('heading')).toBeVisible()
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: `test-results/${path.slice(1)}-mobile.png`, fullPage: true })
  }
  await createAndLogin(page)
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/app-mobile.png', fullPage: true })
})

test('segundo 401 apos refresh nao inicia nova renovacao', async ({ page }) => {
  await createAndLogin(page)
  let refreshes = 0
  let attempts = 0
  page.on('request', r => { if (r.url().endsWith('/api/auth/refresh')) refreshes++ })
  await page.route('**/api/auth/me', route => { attempts++; return route.fulfill({ status: 401 }) })
  await page.evaluate(async () => {
    const modulePath = '/src/features/auth/session.ts'
    const session = await import(modulePath)
    await session.authenticatedRequest('/api/auth/me').catch(() => {})
  })
  await expect(page).toHaveURL(/\/login$/)
  expect(refreshes).toBe(1)
  expect(attempts).toBe(2)
})

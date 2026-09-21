import { expect, test } from '@playwright/test'
import type { Page } from '@playwright/test'

const api = process.env.SGF_TEST_API_URL ?? 'http://localhost:5206'

async function enterApp(page: Page) {
  const account = { name: 'Marina Costa', email: `frontend-${crypto.randomUUID()}@example.com`, password: 'TesteFrontend123!', companyName: 'Horizonte Comercial' }
  expect((await page.request.post(api + '/api/auth/register', { data: account })).status()).toBe(201)
  await page.goto('/login')
  await page.getByLabel('E-mail', { exact: true }).fill(account.email)
  await page.getByLabel('Senha', { exact: true }).fill(account.password)
  await page.getByRole('button', { name: 'Entrar', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
  return account
}

test('configuracoes exibem sessao e tema persiste na navegacao e no F5', async ({ page }) => {
  await page.addInitScript(() => { if (!localStorage.getItem('sgf.theme')) localStorage.setItem('sgf.theme', 'light') })
  await page.setViewportSize({ width: 1440, height: 960 })
  const account = await enterApp(page)
  await page.getByRole('navigation', { name: 'Navegação principal' }).getByRole('link', { name: 'Configurações' }).click()

  await expect(page.getByRole('heading', { name: 'Minha conta' })).toBeVisible()
  await expect(page.getByText(account.name, { exact: true }).last()).toBeVisible()
  await expect(page.getByText(account.email, { exact: true }).last()).toBeVisible()
  await expect(page.getByText('Proprietário', { exact: true })).toBeVisible()
  await expect(page.getByText(account.companyName, { exact: true }).last()).toBeVisible()
  const light = page.getByRole('radio', { name: /Claro/ })
  const dark = page.getByRole('radio', { name: /Escuro/ })
  await expect(light).toHaveAttribute('aria-checked', 'true')
  await page.screenshot({ path: 'test-results/settings-light-1440.png' })

  await dark.focus()
  await page.keyboard.press('Enter')
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await expect(dark).toHaveAttribute('aria-checked', 'true')
  expect(await page.evaluate(() => localStorage.getItem('sgf.theme'))).toBe('dark')
  await page.screenshot({ path: 'test-results/settings-dark-1440.png' })

  await page.getByRole('navigation', { name: 'Navegação principal' }).getByRole('link', { name: 'Produtos' }).click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Produtos', exact: true })).toBeVisible()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  await page.goto('/app/settings')
  await light.click()
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'light')
  await expect(light).toHaveAttribute('aria-checked', 'true')
})

test('dark mode responsivo cobre modulos e permanece no login apos logout', async ({ page }) => {
  test.setTimeout(90000)
  await page.addInitScript(() => { if (!localStorage.getItem('sgf.theme')) localStorage.setItem('sgf.theme', 'dark') })
  await enterApp(page)
  const routes = [
    ['dashboard', 'Visão geral'], ['products', 'Produtos'], ['inventory', 'Estoque'],
    ['finance', 'Financeiro'], ['analytics', 'Analytics'], ['settings', 'Configurações'],
  ] as const
  for (const width of [768, 390]) {
    await page.setViewportSize({ width, height: 900 })
    for (const [route, title] of routes) {
      await page.getByRole('button', { name: 'Abrir navegação' }).click()
      await page.getByRole('dialog', { name: 'Navegação do SGF' }).getByRole('link', { name: title, exact: true }).click()
      await expect(page).toHaveURL(route === 'dashboard' ? /\/app(?:\/dashboard)?$/ : new RegExp(`/app/${route}$`))
      await expect(page.getByRole('heading', { name: title, exact: true })).toBeVisible()
      await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
      expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    }
    await page.screenshot({ path: `test-results/settings-dark-${width}.png` })
  }
  await page.getByRole('button', { name: 'Sair da conta' }).click()
  await expect(page).toHaveURL(/\/login$/)
  await expect(page.locator('html')).toHaveAttribute('data-theme', 'dark')
  expect(await page.evaluate(() => localStorage.getItem('sgf.theme'))).toBe('dark')
  await expect(page.getByRole('heading', { name: 'Entre no SGF' })).toBeVisible()
  await page.screenshot({ path: 'test-results/login-dark-390.png' })
})

import { test, expect } from '@playwright/test'
import type { Page } from '@playwright/test'

async function enterApp(page: Page) {
  const account = { name: 'Marina Costa', email: `frontend-${crypto.randomUUID()}@example.com`, password: 'TesteFrontend123!', companyName: 'Horizonte Comercial' }
  const api = process.env.SGF_TEST_API_URL ?? 'http://localhost:5206'
  expect((await page.request.post(api + '/api/auth/register', { data: account })).status()).toBe(201)
  await page.goto('/login')
  await page.getByLabel('E-mail', { exact: true }).fill(account.email)
  await page.getByLabel('Senha', { exact: true }).fill(account.password)
  await page.getByRole('button', { name: 'Entrar', exact: true }).click()
  await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
}

test('layout compartilhado, navegacao, sessao e perfil placeholder', async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 960 })
  await enterApp(page)
  const nav = page.getByRole('navigation', { name: 'Navegação principal' })
  await expect(nav).toBeVisible()
  await expect(nav.getByRole('link', { name: 'Visão geral' })).toHaveAttribute('aria-current', 'page')
  await page.screenshot({ path: 'test-results/dashboard-desktop.png', fullPage: true })
  for (const [name, slug] of [['Produtos', 'products'], ['Estoque', 'inventory'], ['Financeiro', 'finance'], ['Analytics', 'analytics'], ['Configurações', 'settings']]) {
    await nav.getByRole('link', { name, exact: true }).click()
    await expect(page).toHaveURL(new RegExp('/app/' + slug + '$'))
    await expect(page.getByRole('heading', { level: 1, name })).toBeVisible()
    await expect(nav.getByRole('link', { name, exact: true })).toHaveAttribute('aria-current', 'page')
    if (slug === 'products' || slug === 'inventory') await expect(page.getByRole('heading', { name: 'Nenhum produto cadastrado' })).toBeVisible()
    else if (slug === 'finance') await expect(page.getByRole('heading', { name: 'Nenhum lançamento financeiro' })).toBeVisible()
    else if (slug === 'analytics') await expect(page.getByRole('heading', { name: 'Sem valores realizados no período' })).toBeVisible()
    else await expect(page.getByRole('heading', { name: 'Minha conta' })).toBeVisible()
  }
  await page.reload()
  await expect(page.getByRole('heading', { name: 'Configurações', exact: true })).toBeVisible()
  await page.getByLabel('Menu do usuário').click()
  await expect(page.getByText('Owner', { exact: true })).toBeVisible()
  await page.getByRole('link', { name: 'Meu perfil' }).click()
  await expect(page.getByRole('link', { name: 'Meu perfil' })).not.toBeVisible()
  await nav.getByRole('link', { name: 'Produtos', exact: true }).click()
  await page.screenshot({ path: 'test-results/products-placeholder.png', fullPage: true, animations: 'disabled' })
  await nav.getByRole('link', { name: 'Visão geral' }).click()
  await expect(page).toHaveURL(/\/app\/dashboard$/)
  await page.setViewportSize({ width: 1024, height: 768 })
  await expect(nav).toBeVisible()
  expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
  await page.screenshot({ path: 'test-results/dashboard-notebook.png', fullPage: true })
})

test('drawer acessivel em tablet e mobile, navegacao e logout', async ({ page }) => {
  await enterApp(page)
  for (const width of [768, 390]) {
    await page.setViewportSize({ width, height: 844 })
    const trigger = page.getByRole('button', { name: 'Abrir navegação' })
    await expect(trigger).toBeVisible()
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: `test-results/dashboard-${width}.png`, fullPage: true })
    await trigger.click()
    const drawer = page.getByRole('dialog', { name: 'Navegação do SGF' })
    await expect(drawer).toBeVisible()
    // Native dialogs may let focus visit browser chrome, but never the inert page.
    for (let step = 0; step < 10; step++) {
      await page.keyboard.press('Tab')
      expect(await drawer.evaluate(el => document.activeElement === document.body || el.contains(document.activeElement))).toBe(true)
    }
    await page.screenshot({ path: `test-results/menu-${width}.png`, fullPage: true })
    await page.keyboard.press('Escape')
    await expect(drawer).not.toBeVisible()
    await expect(trigger).toBeFocused()
    await trigger.click()
    await drawer.getByRole('link', { name: 'Financeiro', exact: true }).click()
    await expect(drawer).not.toBeVisible()
    await expect(page.getByRole('heading', { name: 'Financeiro', exact: true })).toBeVisible()
    await trigger.click()
    await drawer.getByRole('link', { name: 'Visão geral' }).click()
    await page.reload()
    await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
  }
  await page.getByLabel('Menu do usuário').click()
  await page.getByRole('button', { name: 'Sair', exact: true }).click()
  await expect(page).toHaveURL(/\/login$/)
  await page.goto('/app/finance')
  await expect(page).toHaveURL(/\/login$/)
})

import { expect, test } from '@playwright/test'
import { execFileSync } from 'node:child_process'

const api = process.env.SGF_TEST_API_URL ?? 'http://localhost:5206'

for (const width of [1440, 390]) {
  test(`dashboard e analytics com dados reais em ${width}px`, async ({ page }) => {
    test.setTimeout(60000)
    await page.addInitScript(theme => localStorage.setItem('sgf.theme', theme), width === 390 ? 'dark' : 'light')
    await page.setViewportSize({ width, height: 960 })
    const user = { name: 'Marina Costa', companyName: 'Horizonte Comercial', email: `frontend-${crypto.randomUUID()}@example.com`, password: 'TesteFrontend123!' }
    expect((await page.request.post(api + '/api/auth/register', { data: user })).status()).toBe(201)
    const login = await page.request.post(api + '/api/auth/login', { data: { email: user.email, password: user.password } })
    const session = await login.json()
    const headers = { Authorization: 'Bearer ' + session.accessToken }
    const empty = await (await page.request.get(api + '/api/dashboard', { headers })).json()
    await page.context().clearCookies()
    await page.goto('/login')
    await page.getByLabel('E-mail', { exact: true }).fill(user.email)
    await page.getByLabel('Senha', { exact: true }).fill(user.password)
    await page.getByRole('button', { name: 'Entrar', exact: true }).click()
    await expect(page.getByRole('heading', { name: 'Visão geral', exact: true })).toBeVisible()
    await expect(page.getByRole('heading', { name: 'Sem valores realizados no período' })).toBeVisible()
    await expect(page.getByText('Sem comparação disponível')).toHaveCount(2)
    await expect(page.getByRole('article', { name: 'Receitas recebidas', exact: true })).toContainText('R$ 0,00')

    async function post(path: string, data: object) {
      const response = await page.request.post(api + path, { headers, data })
      expect(response.status()).toBe(201)
      return response.json()
    }
    const mouse = await post('/api/products', { name: 'Mouse sem fio', sku: 'MOU-L', costPrice: 45, salePrice: 79.90, minimumStock: 5 })
    const keyboard = await post('/api/products', { name: 'Teclado compacto', sku: 'TEC-L', costPrice: 100, salePrice: 149.90, minimumStock: 2 })
    const cable = await post('/api/products', { name: 'Cabo USB', sku: 'CAB-L', costPrice: 10, salePrice: 19.90, minimumStock: 0 })
    for (const [id, entry, exit] of [[mouse.id, 10, 7], [keyboard.id, 4, 4]]) {
      await post('/api/inventory/entries', { productId: id, quantity: entry })
      await post('/api/inventory/exits', { productId: id, quantity: exit })
    }
    async function paid(type: string, amount: number) {
      const e = await post('/api/finance', { type, amount, description: type === 'Income' ? 'Serviços' : 'Estrutura', dueDate: empty.period.to })
      expect((await page.request.patch(api + '/api/finance/' + e.id + '/pay', { headers })).status()).toBe(200)
      return e
    }
    await paid('Income', 1000)
    await paid('Expense', 300)
    const previousIncome = await paid('Income', 800)
    const previousExpense = await paid('Expense', 200)
    const yesterday = new Date(empty.period.to + 'T12:00:00Z')
    yesterday.setUTCDate(yesterday.getUTCDate() - 1)
    await post('/api/finance', { type: 'Income', amount: 250, description: 'Recebimento pendente', dueDate: yesterday.toISOString().slice(0, 10) })
    await post('/api/finance', { type: 'Expense', amount: 180, description: 'Pagamento pendente', dueDate: yesterday.toISOString().slice(0, 10) })

    // Backdating is restricted to this local test's own rows, never an API or production seed.
    if (api !== 'http://localhost:5206') throw new Error('Historical fixture requires local SGF development database.')
    const ids = [session.companyId, cable.id, previousIncome.id, previousExpense.id]
    for (const id of ids) expect(id).toMatch(/^[a-f0-9-]{36}$/i)
    expect(empty.period.previousFrom).toMatch(/^\d{4}-\d{2}-\d{2}$/)
    const stamp = empty.period.previousFrom + 'T15:00:00Z'
    const sql = `UPDATE "Products" SET "CreatedAt" = NOW() - INTERVAL '35 days' WHERE "Id" = '${cable.id}' AND "CompanyId" = '${session.companyId}';
UPDATE "FinancialEntries" SET "PaidAt" = '${stamp}', "CreatedAt" = '${stamp}', "UpdatedAt" = '${stamp}' WHERE "Id" IN ('${previousIncome.id}', '${previousExpense.id}') AND "CompanyId" = '${session.companyId}';`
    execFileSync('docker', ['exec', 'sgf_postgres', 'psql', '-U', 'sgf_user', '-d', 'sgf_dev', '-v', 'ON_ERROR_STOP=1', '-c', sql], { stdio: 'pipe', windowsHide: true })

    await page.getByRole('button', { name: 'Atualizar indicadores' }).click()
    const metric = (name: string) => page.getByRole('article', { name, exact: true })
    await expect(metric('Receitas recebidas')).toContainText('R$ 1.000,00')
    await expect(metric('Receitas recebidas')).toContainText('+25%')
    await expect(metric('Despesas pagas')).toContainText('+50%')
    await expect(metric('Saldo realizado')).toContainText('R$ 700,00')
    await expect(metric('A receber')).toContainText('R$ 250,00')
    await expect(metric('A pagar')).toContainText('R$ 180,00')
    await expect(metric('Produtos ativos').locator('.analysis-value')).toHaveText('3')
    await expect(metric('Estoque baixo').locator('.analysis-value')).toHaveText('3')
    await expect(page.getByRole('region', { name: 'Reposição de estoque' })).toContainText('Teclado compacto')
    const insights = page.getByRole('region', { name: 'Insights', exact: true })
    await expect(insights).toContainText('2 produto(s) sem estoque')
    await expect(insights).toContainText('1 produto(s) sem movimentação')
    await expect(insights).toContainText('R$ 180,00 em contas a pagar vencidas')
    await insights.getByText('Critério', { exact: true }).first().click()
    await expect(insights.getByText('Produtos ativos com saldo atual igual a zero.')).toBeVisible()
    const chart = page.getByRole('img', { name: 'Gráfico de receitas recebidas e despesas pagas' })
    await expect.poll(() => chart.locator('.recharts-bar-rectangle path').evaluateAll(elements =>
      elements.some(element => {
        const bounds = element.getBoundingClientRect()
        return bounds.width > 0 && bounds.height > 0
      })), { message: 'O gráfico deve renderizar ao menos uma barra com valor.' }).toBe(true)
    expect(await chart.locator('.recharts-wrapper > svg.recharts-surface').evaluate(el => el.getBoundingClientRect().width)).toBeGreaterThan(200)
    await page.evaluate(() => window.scrollTo(0, 0))
    await page.screenshot({ path: `test-results/dashboard-real-${width}.png` })
    await page.getByRole('combobox', { name: 'Período', exact: true }).selectOption('last90')
    await expect(metric('Receitas recebidas')).toContainText('R$ 1.800,00')
    await expect(page.getByText('Por mês · R$')).toBeVisible()
    await page.reload()
    await expect(metric('Saldo realizado')).toContainText('R$ 1.300,00')
    await expect(page.getByRole('combobox', { name: 'Período', exact: true })).toHaveValue('last90')

    await page.goto('/app/analytics?period=last30')
    await expect(page.getByRole('heading', { level: 1, name: 'Analytics' })).toBeVisible()
    await expect(page.getByRole('region', { name: 'Contas vencidas', exact: true })).toContainText('2 lançamentos')
    const nav = page.getByRole('navigation', { name: 'Análises' })
    await nav.getByRole('link', { name: 'Estoque', exact: true }).click()
    await expect(metric('Entradas no período')).toContainText('14')
    await expect(metric('Saídas no período')).toContainText('11')
    await expect(metric('Valor estimado a custo')).toContainText('R$ 135,00')
    await expect(page.getByRole('region', { name: 'Produtos com maior saída de estoque' })).toContainText('Mouse sem fio')
    await page.screenshot({ path: `test-results/analytics-stock-${width}.png` })
    await nav.getByRole('link', { name: 'Produtos', exact: true }).click()
    await expect(page.getByRole('region', { name: 'Produtos sem movimentação', exact: true })).toContainText('Cabo USB')
    await page.reload()
    await expect(nav.getByRole('link', { name: 'Produtos', exact: true })).toHaveAttribute('aria-current', 'page')
    await expect(page.getByRole('region', { name: 'Produtos sem movimentação', exact: true })).toContainText('Cabo USB')
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true)
    await page.screenshot({ path: `test-results/analytics-products-${width}.png` })
  })
}

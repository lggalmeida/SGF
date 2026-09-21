# SGF - Sistema de Gestão Facilitada

O SGF é um protótipo funcional robusto desenvolvido como Trabalho de Conclusão
de Curso em Sistemas de Informação. O tema é:

> Desenvolvimento de uma plataforma SaaS para gestão integrada de estoque e
> finanças com análise de dados aplicada à tomada de decisão em pequenas empresas.

O sistema centraliza produtos, estoque e finanças e transforma esses registros
em indicadores e insights determinísticos. Ele apoia a decisão humana; não toma
decisões automaticamente e não utiliza inteligência artificial.

## Módulos Entregues

- identidade com ASP.NET Core Identity, access token JWT, refresh e logout;
- empresas, Memberships e papéis Owner, Admin e Member;
- isolamento multi-tenant por CompanyId;
- produtos com SKU único por empresa e inativação;
- estoque com saldo, mínimo, entradas, saídas e histórico imutável;
- financeiro com receitas, despesas, pendências e pagamentos;
- dashboard e analytics com períodos, comparações e insights explicáveis;
- frontend responsivo com temas claro e escuro.

Fornecedores, compras, vendas, notas fiscais, integrações bancárias e previsões
não integram a entrega atual. Veja [dívida técnica e trabalhos futuros](docs/14-divida-tecnica-e-trabalhos-futuros.md).

## Stack

| Área | Tecnologias |
| --- | --- |
| Backend | C#, .NET 10 LTS, ASP.NET Core Minimal APIs |
| Persistência | Entity Framework Core 10, Npgsql, PostgreSQL 17 |
| Frontend | React, TypeScript, Vite, TanStack Query, React Router, Recharts |
| Testes | xUnit, WebApplicationFactory, PostgreSQL real e Playwright |
| Ambiente local | Docker Compose |

## Pré-requisitos

- Git;
- .NET SDK compatível com o [global.json](global.json);
- Node.js 22.12 ou superior e npm;
- Docker Desktop com containers Linux;
- PowerShell 7.

Na raiz, confirme com:

```powershell
dotnet --version
node --version
docker version
```

Esta máquina também pode usar `./.dotnet/dotnet.exe` no lugar de `dotnet`.

## Configuração e Execução

Execute esta sequência a partir da raiz do repositório. Mantenha as variáveis
do backend no mesmo terminal em que a API será iniciada.

### 1. Configurar ambiente e JWT

```powershell
if (!(Test-Path .env)) { Copy-Item .env.example .env }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Jwt__SigningKey = [Convert]::ToBase64String(
  [System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64)
)
```

`.env` não é versionado. O ASP.NET Core não carrega esse arquivo; ele é usado
pelo Docker Compose. A chave JWT deve possuir pelo menos 32 bytes, não deve ser
exibida ou versionada e precisa ser reutilizada entre reinícios se for necessário
preservar tokens já emitidos.

### 2. Iniciar PostgreSQL

```powershell
docker compose up -d --wait
docker compose ps
```

O banco local fica restrito a `127.0.0.1:15432`. Os valores padrão de
desenvolvimento estão em `.env.example` e combinam com
`appsettings.Development.json`; não são credenciais de produção.

### 3. Restaurar backend e ferramenta EF

```powershell
dotnet restore src/backend/Sgf.sln
dotnet tool restore
```

### 4. Aplicar migrations

```powershell
dotnet ef database update --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api
```

As migrations criam Identity, Companies, Memberships, RefreshTokens, Products,
InventoryMovements e FinancialEntries.

### 5. Iniciar a API

```powershell
dotnet run --project src/backend/Sgf.Api --launch-profile http
```

- API: [http://localhost:5206](http://localhost:5206)
- Health: [http://localhost:5206/api/health](http://localhost:5206/api/health)
- Banco: [http://localhost:5206/api/health/database](http://localhost:5206/api/health/database)

### 6. Iniciar o frontend

Em outro terminal:

```powershell
cd src/frontend
npm ci
npm run dev -- --host localhost --port 5173 --strictPort
```

Acesse [http://localhost:5173/login](http://localhost:5173/login). O exemplo
`src/frontend/.env.example` permite configurar `VITE_API_BASE_URL`. Use
`localhost` tanto no frontend quanto no backend para manter o comportamento
same-site do cookie HttpOnly.

## Dados de Demonstração

Com API, frontend e PostgreSQL locais ativos, execute:

```powershell
./scripts/seed-demo.ps1
```

O script pede uma senha para `demo@sgf.local`; ela não é gravada nem exibida.
Como alternativa temporária:

```powershell
$env:SGF_DEMO_PASSWORD = 'Defina-Uma-Senha-Local1!'
./scripts/seed-demo.ps1
Remove-Item Env:SGF_DEMO_PASSWORD
```

A carga é manual, aceita apenas API HTTP em localhost e deve ser usada em banco
local limpo. Ela cria a empresa **Mercado Exemplo LTDA**, 11 produtos, saldos
variados, histórico, lançamentos pagos, pendentes e vencidos. Também ajusta,
diretamente no PostgreSQL local, somente as datas dos registros que acabou de
criar para formar períodos comparáveis e um produto parado. Não existe seed
automático em produção.

Para recriar exclusivamente o ambiente local do zero, sabendo que todos os dados
locais serão removidos:

```powershell
docker compose down -v
docker compose up -d --wait
dotnet ef database update --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api
```

O cenário e a ordem sugerida de apresentação estão em
[roteiro de demonstração](docs/15-roteiro-demonstracao.md).

## Capturas Finais

As evidências visuais geradas com o cenário demo estão em
[`docs/screenshots`](docs/screenshots):

- login em tema claro;
- dashboard em light/dark e desktop/mobile;
- Produtos, Estoque, Financeiro e Analytics;
- Configurações e navegação mobile.

As imagens são material acadêmico; credenciais, tokens e dados sensíveis não
aparecem nelas.

## Testes

Com PostgreSQL ativo:

```powershell
dotnet build src/backend/Sgf.sln
dotnet test src/backend/Sgf.sln --no-build
npm --prefix src/frontend run typecheck
npm --prefix src/frontend run build
npm --prefix src/frontend test
```

Os testes backend incluem `Sgf.Application.Tests`, `Sgf.Infrastructure.Tests`
e `Sgf.Api.Tests`. Os testes de integração aplicam migrations em bancos
temporários e validam isolamento entre tenants. O Playwright requer API e
PostgreSQL ativos; ele inicia o Vite quando necessário. Para usar o Edge local:

```powershell
$env:PLAYWRIGHT_CHANNEL = 'msedge'
npm --prefix src/frontend test
```

## Estrutura

```text
src/backend/
  Sgf.Domain/          entidades e regras centrais
  Sgf.Application/     contratos e casos de uso
  Sgf.Infrastructure/  EF Core, Identity e implementações
  Sgf.Api/             HTTP, autenticação e composição
  *.Tests/             testes unitários e de integração
src/frontend/
  src/app/             layout, rotas e providers
  src/features/        auth, produtos, estoque, financeiro e analytics
  src/components/      componentes compartilhados com uso real
  tests/               fluxos Playwright
docs/                  decisões, domínio e material acadêmico
scripts/               ferramentas locais controladas
```

O backend é um monólito modular: uma unidade de implantação com limites de
responsabilidade internos. Não há microsserviços, CQRS, MediatR, Redis ou
repository genérico.

## Segurança

- senhas são tratadas exclusivamente pelo ASP.NET Core Identity;
- access tokens JWT são curtos e refresh tokens opacos ficam em cookie HttpOnly;
- refresh tokens são armazenados somente por hash e rotacionados;
- Membership e Company são revalidados a cada contexto de tenant;
- Global Query Filters isolam leituras por CompanyId;
- proteção de escrita e tokens de concorrência impedem escrita cross-tenant;
- CORS aceita apenas origens configuradas;
- rotas de negócio exigem autenticação.

Essas práticas aproximam o protótipo de uma publicação, mas não substituem o
hardening operacional descrito na documentação de dívida técnica.

## Limitações

- uma sessão suporta uma única empresa; switch-company não foi implementado;
- não há confirmação de e-mail, recuperação de senha, MFA ou lockout;
- não há rate limiting, observabilidade, política de backup ou deploy de produção;
- refresh tokens não possuem famílias de sessão nem detecção avançada de reuso;
- financeiro não possui pagamento parcial, reversão, contas bancárias ou integração;
- estoque não possui depósitos, lotes, reservas, custo médio ou vínculo financeiro;
- analytics usa regras determinísticas e dados atuais, sem previsão ou IA;
- preferências de tema são locais ao navegador.

O SGF deve ser apresentado como **aplicação acadêmica com práticas de produção**,
não como serviço público pronto ou plataforma de escala enterprise.

## Documentação

- [Visão do produto](docs/01-visao-do-produto.md)
- [Requisitos](docs/02-requisitos.md)
- [Arquitetura](docs/03-arquitetura.md)
- [Modelo de domínio](docs/04-modelo-de-dominio.md)
- [Regras de negócio](docs/05-regras-de-negocio.md)
- [Roadmap](docs/06-roadmap.md)
- [Produtos](docs/10-produtos.md)
- [Estoque](docs/11-estoque.md)
- [Financeiro](docs/12-financeiro.md)
- [Dashboard e Analytics](docs/13-dashboard-analytics.md)
- [Dívida técnica e trabalhos futuros](docs/14-divida-tecnica-e-trabalhos-futuros.md)
- [Roteiro de demonstração](docs/15-roteiro-demonstracao.md)
- [Capturas finais](docs/screenshots)
- [ADRs](docs/adr)

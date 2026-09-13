# SGF - Sistema de Gestão Facilitada

Este repositório contém o projeto **SGF - Sistema de Gestão Facilitada**, desenvolvido como Trabalho de Conclusão de Curso em Sistemas de Informação.

O tema acadêmico do projeto é:

**Desenvolvimento de uma plataforma SaaS para gestão integrada de estoque e finanças com análise de dados aplicada à tomada de decisão em pequenas empresas.**

O objetivo é desenvolver uma solução acadêmica robusta, organizada e próxima de um produto real, priorizando simplicidade, boas práticas e clareza arquitetural.

## Objetivo do Produto

O SGF tem como finalidade apoiar pequenas empresas na gestão integrada de:

- produtos;
- estoque;
- fornecedores;
- movimentações de entrada e saída;
- receitas;
- despesas;
- contas a pagar;
- contas a receber;
- fluxo de caixa;
- indicadores gerenciais;
- insights para tomada de decisão.

O sistema será desenvolvido como uma aplicação SaaS multi-tenant, permitindo que múltiplas empresas utilizem a mesma plataforma com isolamento lógico de dados.

## Escopo Inicial do MVP

O MVP definido para o projeto inclui:

- cadastro e autenticação de usuários;
- cadastro de empresas;
- vínculo entre usuários e empresas;
- controle multi-tenant por empresa;
- cadastro de produtos;
- cadastro de fornecedores;
- movimentações de entrada e saída de estoque;
- controle básico de saldo de estoque;
- cadastro de receitas e despesas;
- contas a pagar e contas a receber;
- fluxo de caixa simples;
- dashboard com indicadores básicos;
- geração inicial de insights baseados em regras.

Funcionalidades avançadas, integrações externas, inteligência artificial generativa, microsserviços e infraestrutura complexa não fazem parte do escopo inicial.

## Stack Planejada

### Backend

- C#
- ASP.NET Core Web API
- Entity Framework Core

### Banco de Dados

- PostgreSQL

### Frontend

- React
- TypeScript
- TanStack Query

## Arquitetura

A arquitetura escolhida é um **monólito modular**.

Isso significa que o sistema será entregue como uma única aplicação backend, mas organizado internamente por módulos de negócio. Essa abordagem reduz a complexidade operacional e facilita o aprendizado, sem abrir mão de organização e separação de responsabilidades.

Os módulos previstos inicialmente são:

- Identidade e Acesso;
- Empresas e Multi-Tenancy;
- Produtos;
- Fornecedores;
- Estoque;
- Financeiro;
- Dashboard;
- Insights;
- Auditoria básica.

## Multi-Tenancy

A estratégia inicial será de **banco de dados compartilhado com isolamento por `CompanyId`**.

Cada entidade de negócio deverá possuir uma referência à empresa proprietária dos dados. Assim, as consultas, comandos e regras de autorização deverão sempre considerar a empresa atual do usuário autenticado.

Essa decisão foi tomada por equilibrar simplicidade, clareza de implementação e aderência ao contexto de um projeto acadêmico com características reais de SaaS.

## Documentação

A documentação inicial do projeto está disponível na pasta `/docs`:

- `01-visao-do-produto.md`
- `02-requisitos.md`
- `03-arquitetura.md`
- `04-modelo-de-dominio.md`
- `05-regras-de-negocio.md`
- `06-roadmap.md`

O arquivo `AGENTS.md` contém orientações para futuros agentes e colaboradores seguirem as decisões arquiteturais do projeto.

## Estado Atual

A fundacao e as fases A-F de identidade estao implementadas, com hardening F.1:

- cadastro de usuario, empresa e Membership Owner em transacao;
- login JWT, refresh token com rotacao e logout;
- contexto de tenant revalidado e policies OwnerOnly/AdminOrOwner;
- protecao reutilizavel de leitura e escrita multi-tenant;
- PostgreSQL local via Docker e migrations do EF Core;
- frontend React/TypeScript com login, cadastro e pagina de sessao protegida (fase G);
- testes em Sgf.Api.Tests, Sgf.Application.Tests e Sgf.Infrastructure.Tests.

Produtos, estoque, financeiro e analytics ainda nao foram implementados.
Decisoes de autenticacao: [ADR 006](docs/adr/006-refresh-token-and-authorization.md).

## Execucao Local

Siga a sequencia abaixo em PowerShell 7, a partir da raiz do clone SGF.
Use o mesmo terminal para os passos 2 a 7, pois as variaveis pertencem ao processo.

### 1. Pre-requisitos

- Git;
- .NET SDK 10.0.400 ou compativel com `global.json`;
- Node.js 22.12+ (ou 24) e npm;
- Docker Desktop iniciado, com containers Linux;
- PowerShell 7.

A pasta `.dotnet/` desta maquina nao e versionada. Em um clone novo, instale o SDK.
Se ja utiliza o SDK local, substitua `dotnet` por `./.dotnet/dotnet.exe` nos comandos
executados na raiz. Confira com `dotnet --version`, `node --version` e `docker version`.

### 2. Configurar o ambiente

```powershell
if (!(Test-Path .env)) { Copy-Item .env.example .env }
$env:ASPNETCORE_ENVIRONMENT = 'Development'
```

O Compose usa `.env`; ASP.NET Core nao carrega esse arquivo automaticamente.
Os valores padrao de PostgreSQL sao exclusivos de desenvolvimento e combinam com
`appsettings.Development.json`: host 127.0.0.1, porta 15432, banco sgf_dev,
usuario sgf_user. Se alterar porta/credenciais, ajuste tambem
`ConnectionStrings__DefaultConnection` no ambiente. Os testes atualmente usam os
valores locais padrao e criam seus proprios bancos; nao aponte testes para producao.

### 3. Configurar a chave JWT antes de migrations ou API

```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([System.Security.Cryptography.RandomNumberGenerator]::GetBytes(64))
```

Nao exiba nem versione a chave. Esse comando fornece uma chave somente para o
terminal e seus processos. Para manter JWTs entre reinicios, reutilize uma chave
armazenada em local seguro. Gerar outra chave invalida access tokens anteriores.
O placeholder de `.env.example` nao e uma chave para uso real.

Todos os ambientes, inclusive Testing, exigem chave explicita de pelo menos
32 bytes. As factories de teste fornecem uma chave exclusiva de testes, sem
alterar variaveis globais e sem fallback aleatorio.

### 4. Iniciar PostgreSQL

```powershell
docker compose up -d --wait
docker compose ps
```

A publicacao deve mostrar `127.0.0.1:15432->5432/tcp`.
O volume preserva dados entre reinicios; nao use `docker compose down -v` para
apenas reiniciar o sistema.

### 5. Restaurar projetos e ferramenta EF

```powershell
dotnet restore src/backend/Sgf.sln
dotnet tool restore
```

A versao da ferramenta `dotnet-ef` e definida no manifesto `dotnet-tools.json`
na raiz. Nenhuma instalacao global da ferramenta e necessaria.

### 6. Aplicar migrations

```powershell
dotnet ef database update --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api
```

O comando cria as tabelas de identidade, Companies, Memberships e RefreshTokens.
A F.1 nao acrescenta schema ou migration.

### 7. Executar API

```powershell
dotnet run --project src/backend/Sgf.Api --launch-profile http
```

API: http://localhost:5206. Validacao: `GET /api/health` e
`GET /api/health/database`. Autenticacao: `POST /api/auth/register`,
`POST /api/auth/login`, `POST /api/auth/refresh`, `POST /api/auth/logout`
e `GET /api/auth/me`.

### 8. Executar frontend atual

Em outro terminal, a partir da raiz:

```powershell
cd src/frontend
npm ci
npm run dev -- --host localhost --port 5173 --strictPort
```

Abra http://localhost:5173/login. Cadastro em /register e sessao protegida em /app.
A URL padrao do backend e http://localhost:5206; `VITE_API_BASE_URL` permite
configura-la. Use localhost nos dois enderecos para preservar comportamento
same-site dos cookies. O exemplo de configuracao esta em src/frontend/.env.example.
Detalhes da sessao e testes: [Frontend de autenticacao](docs/08-frontend-autenticacao.md).

### 9. Executar builds e testes

Com PostgreSQL ativo, em outro terminal na raiz (pare a API antes de recompilar
caso o Windows informe arquivos em uso):

```powershell
dotnet build src/backend/Sgf.sln
dotnet test src/backend/Sgf.sln --no-build
npm --prefix src/frontend run build
```

A solucao inclui `Sgf.Api.Tests`, `Sgf.Application.Tests` e
`Sgf.Infrastructure.Tests`. Integracao usa PostgreSQL real e migrations em bancos
temporarios. Os testes de isolamento usam entidade exclusiva de testes, verificam
SQL de UPDATE/DELETE e removem os bancos criados ao terminar, inclusive em falhas.

Testes de navegador da fase G (API e PostgreSQL devem estar ativos):

```powershell
cd src/frontend
npx playwright install chromium
npm test
npm run typecheck
```

O Playwright inicia o Vite quando necessario. Alternativa no Windows com Edge
instalado: defina `$env:PLAYWRIGHT_CHANNEL = 'msedge'` e dispense o download de Chromium.
Os testes criam contas identificadas por `frontend-...@example.com` no banco local.

## Pendencia Antes de Publicacao Publica

Rate limiting de login permanece como divida tecnica registrada pela auditoria.
Nao foi implementado na F.1. Iniciar o frontend nao significa autorizar publicacao
publica sem essa protecao e sem configuracao de HTTPS e segredos de producao.

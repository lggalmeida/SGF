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

Neste momento, o projeto possui a fundação técnica inicial:

- backend ASP.NET Core Web API;
- frontend React com TypeScript;
- PostgreSQL via Docker Compose;
- Entity Framework Core configurado;
- endpoint `GET /api/health`;
- endpoint `GET /api/health/database`;
- teste básico da API;
- teste básico da camada de aplicação.

Nenhuma funcionalidade de negócio foi implementada ainda.

## Como Executar Localmente

### Pré-requisitos

- .NET SDK 10;
- Node.js;
- npm;
- Docker Desktop.

### SDK .NET

O projeto possui um `global.json` apontando para o SDK .NET 10 usado na fundacao tecnica.

Nesta maquina, o SDK .NET 10 tambem foi instalado localmente em `.dotnet/`, pasta ignorada pelo Git. Se voce instalar o SDK .NET 10 globalmente no Windows, tambem podera usar `dotnet` normalmente.

### 1. Subir o PostgreSQL

Na raiz do projeto:

```powershell
docker compose up -d
```

O PostgreSQL ficará disponível em:

```text
Host: 127.0.0.1
Porta: 15432
Banco: sgf_dev
Usuario: sgf_user
```

A senha usada é apenas local de desenvolvimento e está documentada em `.env.example`.

### 2. Executar o Backend

```powershell
cd src\backend
..\..\.dotnet\dotnet.exe run --project Sgf.Api\Sgf.Api.csproj --launch-profile http
```

A API ficará disponível em:

```text
http://localhost:5206
```

Endpoints de validação:

```text
GET http://localhost:5206/api/health
GET http://localhost:5206/api/health/database
```

### 3. Executar o Frontend

Em outro terminal:

```powershell
cd src\frontend
npm install
npm run dev
```

O frontend ficará disponível normalmente em:

```text
http://localhost:5173
```

ou:

```text
http://127.0.0.1:5173
```

### 4. Executar Builds e Testes

Backend:

```powershell
cd src\backend
..\..\.dotnet\dotnet.exe build Sgf.sln
..\..\.dotnet\dotnet.exe test Sgf.sln
```

Frontend:

```powershell
cd src\frontend
npm run build
```

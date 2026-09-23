# Guia completo do SGF para estudo e defesa do TCC

> **Fonte de verdade:** código, configuração, migrations e testes existentes no repositório em setembro de 2026. Este guia descreve o que está implementado, não uma proposta de arquitetura. Os caminhos são relativos à raiz do repositório. `docs/01` a `docs/15` e os ADRs dão o contexto histórico; em caso de divergência, prevalece o código atual.

## Índice

1. [Visão geral](#1-visão-geral)
2. [Stack tecnológica](#2-stack-tecnológica)
3. [Arquitetura](#3-arquitetura)
4. [Domínio](#4-domínio)
5. [Banco de dados](#5-banco-de-dados)
6. [Multi-tenancy](#6-multi-tenancy)
7. [Identity e usuários](#7-identity-e-usuários)
8. [Cadastro](#8-cadastro)
9. [Login e JWT](#9-login-e-jwt)
10. [Refresh token e logout](#10-refresh-token-e-logout)
11. [Autorização](#11-autorização)
12. [Frontend](#12-frontend)
13. [Estado de autenticação no frontend](#13-estado-de-autenticação-no-frontend)
14. [TanStack Query](#14-tanstack-query)
15. [Produtos](#15-produtos)
16. [Estoque](#16-estoque)
17. [Financeiro](#17-financeiro)
18. [Dashboard e analytics](#18-dashboard-e-analytics)
19. [Insights](#19-insights)
20. [Curva ABC](#20-curva-abc)
21. [Settings e dark mode](#21-settings-e-dark-mode)
22. [Design e UX](#22-design-e-ux)
23. [API completa](#23-api-completa)
24. [Testes](#24-testes)
25. [Concorrência](#25-concorrência)
26. [Segurança](#26-segurança)
27. [Docker e execução](#27-docker-e-execução)
28. [Configuração](#28-configuração)
29. [Seed de demonstração](#29-seed-de-demonstração)
30. [Fluxos end-to-end](#30-fluxos-end-to-end)
31. [Decisões arquiteturais](#31-decisões-arquiteturais)
32. [O que não foi implementado](#32-o-que-não-foi-implementado)
33. [Dívida técnica](#33-dívida-técnica)
34. [Como publicar futuramente](#34-como-publicar-futuramente)
35. [Perguntas da banca](#35-perguntas-da-banca)
36. [Glossário](#36-glossário)
37. [Guia para estudar o código](#37-guia-para-estudar-o-código)
38. [Mapa de arquivos importantes](#38-mapa-de-arquivos-importantes)
39. [Resumo para defesa](#39-resumo-para-defesa)
40. [Autoavaliação](#40-autoavaliação)

## 1. Visão geral

**SGF** significa Sistema de Gestão Facilitada. O problema abordado é a fragmentação dos controles de pequenas empresas: produtos em uma planilha, entradas e saídas em anotações, contas em outro sistema e pouca capacidade de enxergar o conjunto. O SGF centraliza esses registros e deriva indicadores para **apoiar** decisões humanas. Não executa decisões automaticamente. O público-alvo é a pequena empresa que precisa de controle operacional compreensível, não um ERP fiscal completo. A formulação acadêmica está em `docs/01-visao-do-produto.md` e `README.md`.

É um SaaS **na arquitetura**: uma aplicação e um banco podem atender várias empresas, que compartilham infraestrutura mas não dados de negócio. Cada `Company` é um tenant. Ainda não há hospedagem pública, cobrança de assinatura, autoatendimento de múltiplas empresas ou operação contínua; portanto não o apresente como serviço comercial publicado.

O que existe: cadastro da conta e primeira empresa, login, renovação e logout, produtos, estoque com movimentos, financeiro básico, dashboard, analytics determinístico, configurações de aparência e interface responsiva. O que não existe: fornecedores, compras/vendas, nota fiscal, bancos, conciliação, contas bancárias, cobrança SaaS, seleção de empresa, permissões granulares, IA ou previsão. Limitações operacionais importantes incluem ausência de rate limiting e de uma infraestrutura de produção. O fluxo de uso é cadastrar-se, entrar, cadastrar produtos, movimentar estoque, registrar lançamentos, marcar pagamentos e consultar indicadores.

```text
Pessoa -> navegador / React + TypeScript
       -> ASP.NET Core Minimal API (HTTP, autenticação, respostas)
       -> contratos da Application (casos de uso e respostas)
       -> Domain (entidades e invariantes)
       -> Infrastructure (Identity, EF Core, consultas e persistência)
       -> PostgreSQL
       <- JSON / estado de servidor / interface
```

Esse desenho é **lógico**, não a ordem literal de referências: as implementações dos contratos Application ficam em Infrastructure e usam Domain; a API compõe ambos. Veja `src/backend/Sgf.Api/Program.cs`, `src/backend/Sgf.Infrastructure/DependencyInjection.cs` e `src/frontend/src/App.tsx`.

## 2. Stack tecnológica

As versões abaixo vêm de `global.json`, dos `*.csproj`, de `src/frontend/package.json` e de `docker-compose.yml`; dependências com `^` no npm podem resolver versões patch/minor diferentes no lockfile.

| Tecnologia | O que é e uso real | Por que aqui; vantagem e limite | Alternativa não necessária agora |
| --- | --- | --- | --- |
| C# / .NET 10 | Linguagem e runtime/SDK do backend `net10.0`. | Tipagem, ecossistema ASP.NET/EF e LTS; requer SDK compatível. | Java/Spring ou Node seriam viáveis, mas trocariam a stack aprovada sem ganho demonstrado. |
| ASP.NET Core Minimal APIs | Framework HTTP; rotas em `Program.cs` e `*Endpoints.cs`. | Pouca cerimônia para contratos pequenos; exige disciplina para manter handlers finos. | Controllers MVC seriam válidos, mas não necessários para estas rotas. |
| EF Core 10 | ORM, mapeamento, LINQ, tracking, transações, migrations e filtros. | Integra domínio e PostgreSQL sem SQL repetitivo; exige atenção a SQL gerado, tracking e filtros. | SQL manual/Dapper dariam controle fino, com mais código de persistência. |
| Npgsql EF Provider 10 | Traduz EF Core para PostgreSQL. | Tipos `numeric`, `date`, `timestamptz`, constraints e índices; acopla persistência ao provedor. | Outro banco mudaria migrations e semântica. |
| PostgreSQL 17 | Banco relacional no container. | Transações, FK, checks, índices e agregações; precisa ser operado e copiado em produção. | SQLite é prático para protótipos, mas não reproduz concorrência e tipos PostgreSQL. |
| ASP.NET Core Identity | Gestão de usuários, senha e hash em Infrastructure. | Evita construir autenticação de senha artesanal; adiciona tabelas próprias. | Sistema próprio de senhas seria risco injustificado. |
| JWT Bearer | Access token assinado validado pela API. | Requisições autenticadas sem consultar sessão central a cada token; expiração/revogação precisam ser tratadas. | Cookie de sessão de servidor seria possível, mas diferente do contrato API/SPA escolhido. |
| React 19 / TypeScript 6 | Interface por componentes com tipos. | Reuso e contratos mais verificáveis; build e estado de cliente adicionam complexidade. | HTML server-side seria mais simples, mas não atenderia a SPA planejada. |
| Vite 8 | Servidor de desenvolvimento e build frontend. | Feedback rápido e bundle estático; variáveis `VITE_*` são públicas. | Outra ferramenta de build não resolveria necessidade adicional. |
| TanStack Query 5 | Cache de dados recebidos da API, mutations e invalidação. | Evita gerenciar manualmente listas remotas; não substitui estado local. | Redux/Zustand criariam outra camada para pouco benefício. |
| React Router 7 | Rotas públicas e autenticadas em `App.tsx`. | Navegação SPA e rotas aninhadas; autorização real continua na API. | Navegação manual por pathname seria frágil. |
| Lucide React | Ícones da interface. | Consistência com baixo custo; não é sistema de componentes. | SVGs manuais multiplicariam manutenção. |
| Recharts 3 | Gráfico financeiro em `FinancialChart.tsx`. | Abstrai eixos/tooltips/barras; adiciona peso de bundle, por isso o componente é carregado sob demanda. | SVG próprio para um gráfico demandaria lógica visual extra. |
| xUnit 2 / WebApplicationFactory | Testes .NET e host da API em integração. | Comportamento próximo ao HTTP real; integração com PostgreSQL é mais lenta. | Só testes unitários não revelariam falhas de migration, autenticação e isolamento. |
| Playwright | Navegador automatizado em `src/frontend/tests`. | Exercita navegação, cookies, F5 e responsividade; depende de API/banco disponíveis. | Testes de componentes isolados não cobrem o fluxo ponta a ponta. |
| Docker Compose | PostgreSQL local, porta/volume/health em `docker-compose.yml`. | Ambiente reproduzível; exige Docker. Não é o deploy do SaaS. | Instalação manual do banco dificulta repetição. |
| Git | Histórico do repositório, `.gitignore`, código e migrations. | Rastreabilidade; não substitui backup de banco. | Não aplicável. |
| EF migrations | Arquivos versionados que evoluem o schema. | Aplicação controlada e auditável; devem ser testadas antes de publicação. | Criar tabelas manualmente perderia reprodutibilidade. |

Os pacotes de teste e versões exatas são conferíveis nos arquivos `src/backend/Sgf.*.Tests/*.csproj`. Não há Redis, Kafka, MediatR, CQRS, AutoMapper nem biblioteca externa de multi-tenancy no projeto.

## 3. Arquitetura

O backend é **um processo** com divisão por responsabilidades; “monólito modular” aqui não significa múltiplos serviços implantados. `src/backend/Sgf.sln` agrupa os projetos.

| Projeto | Responsabilidade e exemplos reais | Dependências de projeto | Não deve conter |
| --- | --- | --- | --- |
| `Sgf.Domain` | Entidades e invariantes: `Products/Product.cs`, `Inventory/InventoryMovement.cs`, `Finance/FinancialEntry.cs`, `Companies/*`. | Nenhuma referência aos outros projetos. | HTTP, EF, cookie, UI. |
| `Sgf.Application` | Contratos de entrada/saída e interfaces de casos de uso: `Products/ProductContracts.cs`, `Inventory/InventoryContracts.cs`, `Finance/FinanceContracts.cs`, `Analytics/InsightRules.cs`, `Identity/ICurrentTenantContext.cs`. | Domain e Shared. | `DbContext`, `HttpContext`, Npgsql. |
| `Sgf.Infrastructure` | Implementação dos serviços Application, `SgfDbContext`, configurações EF, migrations, Identity, JWT, consultas SQL via EF. | Application, Domain, Shared. | Rotas/HTML ou regras de apresentação. |
| `Sgf.Api` | Composição, autenticação HTTP, CORS, endpoints e tradução de erros para status. | Application e Infrastructure. | Regras inteiras de cadastro/estoque/financeiro dentro dos handlers. |
| `Sgf.Shared` | Projeto referenciado por Application e Infrastructure, mas sem responsabilidade funcional clara/código relevante hoje. | Nenhuma. | Tornar-se “pasta de coisas genéricas”; só adicionar com necessidade transversal real. |

```text
Sgf.Domain <- Sgf.Application <- Sgf.Infrastructure <- Sgf.Api
                       ^                ^                 |
                       +----------------+-----------------+
Sgf.Shared  ---------> Application e Infrastructure (referência, sem papel funcional atual)
Sgf.Api também referencia Application diretamente; não há ciclo.
```

Uma requisição `POST /api/products` chega a `ProductEndpoints`, que valida a fronteira HTTP e chama `IProductService`. `ProductService` (Infrastructure) consulta e salva via `SgfDbContext`, chama a entidade `Product` (Domain), usa `ICurrentTenantContext` (contrato Application) e devolve um resultado para o endpoint traduzir em 201/400/409. Veja `src/backend/Sgf.Api/Products/ProductEndpoints.cs`, `src/backend/Sgf.Infrastructure/Products/ProductService.cs` e `src/backend/Sgf.Domain/Products/Product.cs`.

Por que não microserviços? Não há equipes, escalas ou implantação independente que justifiquem rede, consistência distribuída e observabilidade extra. Por que não CQRS/MediatR? Os serviços existentes já coordenam comandos e consultas sem um barramento. Por que não repository genérico/Unit of Work customizado? `DbContext` já oferece consultas, tracking e `SaveChanges` transacional. Limite: partes do backend continuam acopladas ao EF Core em Infrastructure, e a arquitetura não garante modularidade por processo. Isso é uma escolha deliberada, não ausência de arquitetura. Referência histórica: `docs/03-arquitetura.md`.

## 4. Domínio

**ApplicationUser** (`src/backend/Sgf.Infrastructure/Identity/ApplicationUser.cs`) estende `IdentityUser`. Seu `Id` é `string` do Identity, possui `Name`, `Email` e `PasswordHash` gerenciado por Identity. É criado no cadastro via `UserManager`, não pelo frontend diretamente. Não possui `IsActive` próprio; desativação de acesso a empresa ocorre pelo Membership/Company. Não implementa `ICompanyScopedEntity`: uma mesma pessoa pode estar em mais de uma empresa.

**Company** (`src/backend/Sgf.Domain/Companies/Company.cs`) é o tenant. `Id: Guid` identifica a empresa, `Name` obrigatório até 200, `TradeName` opcional, `IsActive`, `CreatedAt`, `UpdatedAt`. É criada ativa no cadastro; não há CRUD público de empresa. Não é filtrada “por si mesma”, pois é peça do mecanismo de seleção/revalidação.

**Membership** (`src/backend/Sgf.Domain/Companies/Membership.cs`) liga `UserId: string` a `CompanyId: Guid`; possui `Id: Guid`, `Role` (`Owner`, `Admin`, `Member`), `IsActive` e `CreatedAt`. O par UserId+CompanyId é único. O cadastro cria Owner ativo. Não há tela/API de convites, edição de papel ou switch-company. Também não recebe o filtro genérico de dados de negócio: login precisa inspecionar os vínculos do usuário para selecionar uma empresa.

**RefreshToken** (`src/backend/Sgf.Infrastructure/Identity/Authentication/RefreshToken.cs`) é persistência de sessão, não entidade de domínio de negócio. Guarda `Id`, `UserId`, `CompanyId`, `TokenHash`, `CreatedAt`, `ExpiresAt`, `RevokedAt`. Login cria, refresh rotaciona/revoga, logout revoga; o token bruto nunca vai ao banco. Company fixa a empresa daquela sessão.

**Product** (`src/backend/Sgf.Domain/Products/Product.cs`) possui `Id`, `CompanyId`, `Name`, `SKU`, `Description?`, `CostPrice`, `SalePrice`, `IsActive`, `CreatedAt`, `UpdatedAt`, `CurrentStock`, `MinimumStock`. Nome/SKU obrigatórios; SKU é normalizado; preço e quantidades não podem ser negativos e devem caber na precisão do banco. `CurrentStock` nasce zero e **somente** `RecordMovement` o altera; editar o cadastro do produto altera mínimo, não saldo. Produto inativo permanece no banco e reserva SKU para manter referência histórica. Implementa `ICompanyScopedEntity`.

**InventoryMovement** (`src/backend/Sgf.Domain/Inventory/InventoryMovement.cs`) guarda evento de entrada/saída com `Id`, `CompanyId`, `ProductId`, `Type`, `Quantity`, `Notes?`, `CreatedAt`, `UserId`. Quantidade positiva; histórico não tem endpoint de edição/exclusão e `SaveChanges` também bloqueia Modified/Deleted. Isso protege auditabilidade operacional. O nome atual do produto/usuário exibido em consultas não é um snapshot imutável do texto daquele dia; IDs, tipo, quantidade e data são preservados. Implementa `ICompanyScopedEntity`.

**FinancialEntry** (`src/backend/Sgf.Domain/Finance/FinancialEntry.cs`) unifica receita/despesa e pendência/realização. Campos: `Id`, `CompanyId`, `Type` Income/Expense, `Description`, `Category?`, `Amount`, `DueDate`, `PaidAt?`, `Status` Pending/Paid, `Notes?`, `CreatedAt`, `UpdatedAt`, `Version`. É criada Pending; enquanto Pending pode ser editada; `Pay` marca como Paid com instante atual e é idempotente. Não há exclusão/reversão/pagamento parcial. `Version` é token de concorrência interno. Implementa `ICompanyScopedEntity`.

Relações de negócio: Company 1:N Product/FinancialEntry/InventoryMovement/Membership; User 1:N Membership/RefreshToken/InventoryMovement; Product 1:N InventoryMovement. Classes de domínio guardam invariantes locais; serviços guardam operações que atravessam entidade, banco e contexto autenticado. DTOs ficam em `Sgf.Application/*/*Contracts.cs`.

## 5. Banco de dados

`src/backend/Sgf.Infrastructure/Database/SgfDbContext.cs` é o `DbContext` único, também derivado do contexto EF do Identity. `OnModelCreating` configura mapeamentos de `Database/Configurations/*` e o filtro tenant-scoped; `SaveChanges` aplica proteção de escrita. O PostgreSQL local é 17 Alpine (`docker-compose.yml`). Ids de negócio são `Guid`/UUID; o Identity mantém `string`. UUID facilita criação independente de registros e dificulta adivinhação sequencial, **mas não substitui autorização**.

```text
AspNetUsers (string Id) 1---N Memberships N---1 Companies (uuid Id)
       |                        UNIQUE(UserId, CompanyId) |
       +---N RefreshTokens N-----------------------------+
       +---N InventoryMovements (responsável)           +---N Products
                                                       |      |
                                                       |      +---N InventoryMovements
                                                       +---N FinancialEntries
```

**Migrations reais, em ordem** (`src/backend/Sgf.Infrastructure/Database/Migrations`):

1. `20260907234845_InitialIdentityAndCompanies`: cria `AspNetUsers`, `AspNetUserClaims`, `AspNetUserLogins`, `AspNetUserTokens`, `Companies`, `Memberships`; FK de Membership para usuário e empresa, unicidade `(UserId, CompanyId)` e check dos papéis. **Não** cria tabelas `AspNetRoles`/`AspNetUserRoles`, porque a role é do Membership. Índice de NormalizedUserName único; índice de e-mail normalizado não é constraint única de banco, embora o Identity/fluxo de cadastro rejeite duplicação em condições normais.
2. `20260909223044_AddRefreshTokens`: cria `RefreshTokens`, hash único, índices de CompanyId e `(UserId, CompanyId)`, FKs. Necessária para rotação e logout persistidos.
3. `20260914002854_AddProducts`: cria `Products`, FK para Companies, índice único `(CompanyId, SKU)`, checks de domínio e preços `numeric(12,2)`. O mesmo SKU em duas empresas é permitido.
4. `20260919203320_AddInventory`: adiciona `CurrentStock` e `MinimumStock` `numeric(14,3)` com default zero a Products, check de não-negatividade, chave alternativa `(CompanyId, Id)` e `InventoryMovements`; FK composta `(CompanyId, ProductId)` para Products impede movimento apontar produto de outra empresa mesmo por erro de código. `UserId` referencia AspNetUsers com exclusão restrita, preservando o autor histórico. Índices `(CompanyId, CreatedAt)` e `(CompanyId, ProductId, CreatedAt)` servem histórico geral/por produto.
5. `20260919211207_AddFinancialEntries`: cria `FinancialEntries`, `Amount numeric(14,2)`, `DueDate date`, checks de tipo/valor/status e consistência Status/PaidAt, FK Company e índice `(CompanyId, DueDate)`. Não indexa indiscriminadamente Status/Type.

`numeric(12,2)` suporta 10 dígitos inteiros e dois decimais nos preços; `numeric(14,2)` suporta 12+2 no financeiro; `numeric(14,3)` suporta 11+3 no estoque. `DateOnly`/`date` expressa vencimento civil sem hora; `DateTimeOffset`/`timestamp with time zone` guarda instantes em UTC. A aplicação usa UTC para eventos e, no analytics, dia de negócio em UTC-03 fixo. PostgreSQL preserva microssegundos, menos precisão que ticks .NET; serviços normalizam instantes quando relevante para evitar divergência de comparação.

FKs de histórico/empresa usam `Restrict` onde exclusão causaria perda de referência; vínculos Identity podem ter cascade conforme mapeamento. Confira comportamento exato em `Database/Configurations/*Configuration.cs` e nas migrations. `Version` do Financeiro e `CurrentStock`, `IsActive`, `CompanyId` são concurrency tokens no EF; não há coluna SQL `rowversion` automática. Migrations devem ser aplicadas em ordem com o comando do README; `SgfDbContextModelSnapshot.cs` é o estado atual para gerar a próxima, não migration de execução.

## 6. Multi-tenancy

**Tenant** é uma organização isolada em um software compartilhado. No SGF, `Company` é tenant e cada tabela operacional contém `CompanyId`. Banco compartilhado é mais simples de desenvolver/operar para o TCC do que banco por empresa, mas aumenta a importância de filtros, constraints e testes. Não basta esconder registros na tela: a API é a fronteira de segurança.

1. Login consulta Membership ativo e Company ativa. Com exatamente um vínculo elegível, o JWT assinado recebe `sub`, `company_id` e role (`AccessTokenFactory.cs`). O cliente não escolhe esse ID no body.
2. `CurrentTenantContext` (`src/backend/Sgf.Api/Identity/CurrentTenantContext.cs`) lê claims **do JWT já validado** e reconsulta usuário, Membership e Company. Confere ativo e igualdade da role atual. É `scoped` por request e memoiza a resolução para evitar consultas repetidas. `ICurrentTenantContext`/`CurrentTenant` ficam em Application para os casos de uso não conhecerem HTTP.
3. Os serviços chamam `ICurrentTenantContext.GetCurrentAsync` antes de acessar tabelas de negócio; se o resultado for nulo, rejeitam a operação. A infraestrutura define o CompanyId atual no `SgfDbContext` via `UseCurrentCompany`.
4. `ICompanyScopedEntity` (`src/backend/Sgf.Domain/Companies/ICompanyScopedEntity.cs`) marca `Product`, `InventoryMovement`, `FinancialEntry`. `CompanyScopedModelBuilderExtensions` registra em cada uma um **Global Query Filter** equivalente a `CurrentCompanyId != null && row.CompanyId == CurrentCompanyId`. Um `Find`/`Single`/listagem LINQ normal sobre essas entidades só enxerga o tenant. Sem tenant, o predicado é falso: **fail closed**.
5. `CompanyScopedChangeTrackerExtensions` define `CompanyId` na criação e, em Modified/Deleted, exige valores original e atual coerentes com o contexto. Mudar o CompanyId é proibido. `CompanyId` também é concurrency token: o EF emite conceitualmente `UPDATE ... WHERE Id=@id AND CompanyId=@originalCompanyId` ou `DELETE ... WHERE Id=@id AND CompanyId=@originalCompanyId`. Zero linha afetada gera `DbUpdateConcurrencyException`. Isso cobre inclusive objetos construídos manualmente e anexados via `Attach`, `Update` ou `Remove`, nos quais OriginalValue e CurrentValue poderiam ambos mentir dizendo A sem um SELECT prévio. Foi o problema HIGH da auditoria F.1; o **predicado no banco**, não apenas a inspeção do objeto, o corrige.

Exemplo: A possui produto A-1, B possui B-1. A consulta lista: o filtro adiciona `CompanyId=A`; B-1 não aparece. A tenta GET por `Id=B-1`: o filtro faz a busca se comportar como inexistente (404, reduz IDOR). A tenta criar produto mandando `CompanyId=B`: o DTO nem possui esse campo e `SaveChanges` grava A do contexto. A constrói objeto com `Id=B-1, CompanyId=A` e tenta `Update`: a linha B-1 tem `CompanyId=B`, portanto `WHERE Id=B-1 AND CompanyId=A` afeta zero linhas. Para movimentos, a FK composta reforça o vínculo produto/empresa.

Leitura, criação e alteração/exclusão exigem proteções **diferentes**; o filtro de leitura sozinho não protege `Attach`. `Company`, `Membership`, Identity e RefreshToken são dados de identidade e não recebem indiscriminadamente o filtro de negócio. `IgnoreQueryFilters()` retiraria a proteção de leitura e precisa ser excepcional/revisado; não é usado no fluxo comum. SQL bruto e `ExecuteUpdate`/`ExecuteDelete` não passam pelo ChangeTracker nem pela proteção de concorrência em `SaveChanges`: se forem usados no futuro, exigem predicado tenant explícito. Veja `docs/adr/004-current-tenant-context.md`, `docs/adr/005-company-scoped-data-isolation.md` e testes de `Sgf.Infrastructure.Tests` com entidades **exclusivas de teste**, sem tabela de produção.

## 7. Identity e usuários

ASP.NET Core Identity, configurado em `src/backend/Sgf.Infrastructure/DependencyInjection.cs`, cria/verifica hashes de senha por meio de `UserManager<ApplicationUser>`; a aplicação nunca compara `PasswordHash` manualmente. Regras atuais: mínimo 8 caracteres, dígito, minúscula, maiúscula e símbolo; e-mail único no fluxo do Identity. `PasswordHash` é dado interno do `AspNetUsers`, não retorno de API. Hash de senha é diferente de criptografia reversível; o Identity administra algoritmo/salt/versões.

Das tabelas Identity criadas pela migration inicial, o SGF usa diretamente `AspNetUsers` (dados, senha e consultas via UserManager). `AspNetUserClaims`, `AspNetUserLogins` e `AspNetUserTokens` são suporte do esquema Identity, mas não fundamentam as roles por empresa nem o refresh token próprio do SGF. Não existem `AspNetRoles` e `AspNetUserRoles` neste modelo. O índice de e-mail normalizado não é UNIQUE no PostgreSQL; a regra de unicidade da conta está no Identity/fluxo normal de cadastro, ponto importante para não confundir regra de aplicação com constraint de banco.

`ApplicationUser` representa pessoa. `Membership` representa **papel naquela empresa**: João pode ser Owner em A e Member em B sem duas contas. `MembershipRole` define Owner, Admin, Member; não há papéis globais do ASP.NET Identity nem permissões por ação. No estado atual, o cadastro cria a primeira empresa e Owner, mas login com dois vínculos ativos retorna `company_selection_required`, pois não existe switch-company. Gerenciamento de usuários, convites, recuperação e confirmação de e-mail estão fora do escopo. Entender essa distinção é essencial: usuário autenticado não ganha automaticamente acesso a todo registro do banco.

## 8. Cadastro

`POST /api/auth/register` em `src/backend/Sgf.Api/Program.cs` recebe `name`, `email`, `password`, `companyName`; **não** aceita CompanyId/Role. O endpoint chama `IRegisterCompanyOwnerUseCase`, implementado por `RegisterCompanyOwnerService` (`src/backend/Sgf.Infrastructure/Identity/Registration/RegisterCompanyOwnerService.cs`). O caso de uso valida nome, e-mail, senha e nome de empresa (obrigatório, até 200), verifica duplicação e abre transação EF. `UserManager.CreateAsync` cria o usuário pelo Identity; depois Company ativa e Membership Owner ativo são adicionados; `SaveChanges` e commit encerram a operação.

```text
Formulário -> POST register -> endpoint fino -> RegisterCompanyOwnerService
          -> validação -> BeginTransaction -> Identity/UserManager
          -> Company -> Membership(Owner) -> SaveChanges -> Commit -> 201
```

**Atomicidade**: ou as três partes são confirmadas, ou nenhuma. Se Company/Membership falhar depois de `UserManager.CreateAsync`, a transação não confirma o usuário. Um teste de integração provoca falha real de persistência para comprovar rollback; entrada inválida de empresa é barrada antes e não prova rollback. E-mail duplicado retorna 409; validação (inclusive senha pelas regras Identity) retorna 400; persistência inesperada retorna erro genérico 500, sem stack trace. A resposta contém identificadores seguros, nunca senha/hash. O cadastro **não autentica**: usuário precisa chamar login depois. Veja `docs/adr/002-register-transaction.md`.

## 9. Login e JWT

Autenticação responde “quem é você?”; autorização responde “pode fazer isto nesta empresa?”. JWT é um conjunto de claims assinado, enviado como `Authorization: Bearer <accessToken>`. Assinatura HMAC-SHA256 permite à API detectar alteração, **não criptografa o conteúdo**. `Issuer` identifica emissor esperado; `Audience`, destinatário; `exp`, validade. A Signing Key é secreta e externa ao Git. `Program.cs` valida assinatura, emissor, audiência e expiração com `ClockSkew=0`; configurações inválidas impedem iniciar a API. `AccessTokenFactory.cs` emite:

- `sub`: Id string do usuário, para identificação;
- `company_id`: Guid da empresa daquela sessão;
- role/`ClaimTypes.Role`: papel do Membership, não papel global;
- `jti`: ID único do token; `iat`: emissão;
- claims padrão `iss`, `aud`, `exp` geradas na emissão.

Não inclui e-mail, senha, hash ou dados de empresa. `JwtSecurityTokenHandler.DefaultMapInboundClaims=false` evita renomear claims silenciosamente. `POST /api/auth/login` chama `LoginService.cs`: localiza por e-mail, `UserManager.CheckPasswordAsync`, seleciona vínculos ativos com empresas ativas. Zero empresa elegível: 403 sem token; duas ou mais: 409 `company_selection_required`, sem escolher a primeira; uma: cria JWT e refresh token persistido, devolve JSON de access e cookie. Usuário inexistente e senha errada recebem a mesma mensagem genérica e 401, dificultando enumeração. A conta precisa de nova autenticação após cadastro.

`GET /api/auth/me` exige Bearer; `GetCurrentUserService.cs` usa `CurrentTenantContext` e busca os dados atuais seguros de pessoa e empresa. Não se limita a ecoar claims: usuário, vínculo, empresa e role são revalidados. Sem/invalidado/expirado JWT, o middleware responde 401; JWT válido mas contexto tenant inválido recebe 403. 401 significa “não autenticado/credencial inválida”; 403, “identidade autenticada, acesso à empresa não permitido agora”. IDs de recurso de outro tenant nos módulos de negócio respondem 404, para não revelar sua existência.

## 10. Refresh token e logout

O access JWT dura **15 minutos** por padrão (`Jwt:AccessTokenMinutes`): reduz janela de uso de token vazado. Depois de expirar, chamadas protegidas recebem 401. Um JWT válido por dias seria mais difícil de invalidar. Refresh é credencial diferente: **64 bytes aleatórios criptográficos** codificados em Base64 (`RefreshTokenGenerator.cs`), opaca, válida **7 dias** por padrão (`RefreshToken:Days`). Somente SHA-256 hexadecimal do valor bruto é salvo em `RefreshTokens`; por alta entropia, hash rápido é adequado para este token, **não** para senha humana. Não é JWT.

Browser recebe refresh em cookie `sgf_refresh_token`: `HttpOnly` (JavaScript não lê), `SameSite=Lax`, `Path=/api/auth`, sem Domain, `Secure` fora de Development/Testing. Frontend `localhost:5173` e API `localhost:5206` são origens diferentes, mas mesmo site local; requisições de login/refresh/logout usam `credentials: include`, CORS permite credenciais apenas para origens explícitas. POSTs auth com header Origin não autorizado são bloqueados. Produção exige HTTPS e topologia de domínios compatível com SameSite; hospedagem cross-site requer revisão/CSRF.

`POST /api/auth/refresh`: cookie -> hash -> busca token não expirado/não revogado -> revalida usuário, Membership e Company -> lê **role atual** -> emite access -> marca token antigo revogado e cria sucessor em um `SaveChanges`. `RevokedAt` é concurrency token: dois refreshes simultâneos do mesmo token não vencem juntos; um perde e recebe 401. O token velho não pode normalmente ser reutilizado. Não há família de tokens, detecção elaborada de roubo nem prazo absoluto de sessão: cada sucessor recebe novos sete dias. Se a resposta da rotação se perde, novo login pode ser necessário. `RefreshTokenService.cs` e `docs/adr/006-refresh-token-and-authorization.md` detalham.

`POST /api/auth/logout` tenta revogar o refresh do cookie, apaga o cookie e responde 204 inclusive se já não houver sessão. Isso encerra **aquela sessão de refresh**, não todos os dispositivos. O JWT já emitido é autocontido e pode continuar válido até expirar (sujeito à revalidação de Membership/Company); não há blacklist. O frontend apaga o access em memória e o cache imediatamente. Se a rede falhar, a sessão local termina, mas a revogação no servidor não é garantida; há sinalizador local de logout pendente para tentar de novo. Nunca diga que logout revoga instantaneamente todo JWT.

## 11. Autorização

`Owner`, `Admin` e `Member` são níveis simples de Membership (`src/backend/Sgf.Domain/Companies/MembershipRole.cs`). `Program.cs` registra `OwnerOnly` (Owner) e `AdminOrOwner` (Admin ou Owner). `CurrentTenantRoleAuthorizationHandler.cs` chama `ICurrentTenantContext`, que compara role JWT com Membership atual. Assim, JWT emitido quando João era Owner **não** autoriza uma ação Owner após João virar Member: o contexto antigo falha; um refresh emite nova role Member. Não se usa cegamente `[Authorize(Roles="Owner")]` para privilégio por empresa.

Importante para a banca: as policies estão **implementadas e testadas**, mas os endpoints de Produtos/Estoque/Financeiro exigem autenticação/contexto e não aplicam hoje uma policy Owner/Admin; Member também opera essas funções. Não existe gerenciamento de usuários/empresa que use essas policies em uma rota real. Não confundir proteção de rota (`RequireAuthorization`) com hierarquia aplicada a todos os módulos.

Se a role de um token antigo divergir do banco, o contexto tenant inteiro fica indisponível: inclusive uma chamada operacional normal recebe 403 até obter um token atualizado via refresh ou novo login. Isso impede que uma role Owner antiga permaneça privilegiada e evita aceitar silenciosamente claims defasadas.

## 12. Frontend

Entrada: `src/frontend/index.html` prepara tema; `src/frontend/src/main.tsx` monta React, `QueryClientProvider` e `App.tsx`; `App.tsx` usa `BrowserRouter`, `AuthProvider`, gates e rotas aninhadas. `src/frontend/src/app/AppLayout.tsx` renderiza shell autenticado (sidebar, cabeçalho e `Outlet`); `Sidebar.tsx`, `UserMenu.tsx` e `navigation.ts` centralizam navegação. `features/*` contém páginas e `api.ts` por módulo; `lib/api.ts` concentra HTTP. `components/PageContent.tsx` reúne peças visuais compartilhadas.

```text
/login e /register       públicas (redirecionam autenticado)
/app                     protegida, renderiza AnalyticsPage na visão principal
/app/dashboard           visão principal AnalyticsPage
/app/products            ProductsPage
/app/inventory           InventoryPage
/app/finance             FinancePage
/app/analytics           AnalyticsPage detalhada
/app/settings            SettingsPage
```

React Router seleciona página sem recarregar a aplicação; guard de rota aguarda restauração e redireciona anônimo para login. Isso é **UX**, não autorização: a API exige Bearer/contexto. `ModulePage.tsx` é remanescente de placeholder e não é uma rota ativa. O backend não fornece HTML da SPA nesse ambiente de desenvolvimento; Vite serve frontend e a API fica em outro processo.

## 13. Estado de autenticação no frontend

`src/frontend/src/features/auth/session.ts` mantém access token **somente em variável de módulo, na memória da aba**. Isso reduz exposição a leitura persistente em `localStorage`, mas F5 o apaga. O refresh bruto permanece no cookie HttpOnly, invisível a JS. `AuthProvider.tsx` expõe `useAuth`, estados `restoring`, `anonymous`, `authenticated`, e carrega `/api/auth/me` como dado de servidor. Nenhuma senha/hash é persistida.

Ao apertar F5, React inicia em `restoring`; `AuthProvider` tenta uma vez `POST /api/auth/refresh` com cookie. Sucesso: novo access em memória, GET `/me`, sessão autenticada e rota protegida liberada. Falha: estado anonymous e `/app` redireciona `/login`, sem alerta assustador durante tentativa silenciosa. `session.ts` compartilha uma Promise de refresh na aba para que vários 401 próximos não disparem várias rotações; repete cada request original **uma vez**, evitando loop. O Bearer vai nas chamadas protegidas, enquanto `credentials: include` fica nas chamadas que usam cookie. Um marcador de geração evita aplicar resposta velha após logout/novo login. Em 401/403 irrecuperável, limpa sessão/cache. Logout usa endpoint existente, limpa memória e TanStack Query, navega para login; `sgf.logoutPending` em `sessionStorage` é apenas sinalizador não sensível para nova tentativa se logout remoto falhar.

Limites: memória é por aba; não há sincronização sofisticada entre abas, storage de access nem detecção de família de refresh. `src/frontend/src/lib/api.ts` centraliza base URL (`VITE_API_BASE_URL`, fallback local), timeout e `ApiError`, sem expor stack trace na UI.

## 14. TanStack Query

**Server state** são dados cuja autoridade é a API, como lista de produtos. TanStack Query armazena respostas por `queryKey`, acompanha loading/erro e evita fetches desnecessários; **mutation** altera servidor e, após sucesso, invalida as queries afetadas para buscar estado atualizado. Não armazena senha, token de refresh nem substitui state local de formulário/modal.

- Produtos (`features/products/ProductsPage.tsx`/`api.ts`): key inclui empresa, página, busca e status; cadastro/edição/status invalidam produtos e estoque.
- Estoque (`features/inventory/InventoryPage.tsx`): keys de saldo/histórico; entrada/saída invalidam listas e dados do produto.
- Financeiro (`features/finance/FinancePage.tsx`): lista e summary separados; criar/editar/pagar invalidam ambos.
- Dashboard/Analytics (`features/analytics/AnalyticsPage.tsx`): key inclui empresa e período; troca do período busca novo agregado.

O cache é limpo no logout para não mostrar dados de empresa anterior. Redux/Zustand não são necessários porque estado global de autenticação é pequeno (`AuthProvider`) e o restante é dado de servidor gerido por Query. Query **não** substitui filtro tenant no backend.

## 15. Produtos

`Product` guarda nome (até 200), SKU (até 64), descrição opcional (até 2000), custos/venda `numeric(12,2)`, status e estoque. `ProductService.cs` valida entradas, normaliza SKU (maiúsculas e remoção de espaços para evitar duplicação óbvia) e confia no índice único `(CompanyId, SKU)` como proteção final sob concorrência. Mesmo SKU em A e B é válido. Produto inativo permanece e reserva SKU: inativar não é deletar. Preços podem ser zero, não negativos; não há obrigação SalePrice > CostPrice.

Endpoints em `ProductEndpoints.cs`: GET lista (page default 1, pageSize 20 até 100, busca por nome/SKU e `isActive`), GET por Guid, POST, PUT por Guid e PATCH `/{id}/status`. Não há DELETE. Lista é ordenada por nome/ID; paginação reduz volume de resposta, mas `Count` e itens são consultas separadas, não snapshot transacional. 400 validação, 404 ID inexistente/de outra empresa, 409 SKU duplicado/conflito. DTO `SaveProductRequest` não possui CompanyId, timestamps ou CurrentStock; o mínimo pode ser alterado na edição. `ProductConfiguration.cs` mapeia FK/índices/precisão, e os mecanismos tenant fazem o resto.

`ProductsPage.tsx` mostra filtros/tabela responsiva, `ProductForm.tsx` cadastra/edita em diálogo, status pede confirmação e feedback. Fluxo: Novo Produto -> validação do formulário -> `POST /api/products` com Bearer -> `ProductService` + domínio + EF/PostgreSQL -> 201 -> mutation invalida query -> lista atualizada sem F5. Frontend formata BRL; API transporta número. Testes em `Sgf.Api.Tests/Products` e Playwright cobrem duplicação, tenant, validação, status e UX. Não há imagem, categoria ou unidade configurável.

## 16. Estoque

`Product.CurrentStock` e `MinimumStock` são `numeric(14,3)`, começam em zero. Três casas decimais permitem 0,250 e 10,5, sem múltiplas unidades ou conversão. `MinimumStock` é editável no produto; `CurrentStock` só muda por `Product.RecordMovement` chamado por `InventoryService.cs`. Isso evita saldo sem trilha histórica. Produto ativo com `CurrentStock <= MinimumStock` é baixo; zero também entra nessa contagem, inclusive se mínimo é zero. Não existe tabela de alertas.

`InventoryMovement` é evento Entry/Exit com quantidade positiva, produto, empresa, usuário, data e observação. Entrada adiciona; saída subtrai somente se houver saldo. Produto deve existir **no tenant** e estar ativo. Movimento e novo saldo são salvos em um `SaveChanges`, cuja transação nativa EF garante atomicidade: se gravar movimento falhar, alteração de saldo não permanece. `SgfDbContext` rejeita update/delete de movimento rastreado. Erro histórico deve ser compensado por novo movimento no futuro; não há correção automática hoje.

**Concorrência:** saldo 5, duas saídas de 4. Ambas podem ler 5. `CurrentStock` e `IsActive` são concurrency tokens (além de `CompanyId`); a primeira grava saldo 1. A segunda tenta `UPDATE Products SET CurrentStock=1 WHERE Id=@id AND CompanyId=@tenant AND CurrentStock=5 AND IsActive=true`; zero linhas afetadas causa `DbUpdateConcurrencyException`, operação inteira reverte e API retorna 409. Se ler saldo 1 depois, rejeita por estoque insuficiente, também sem movimento. Não se usa lock distribuído, fila ou retry automático de negócio. FK composta `(CompanyId, ProductId)` em `InventoryMovements` reforça que produto e movimento são da mesma empresa.

API (`InventoryEndpoints.cs`): GET `/api/inventory` (search, lowStock, paginação), GET `/api/inventory/movements` (produto, tipo Entry/Exit, paginação, mais recente primeiro), POST `/entries`, POST `/exits` com `{productId, quantity, notes?}`. 201 com movimento e saldo; 400 entrada inválida, 404 produto invisível/inexistente, 409 inativo/insuficiente/concorrência/limite. Não há endpoint de edição/exclusão de movimento. Na interface `InventoryPage.tsx`, abas Estoque/Movimentações, filtros em URL, formulários em `MovementForm.tsx`, saldo reconsultado, histórico e feedback; mobile adapta a lista. Movimentos físicos **não provam venda**. Testes PostgreSQL cobrem transação, tenant, imutabilidade, duas saídas concorrentes e saldo; Playwright cobre 10 - 3 = 7 e tentativa de 8. Veja `docs/11-estoque.md`.

## 17. Financeiro

`FinancialEntry` é uma só tabela para Income (receita) e Expense (despesa), Pending/Paid. `Amount` é sempre positivo `numeric(14,2)`; Type define o sinal. `Description` obrigatória até 200, `Category` texto opcional até 100, `Notes` até 2000, `DueDate` obrigatória, `PaidAt` só quando Paid. Novo lançamento é Pending; cliente não envia CompanyId, Status ou PaidAt. Categorias não têm tabela nem taxonomia controlada.

`PATCH /api/finance/{id}/pay` registra recebimento de Income ou pagamento de Expense integral no instante atual. Chamar de novo em Paid retorna estado coerente, sem duplicar valor. Não há reversão ou pagamento parcial. Edição via PUT é permitida somente enquanto Pending; `Version` Guid muda nas escritas e faz EF condicionar `UPDATE ... WHERE Id AND CompanyId AND VersionOriginal`. Conflito retorna 409; corrida entre dois pagamentos reconsulta estado confirmado para retornar Paid quando apropriado. Isso evita sobrescrita silenciosa, mas não detecta necessariamente formulário aberto por horas no cliente, pois Version não é exposta como controle de edição frontend.

**Saldo realizado** = soma de Income Paid - soma de Expense Paid. **A receber** = Income Pending; **a pagar** = Expense Pending. Pendências não entram no saldo realizado. `FinanceService.SummaryAsync` agrega no PostgreSQL **todo histórico** da empresa, sem obedecer filtros da listagem. Não é saldo bancário nem DRE. GET lista aceita busca por descrição, Type, Status, Category, intervalo inclusivo de DueDate e página; ordena DueDate crescente, CreatedAt decrescente e Id. Endpoints completos em `FinanceEndpoints.cs`: GET lista/ID/summary, POST, PUT, PATCH pay. `FinancePage.tsx` mostra cards e tabela/lista mobile, `FinanceForm.tsx` cria/edita; mutations invalidam summary/lista. Testes cobrem tenant, cálculo, filtros, idempotência e concorrência. Veja `docs/12-financeiro.md`.

## 18. Dashboard e analytics

`GET /api/dashboard?period=...` é **um endpoint agregado** (`DashboardEndpoints.cs`, `DashboardService.cs`, `AnalyticsContracts.cs`). O dashboard e a página Analytics reutilizam o mesmo contrato. `DashboardService` usa consultas SQL via EF (`COUNT`, `SUM`, `GROUP BY`, projeções) e uma transação `RepeatableRead` para o conjunto ter visão consistente; não carrega tabelas completas só para somar no React. Todos os dados de negócio passam por filtro tenant. Retorna `asOf`, período, indicadores financeiros, comparações, série, estoque, movimentos, listas e insights.

**Períodos:** `last30` = hoje e 29 dias anteriores; `currentMonth` = primeiro dia do mês até hoje; `previousMonth` = mês anterior completo; `last90` = hoje e 89 dias anteriores. “Hoje” usa **UTC-03 fixo**, não fuso horário configurável nem regras de horário de verão. Datas civis inclusivas são convertidas em intervalo UTC semiaberto `[início, dia seguinte ao fim)`. Comparação usa intervalo contíguo anterior de **mesma quantidade de dias**; não necessariamente “mês anterior” quando o atual está incompleto. Série agrupa por dia para até 31 dias, por mês acima disso, e preenche buckets sem registro com zero. `MetricComparison.Create`: `(atual-anterior)/anterior*100`, arredondado a uma casa; se anterior = 0, percentual `null`, não infinito/“+100%”.

| Indicador / origem | Fórmula e tempo | Significado, limite e decisão apoiada |
| --- | --- | --- |
| Recebidas / `FinancialEntries` | `SUM Amount` de Income/Paid com `PaidAt` no período. | Entrada registrada como realizada, não depósito bancário; acompanhar capacidade de gerar caixa. |
| Pagas / `FinancialEntries` | `SUM Amount` Expense/Paid por `PaidAt`. | Saídas financeiras registradas, não todos custos contábeis; controlar gastos. |
| Saldo realizado | Recebidas - Pagas no período. | Fluxo líquido registrado, sem saldo inicial/contas bancárias; perceber pressão de caixa. |
| A receber / A pagar | Soma Income/Expense **Pending atuais, sem filtro de período**. | Compromissos em aberto; priorizar cobrança e pagamentos. |
| Vencidos | Pending com `DueDate < hoje` (valores separados e contagem). | Atrasos atuais, independentemente do período selecionado; agir sobre contas urgentes. |
| Produtos ativos / `Products` | COUNT `IsActive`. | Tamanho do catálogo ativo; não mede giro. |
| Estoque baixo | COUNT ativo com `CurrentStock <= MinimumStock`. | Risco operacional; inclui zero e mínimo zero. |
| Estoque zerado | COUNT ativo com `CurrentStock == 0`. | Urgência de reposição; não prova demanda futura. |
| Valor estimado a custo | SUM `CurrentStock * CostPrice` de ativos. | Aproximação de capital imobilizado; **não** avaliação contábil oficial, custo médio ou valor de venda. |
| Entradas / Saídas / Contagem | SUM Quantity por tipo e COUNT de `InventoryMovements.CreatedAt` no período. | Volume físico, não receita/venda; acompanhar movimentação. |
| Maior saída | GROUP BY produto, SUM Quantity de Exit no período, top 10. | Priorizar investigação de itens com maior saída física; unidades heterogêneas limitam comparação. |
| Sem movimentação | Produto ativo criado há >=30 dias e sem movimento nos últimos 30 dias; top/lista e total. | Candidatos a revisão de estoque parado; janela fixa, não ajustada pelo filtro do dashboard. |
| Lista baixo estoque | Ativos `saldo <= mínimo`; saldo zero primeiro, depois diferença saldo-mínimo, nome e ID; top 10. | Ver item/SKU/saldo/mínimo para reposição. |

Indicadores de **estado atual** (estoque e pendências/vencidos) não mudam quando se seleciona período histórico; indicadores de **evento** (PaidAt, movimento CreatedAt) mudam. É uma distinção semântica central. A comparação de recebidas/pagas usa o período anterior; divergência pode gerar insight. Gráficos em `FinancialChart.tsx` (Recharts) visualizam série financeira; listas são usadas onde números e nomes importam mais. Empresa sem dados retorna zeros/listas vazias/percentuais nulos, sem NaN. `docs/13-dashboard-analytics.md` registra fórmulas e limitações acadêmicas.

## 19. Insights

`src/backend/Sgf.Application/Analytics/InsightRules.cs` é função determinística `Evaluate(finance, comparison, inventory, top)`: entradas agregadas -> lista ordenada de `AnalysisInsight(Code, Level, Message, Evidence)`. Não consulta IA, não treina modelo, não adivinha intenção; a mesma entrada gera a mesma regra. Ordem atual é a ordem em que a função acrescenta os itens (atenção antes de informação), sem motor de pontuação.

| Código / nível | Condição e evidência | Utilidade |
| --- | --- | --- |
| `zero_stock` / attention | `ZeroStockProducts > 0`; conta ativos com saldo zero. | Reposição urgente. |
| `low_stock` / attention | `LowStockProducts > 0`; saldo <= mínimo, **inclui** zerados. | Planejar reposição; pode se sobrepor ao anterior. |
| `overdue_payable` / attention | Valor de despesas Pending vencidas > 0. | Priorizar pagamentos e negociar atraso. |
| `overdue_receivable` / attention | Valor de receitas Pending vencidas > 0. | Priorizar cobrança. |
| `expense_increase` / attention | Variação percentual de despesas pagas >= 10%, base anterior positiva. | Investigar aumento, não concluir causa. |
| `income_decrease` / attention | Variação de receitas recebidas <= -10%, base anterior positiva. | Investigar queda, não diagnosticar sozinho. |
| `stale_products` / info | Produtos ativos sem movimento em 30 dias > 0 e criados há >=30 dias. | Revisar itens parados. |
| `top_stock_out` / info | Existe saída no período; cita produto e quantidade maior, com desempate nome/ID. | Inspecionar demanda física/consumo, **não** rotular venda. |

As mensagens vêm com `Evidence`, em português, e valores monetários formatados `pt-BR` para leitura. “Insight” aqui é uma explicação programada a partir de agregações confiáveis: **dados -> consultas -> indicadores -> regra -> alerta -> decisão da pessoa**. A regra não recomenda automaticamente compra, preço ou crédito. Testes em `Sgf.Application.Tests` cobrem condições/limites e testes do dashboard verificam a integração.

## 20. Curva ABC

Curva ABC normalmente ordena itens pela **participação acumulada em um valor** (por exemplo faturamento por produto ou custo consumido), separando grupos de maior/menor contribuição. O SGF não possui venda por produto: há saída física de estoque, que pode ser perda, uso interno ou outra operação. Quantidade de saída de um produto em kg não equivale economicamente à quantidade de outro em unidade; chamar isso “ABC de faturamento” seria incorreto. Por isso nenhuma Curva ABC foi implementada. Futuramente, com vendas registradas e valores vinculados a itens, pode-se definir métrica monetária, período, regra de classificação e testar interpretação. `docs/13-dashboard-analytics.md` registra o limite.

## 21. Settings e dark mode

`src/frontend/src/features/settings/SettingsPage.tsx` tem quatro seções: **Minha conta** (nome, e-mail, role via `useAuth`), **Empresa** (nome da empresa atual), **Aparência** (Claro/Escuro) e **Sessão** (mesmo `signOut` do menu). Conta e empresa são somente leitura; não há endpoint de edição nem status de Company no contrato `/me`, por isso a página não mostra status.

`src/frontend/src/app/theme.ts` usa `localStorage['sgf.theme']` para preferência não sensível. Sem preferência, `index.html` usa `prefers-color-scheme`. Um script no `<head>` aplica `data-theme`/colorScheme antes de montar React, reduzindo flash. CSS em `index.css`, `App.css` e nas features usa variáveis de cor para light/dark; gráficos usam cores adaptadas. A preferência persiste em navegação, F5 e logout, mas apenas **neste navegador**. O seletor expõe `radiogroup` e `aria-checked`. Tema não altera regra de negócio e não precisa de tabela.

## 22. Design e UX

`AppLayout.tsx` provê estrutura única das páginas autenticadas. `Sidebar.tsx` indica rota ativa; em telas estreitas vira menu móvel/drawer. `UserMenu.tsx` dá contexto de usuário/empresa e logout. `PageContent.tsx` reúne cabeçalho e estados compartilhados. Tipografia de sistema, CSS variables, superfícies de contraste, ícones Lucide, espaçamento regular e cores semânticas fazem interface B2B coerente sem biblioteca visual pesada. Cards são usados sobretudo para indicadores; tabelas desktop adaptam-se a listas mobile.

`ProductForm`, `MovementForm` e `FinanceForm` apresentam tarefa focal em diálogo, validações, envio pendente e feedback. Estados vazios diferenciam ausência de dados de busca sem resultado. A navegação não é mecanismo de segurança: ocultar algo no React jamais substitui autenticação/autorização da API. Playwright cobre 1440/390 px, navegação e temas; isso não equivale a certificação formal de acessibilidade.

## 23. API completa

**Legenda:** Pública = sem Bearer; Protegida = JWT + contexto tenant revalidado. Contratos em `Sgf.Application/*/*Contracts.cs`, rotas em `src/backend/Sgf.Api/Program.cs` e `*Endpoints.cs`. Erros de negócio são status/ProblemDetails sanitizados: 400 entrada, 401 credencial, 403 contexto inválido, 404 recurso invisível, 409 duplicidade/conflito. `CompanyId` não faz parte de DTO de criação operacional.

| Método e rota | Acesso | Request essencial | Resposta/finalidade | Erros importantes |
| --- | --- | --- | --- | --- |
| GET `/api/health` | Pública | Nenhum | 200 status do processo. | Falha operacional. |
| GET `/api/health/database` | Pública | Nenhum | 200 após abrir/fechar conexão. | 503 banco indisponível. |
| POST `/api/auth/register` | Pública | `name,email,password,companyName` | 201; cria User/Company/Membership Owner. | 400, 409 e-mail, 500 persistência. |
| POST `/api/auth/login` | Pública | `email,password` | 200 `accessToken,tokenType,expiresAt,userId,companyId,role` e cookie. | 401 genérico, 403 sem empresa, 409 seleção. |
| POST `/api/auth/refresh` | Pública (cookie) | Cookie HttpOnly | 200 novo access + novo cookie. | 401 ausente/inválido/revogado. |
| POST `/api/auth/logout` | Pública (cookie) | Cookie | 204; revoga quando possível e apaga cookie. | Falha operacional. |
| GET `/api/auth/me` | Protegida | Bearer | 200 `userId,name,email,company:{id,name},role`. | 401 JWT, 403 vínculo/empresa/role. |
| GET `/api/products` | Protegida, tenant | `page,pageSize,search,isActive` | 200 lista paginada. | 400 filtros, 403 contexto. |
| GET `/api/products/{id}` | Protegida, tenant | Guid | 200 produto. | 404 outro tenant/inexistente. |
| POST `/api/products` | Protegida, tenant | `name,sku,description?,costPrice,salePrice,minimumStock?` | 201 produto/Location. | 400, 409 SKU. |
| PUT `/api/products/{id}` | Protegida, tenant | Campos editáveis, sem saldo/CompanyId | 200 produto. | 400, 404, 409. |
| PATCH `/api/products/{id}/status` | Protegida, tenant | `isActive` | 200 ativo/inativo. | 404, 409. |
| GET `/api/inventory` | Protegida, tenant | `page,pageSize,search,lowStock` | 200 saldos/mínimos paginados. | 400, 403. |
| GET `/api/inventory/movements` | Protegida, tenant | `page,pageSize,productId?,type?` | 200 histórico recente. | 400, 404 produto filtrado. |
| POST `/api/inventory/entries` | Protegida, tenant | `productId,quantity,notes?` | 201 Entry/novo saldo. | 400, 404, 409 inativo/limite/conflito. |
| POST `/api/inventory/exits` | Protegida, tenant | Mesmo request | 201 Exit/novo saldo. | 400, 404, 409 insuficiente/conflito. |
| GET `/api/finance` | Protegida, tenant | `page,pageSize,search,type,status,category,from,to` | 200 lista paginada. | 400, 403. |
| GET `/api/finance/summary` | Protegida, tenant | Nenhum | 200 `received,paid,receivable,payable,balance`. | 403/500. |
| GET `/api/finance/{id}` | Protegida, tenant | Guid | 200 lançamento. | 404. |
| POST `/api/finance` | Protegida, tenant | `type,description,category?,amount,dueDate,notes?` | 201 Pending/Location. | 400. |
| PUT `/api/finance/{id}` | Protegida, tenant | Campos editáveis, sem Type/Status | 200 se Pending. | 400, 404, 409 Paid/conflito. |
| PATCH `/api/finance/{id}/pay` | Protegida, tenant | Sem body | 200 Paid, idempotente. | 404, 409. |
| GET `/api/dashboard` | Protegida, tenant | `period=last30|currentMonth|previousMonth|last90` | 200 agregado de dashboard/analytics. | 400 período, 403. |

**Não existem** DELETE de produtos/movimentos/financeiro, GET `/api/analytics/inventory`, GET `/api/products/{id}/movements` ou endpoint de company/tema. O mesmo pay recebe receita ou paga despesa conforme Type. Todos os endpoints de negócio usam `RequireAuthorization()`; as duas rotas de health são públicas.

## 24. Testes

Estratégia por risco: função pura -> unitário; persistência/concorrência/tenant -> PostgreSQL temporário real; HTTP/JWT -> `WebApplicationFactory`; navegador/cookies/F5/tema -> Playwright. EF InMemory não reproduziria checks SQL, migrations ou concorrência PostgreSQL. Factories aplicam migrations e removem bancos temporários. Testes não criam entidade artificial de produção.

**Contagem verificada durante este guia:** `dotnet test src/backend/Sgf.sln --no-restore` com SDK local 10: **11 Application, 33 Infrastructure, 116 API = 160 backend aprovados, 0 falhas**. `playwright test --list`: **20 testes em 7 arquivos**. A listagem não é execução de Playwright nesta tarefa. O `dotnet` global desta máquina era SDK 9; `.dotnet/dotnet.exe` local executou o teste sem mudar código. Contagens antigas em documentos de fases anteriores não são o total atual.

**Divergências históricas encontradas:** `docs/03-arquitetura.md` ainda apresenta parte da estrutura como “esperada” e campos genéricos `CreatedByUserId`/`DeletedAt` como sugestões, não como schema implementado. `docs/04-modelo-de-dominio.md` abre com “modelo inicial conceitual” e contém Supplier/ProductCategory/UnitOfMeasure futuros; não há classes/tabelas desses módulos. `docs/02-requisitos.md` inclui exemplos de insights (como despesa acima da média) que não são regras em `InsightRules.cs`. `docs/07-validacao-fase-f.md` e `docs/10-produtos.md` registram contagens e limites de suas fases, não o estado final. Este guia usou código/migrations/testes atuais como autoridade; os documentos históricos não foram reescritos.

Exemplos de cobertura real: rollback após falha de Company/Membership; JWT adulterado/expirado; Membership/Company inativos; rotação/reuso/concorrência de refresh; `Attach/Update/Remove` cross-tenant; SKU único por empresa; estoque e movimento atômicos, duas saídas simultâneas; pagamento idempotente/concorrente; dashboard sem misturar empresas; F5 e dark mode no navegador. Veja `Sgf.Application.Tests`, `Sgf.Infrastructure.Tests`, `Sgf.Api.Tests` e `src/frontend/tests/*.spec.ts`. Número de testes não substitui pertinência das asserções.

## 25. Concorrência

Concorrência ocorre quando requisições leem estado similar e tentam gravar. **Optimistic concurrency** compara valor original ao persistido no momento do UPDATE, sem travar antecipadamente toda operação. `IsConcurrencyToken()` adiciona predicado ao WHERE; zero linhas afetadas gera `DbUpdateConcurrencyException`.

| Área | Token/predicado | Exemplo |
| --- | --- | --- |
| Multi-tenant | `CompanyId` em `ICompanyScopedEntity`: WHERE Id e CompanyId original. | Objeto anexado de A com ID real B afeta zero linha. |
| Refresh | `RevokedAt` original. | Dois refreshes do mesmo cookie: um ganha, outro 401. |
| Estoque | `CurrentStock`, `IsActive`, `CompanyId`. | Duas saídas de 4 a partir de 5: segunda 409 ou insuficiente, nunca -3. |
| Financeiro | `Version` Guid renovado, `CompanyId`. | Editar/pagar concorrentemente não se sobrescreve; conflito ou Paid reconsultado. |

Índices únicos também resolvem corridas de “vi que não existe, então crio”: SKU por empresa, Membership por User+Company e hash refresh. `SaveChanges` transacional evita meia movimentação. Não há fila, lock distribuído ou idempotency key genérica; após timeout de POST, confira lista antes de repetir.

## 26. Segurança

**Implementada:** Identity faz hash/verificação; JWT assinado valida issuer/audience/assinatura/expiração; chave obrigatória fora do código; refresh aleatório por hash, cookie HttpOnly/SameSite/Secure fora de dev/test, rotação; CORS com origens explícitas e Origin guard para auth POST; Bearer nos módulos; revalidação de Membership/Company/Role; leitura filtrada e escrita fail-closed por CompanyId; checks/FKs/índices; validações no backend; consultas LINQ EF parametrizadas reduzem risco de SQL injection; respostas de erro sanitizadas. Parametrização não torna SQL bruto arbitrário seguro. O seed usa SQL direto **somente local** para datas de IDs recém-criados.

**Ainda necessário para produção:** rate limiting, lockout/política de abuso, confirmação/recuperação de e-mail, HTTPS/proxy e segredo gerenciado/rotacionado, revisão de domínios/cookies/CSRF, CSP/cabeçalhos, logs/métricas/alertas, backup e restauração testados, retenção de refresh, CI/CD, carga/teste de invasão e LGPD. `docs/14-divida-tecnica-e-trabalhos-futuros.md` classifica. Apresente como protótipo acadêmico robusto, não serviço público pronto.

## 27. Docker e execução

`docker-compose.yml` sobe apenas `postgres:17-alpine`, com volume e `pg_isready`. O bind `127.0.0.1:15432:5432` expõe banco somente nesta máquina. API local usa porta 15432 via connection string de Development. `/api/health` confirma processo; `/api/health/database` abre/fecha conexão, não mede regras de negócio. `global.json` exige SDK 10.0.400 com `rollForward: latestFeature`.

Sequência canônica no `README.md`: copiar `.env.example` para `.env`; definir `ASPNETCORE_ENVIRONMENT=Development` e `Jwt__SigningKey` **no terminal da API**; `docker compose up -d --wait`; `dotnet restore src/backend/Sgf.sln`; `dotnet tool restore`; `dotnet ef database update --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api`; `dotnet run --project src/backend/Sgf.Api --launch-profile http`; em outro terminal `cd src/frontend`, `npm ci`, `npm run dev -- --host localhost --port 5173 --strictPort`. Frontend em 5173, API em 5206. `docker compose down -v` **apaga dados locais**; não é comando de parada cotidiana.

## 28. Configuração

ASP.NET Core combina `appsettings.json`, `appsettings.Development.json` e ambiente; `Jwt__SigningKey` mapeia `Jwt:SigningKey`. `Program.cs` valida chave >=32 bytes, issuer/audience, duração e CORS no startup; `DependencyInjection.cs` configura Identity/EF. `.env.example` versionado orienta Docker; `.env` ignorado **não é carregado automaticamente pela API**. `appsettings.Development.json` contém credencial local de exemplo, não senha de produção. Signing Key real deve vir de ambiente/secret manager. Inclusive Testing exige chave explícita; factories fornecem chave de teste compartilhada para emissão/validação. `src/frontend/.env.example` mostra `VITE_API_BASE_URL`; essa URL é pública no bundle, não secret. Produção requer conexão, chave, HTTPS, cookies e CORS próprios.

## 29. Seed de demonstração

`scripts/seed-demo.ps1` é **manual e local**: valida API HTTP localhost/127.0.0.1 e health DB, pede senha `Read-Host -AsSecureString` ou aceita `SGF_DEMO_PASSWORD` temporária, cria `demo@sgf.local` e **Mercado Exemplo LTDA** via API, faz login, cadastra 11 produtos (10 ativos, um inativo), entradas/saídas e 12 lançamentos financeiros. Depois usa `docker exec ... psql` para **mudar somente datas de IDs recém-criados** e criar períodos comparáveis/item parado. É exceção controlada que ignora as regras de escrita EF: jamais rodar contra produção.

O cenário tem quatro produtos ativos de estoque baixo, incluindo um zerado; nos últimos 30 dias previstos após carga, recebidas R$ 12.500,00, pagas R$ 5.830,00, saldo R$ 6.670,00, além de pendentes/vencidos e dados anteriores. Como datas são relativas à execução, confira números antes da banca. Script espera banco **limpo** e não é idempotente; repetição falha no cadastro. A senha não é fixa no repositório. `docs/15-roteiro-demonstracao.md` ensina apresentação. Lançamentos narrados como “vendas” são registros financeiros demo, **não** um módulo de vendas nem vínculo automático com saídas.

## 30. Fluxos end-to-end

Nos diagramas, contratos ficam em Application e implementações concretas geralmente em Infrastructure. Após mutation bem-sucedida, TanStack Query invalida a lista/summary relevante.

1. **Cadastro:** `AuthPages` -> `session.register` -> POST register -> `RegisterCompanyOwnerService` -> Identity/UserManager + Company + Membership Owner -> transação PostgreSQL -> 201 -> /login.
2. **Login:** formulário -> POST login -> `LoginService`/Identity -> vínculo único -> `AccessTokenFactory` + refresh hash -> DB -> JSON access/Set-Cookie -> `AuthProvider` GET /me -> /app.
3. **Refresh:** 401 ou F5 -> `session.refreshSession` POST refresh com cookie -> hash/revalidação/rotação em `RefreshTokenService` -> DB -> novo access/cookie -> request repetido uma vez ou /me.
4. **Logout:** menu/Settings -> `signOut` -> POST logout -> revogação no DB -> cookie expirado/204 -> memória/cache limpos -> /login.
5. **Criar produto:** `ProductForm` -> POST products -> `ProductService.CreateAsync` -> `Product` valida/normaliza -> EF define CompanyId -> DB/índice SKU -> 201 -> query invalidada -> produto aparece.
6. **Editar produto:** formulário -> PUT products/{id} -> serviço busca sob filtro tenant -> `Product.Update` -> DB com CompanyId no WHERE -> 200 -> lista recarregada; CurrentStock não está no request.
7. **Entrada:** `MovementForm` -> POST inventory/entries -> serviço busca produto ativo -> `RecordMovement` soma -> Product + InventoryMovement em um SaveChanges -> 201 -> saldo/histórico reconsultados.
8. **Saída:** formulário -> POST inventory/exits -> valida saldo -> `RecordMovement` subtrai -> UPDATE condicional + INSERT atômicos -> 201 ou 409 sem movimento -> queries atualizadas após sucesso.
9. **Criar receita:** `FinanceForm` Income -> POST finance -> `FinanceService.CreateAsync` -> FinancialEntry Pending -> DB -> 201 -> lista/summary invalidados; a receber sobe, saldo realizado não.
10. **Pagar despesa:** tabela -> PATCH finance/{id}/pay -> `FinanceService.PayAsync` -> Paid, PaidAt atual e Version nova -> DB -> 200 -> summary reconsultado; pago sobe, saldo cai.
11. **Dashboard:** `AnalyticsPage` -> GET dashboard?period=last30 Bearer -> contexto tenant -> `DashboardService` consultas EF/SQL -> `InsightRules.Evaluate` -> JSON agregado -> Query -> cards/gráfico/listas.
12. **F5:** access em memória some -> tema aplica antes de React -> `AuthProvider` restoring -> cookie vai em refresh -> novo access -> GET /me -> rota liberada -> queries carregam.
13. **Tema:** Settings `chooseTheme` -> `applyTheme` -> `data-theme`/CSS mudam -> `localStorage['sgf.theme']` -> F5 e logout preservam; nenhum request API.

## 31. Decisões arquiteturais

Os seis registros estão em `docs/adr`. ADR significa *Architecture Decision Record*: contexto, opções, escolha e consequências, não um comando executável. Leia junto ao código citado.

| ADR | Problema, alternativas e escolha | Benefício / custo / implementação |
| --- | --- | --- |
| `001-user-membership-company.md` | User com CompanyId direto seria simples mas prenderia pessoa a uma empresa. Escolha: User -> Membership -> Company. | Papel por empresa e potencial multiempresa; exige consulta/seleção cuidadosa. `Membership.cs`, índice único e `LoginService`; switch-company ainda não existe. |
| `002-register-transaction.md` | User, Company e Owner podiam ficar incompletos. Escolha: uma transação EF/PostgreSQL incluindo Identity. | Consistência; caso de uso coordena persistência. `RegisterCompanyOwnerService`; rollback testado. |
| `003-access-token-jwt.md` | Cliente precisa identificar-se sem reenviar senha. Escolha: JWT assinado curto com sub/company_id/role/jti/iat. | API pode validar identidade; token já emitido não é revogado centralmente. `AccessTokenFactory` e `Program.cs`; refresh foi agregado depois pelo ADR 006. |
| `004-current-tenant-context.md` | Ler claims em cada endpoint ou aceitar CompanyId cliente aumentaria risco. Escolha: `ICurrentTenantContext` scoped e revalidação sob demanda. | Camada Application sem HTTP e role/empresa atuais; consulta DB por request necessário. `CurrentTenantContext.cs`. |
| `005-company-scoped-data-isolation.md` | Banco compartilhado ameaça vazamento entre tenants. Escolha: interface `ICompanyScopedEntity`, filtros globais, proteção de SaveChanges, fail-closed. | Proteção central de leitura/escrita; `IgnoreQueryFilters` e SQL bulk precisam revisão. F.1 adicionou CompanyId como concurrency token para objeto anexado. |
| `006-refresh-token-and-authorization.md` | JWT curto precisa renovação; role JWT pode envelhecer. Escolha: refresh opaco hash/rotação/cookie e policies consultando contexto atual. | Melhor UX e privilégio atual; persistência de sessão, corrida entre abas e revogação não imediata do access. `RefreshTokenService`, `CurrentTenantRoleAuthorizationHandler`. |

Uma alternativa pode ser tecnicamente possível sem ser adequada agora. Rejeitar microserviços/Redis/CQRS não é dizer que são ruins universalmente; é dizer que suas vantagens não pagam custo no escopo e equipe deste TCC.

## 32. O que não foi implementado

- **Fornecedores, compras, vendas e NF-e:** não há entidades, endpoints ou integração; saída física não é venda e lançamento financeiro não é nota fiscal. Exigiriam modelagem de pedido, itens, preços históricos e regras fiscais.
- **Integração bancária, PIX, boletos, conciliação, contas múltiplas e DRE:** financeiro é registro manual simples e saldo realizado, não razão contábil nem extrato.
- **Parcelamento, recorrência, juros, pagamento parcial e reversão:** FinancialEntry só alterna Pending -> Paid integral; mais estados/fluxos aumentariam escopo e invariantes.
- **Depósitos, lotes, validade, serial, reserva, inventário, custo médio/FIFO:** Product tem um saldo agregado e histórico Entry/Exit, suficiente para demonstrar controle básico e concorrência.
- **Switch-company, convites, gestão de membros/papéis:** modelo suporta vínculos múltiplos, mas login com >1 não escolhe empresa e retorna 409. Um fluxo seguro de seleção teria de ser implementado separadamente.
- **Recuperação/confirmacão de e-mail, MFA, login social:** identidade básica existe, mas essas jornadas exigiriam entrega de e-mail, recuperação segura e operação.
- **Assinaturas/billing SaaS, deploy público, alta disponibilidade e backup:** SaaS está na modelagem multi-tenant, não no serviço comercial publicado.
- **Curva ABC por faturamento, ML/IA generativa e previsões:** faltam vendas por item e histórico/qualidade de dados para inferência adequada; insights atuais são regras.
- **Categorias avançadas, unidade de medida configurável, códigos de barra, fotos, importação:** valor menor para o objetivo acadêmico central.
- **App mobile nativo, microsserviços, filas, Redis, Kubernetes, data warehouse:** não há necessidade concreta no volume/escopo atual.

Ver também `docs/14-divida-tecnica-e-trabalhos-futuros.md`; “fora do escopo” não significa impossível nem implementado parcialmente por existir um nome conceitual em `docs/04-modelo-de-dominio.md`.

## 33. Dívida técnica

O documento-fonte é `docs/14-divida-tecnica-e-trabalhos-futuros.md`. Dívida **antes de publicar** é risco operacional/segurança, não item cosmético: rate limiting e lockout mitigam força bruta; segredo gerenciado/HTTPS/proxy protegem credenciais e cookies; CORS/AllowedHosts/CSRF/cabeçalhos precisam coincidir com domínios reais; logs/métricas/alertas detectam incidente; backups e teste de restauração evitam perda de dados; limpeza/retenção de refresh controla volume e exposição; CI/deploy/migration rollback reduzem erro humano; LGPD e política de privacidade são requisitos de tratamento de dados; testes de carga/segurança verificam hipóteses.

**Melhorias futuras** são produto e ergonomia: switch-company, membros, fornecedores, vendas/compras, estoques mais complexos, parcelas, importação, ABC após vendas, eventual previsão. Não precisam ser implementadas para demonstrar o TCC. **Riscos residuais**: access JWT até expiração; refresh em abas competindo; paginação offset instável sob muitas escritas; estimativa a custo não contábil; saídas não equivalem a vendas; sem snapshot histórico de estoque. Assuma-os com honestidade e diga qual proteção existe e qual falta.

## 34. Como publicar futuramente

Sem escolher fornecedor: build estático React em hospedagem web; ASP.NET Core em servidor/container atrás de proxy HTTPS; PostgreSQL persistente/gerenciado em rede privada; connection string, JWT key e credenciais em gerenciador de secrets; CORS/domínios/cookies configurados para o site real; migrations executadas em processo controlado e com backup; CI testa/builda antes de deploy; logs estruturados, métricas, alertas e backups testados. Somar rate limiting, recuperação de conta, revisão LGPD, carga e teste de segurança. Se frontend/API estiverem em **sites** diferentes, a decisão SameSite=Lax deixa de servir para refresh: redesenhar cookie e proteção CSRF antes do deploy. `README.md` descreve desenvolvimento local, não runbook de produção. Evite confundir “arquitetado próximo de publicação” com “pode apontar DNS e publicar hoje”.

## 35. Perguntas da banca

Estas 55 perguntas oferecem uma resposta oral breve e uma expansão para quando o avaliador aprofundar. Todas correspondem ao código atual; limitações são declaradas.

### Arquitetura

**1. O que significa monólito modular no SGF?**

- **Resposta curta:** Uma API implantada como unidade única, organizada em camadas e áreas de negócio.
- **Resposta detalhada:** Sgf.Api compõe serviços de identidade, produtos, estoque, financeiro e analytics; Domain, Application e Infrastructure separam responsabilidades. Não há comunicação de rede entre módulos internos. Veja src/backend/Sgf.sln e Program.cs.

**2. Por que não microserviços?**

- **Resposta curta:** Não existe necessidade real de deploy/escala independente.
- **Resposta detalhada:** Eles trariam chamadas remotas, consistência distribuída e observabilidade para uma equipe/escopo que não os exige. O monólito mantém limites internos sem custo operacional extra.

**3. Qual é o sentido da camada Domain?**

- **Resposta curta:** Guardar entidades e invariantes do negócio.
- **Resposta detalhada:** Product impede preços negativos; FinancialEntry governa transição Pending/Paid; InventoryMovement representa um evento. Domain não depende de EF nem ASP.NET.

**4. Por que Application não conhece HttpContext?**

- **Resposta curta:** Para que o caso de uso não dependa do protocolo HTTP.
- **Resposta detalhada:** ICurrentTenantContext e interfaces de serviço são contratos; a API resolve identidade e Infrastructure implementa persistência. Isso permite testar regras sem simular um controller.

**5. Por que não repository genérico ou MediatR?**

- **Resposta curta:** DbContext e serviços atuais bastam.
- **Resposta detalhada:** DbContext já consulta, rastreia e salva; serviços concretos coordenam operações. Um barramento ou CRUD genérico adicionaria indireção sem regra nova.

### Banco

**6. Quantas migrations existem?**

- **Resposta curta:** Cinco migrations de evolução do schema.
- **Resposta detalhada:** InitialIdentityAndCompanies, AddRefreshTokens, AddProducts, AddInventory e AddFinancialEntries, em src/backend/Sgf.Infrastructure/Database/Migrations. O ModelSnapshot não é uma sexta migration.

**7. Por que SKU é único por CompanyId?**

- **Resposta curta:** Cada empresa organiza seu próprio catálogo.
- **Resposta detalhada:** O índice único (CompanyId, SKU) impede duplicação na mesma empresa e permite o mesmo código em outra. É proteção final inclusive sob cadastros simultâneos.

**8. Por que Guid para entidades e string para usuário?**

- **Resposta curta:** Entidades de negócio usam UUID; Identity usa seu tipo padrão string.
- **Resposta detalhada:** Essa diferença é explicitada por Membership.UserId string e CompanyId Guid. UUID não substitui checagem de autorização nem filtro por empresa.

**9. Por que DueDate é date e PaidAt é instante UTC?**

- **Resposta curta:** Vencimento é dia civil; pagamento é evento em um instante.
- **Resposta detalhada:** DateOnly/date evita ambiguidade horária no vencimento. PaidAt/DateTimeOffset serve para período de realização e série temporal.

**10. Para que servem constraints além da validação C#?**

- **Resposta curta:** Protegem o banco como última barreira.
- **Resposta detalhada:** Checks impedem estados financeiros inconsistentes e estoque negativo; FK composta impede movimento ligado a produto de outra empresa. Outros processos não deveriam depender apenas da UI.

### Segurança

**11. Como A é impedida de ler produto B?**

- **Resposta curta:** Filtro global por CompanyId derivado da sessão validada.
- **Resposta detalhada:** CurrentTenantContext revalida vínculo; EF adiciona predicado tenant nas queries de Product. Buscar ID de B em sessão A resulta 404, não exposição.

**12. Por que filtro global não basta para escrita?**

- **Resposta curta:** Objeto anexado pode não ter sido lido sob o filtro.
- **Resposta detalhada:** CompanyId é concurrency token e entra no WHERE do UPDATE/DELETE, incluindo Attach/Update/Remove. ID de B com CompanyId A afeta zero linhas.

**13. Por que senha não aparece no código de hash?**

- **Resposta curta:** ASP.NET Core Identity administra hash/verificação.
- **Resposta detalhada:** UserManager cria usuário e CheckPasswordAsync verifica; PasswordHash fica no banco, nunca na resposta. Implementação manual seria risco.

**14. JWT é criptografado?**

- **Resposta curta:** Não; é assinado.
- **Resposta detalhada:** Claims podem ser lidas por quem tem o token, por isso não incluem segredo. HMAC-SHA256 detecta alteração; HTTPS protege trânsito em publicação.

**15. O que falta antes de publicação pública?**

- **Resposta curta:** Rate limiting, HTTPS, secrets, operação e revisão de segurança.
- **Resposta detalhada:** O backend já tem várias defesas, mas não protege sozinho contra força bruta nem fornece backup/observabilidade. Consulte docs/14-divida-tecnica-e-trabalhos-futuros.md.

### SaaS e tenant

**16. O que é tenant no SGF?**

- **Resposta curta:** Uma Company que possui seus dados operacionais.
- **Resposta detalhada:** Todas as empresas compartilham a implantação e o banco, mas Product, InventoryMovement e FinancialEntry carregam CompanyId para isolamento.

**17. Por que banco compartilhado?**

- **Resposta curta:** Menor custo e operação mais simples para o TCC.
- **Resposta detalhada:** O trade-off é exigir filtros, constraints e testes rigorosos contra vazamento. Banco por empresa aumentaria provisão, migrations e operação.

**18. De onde vem a Company atual?**

- **Resposta curta:** Do JWT válido, revalidada no Membership e Company do banco.
- **Resposta detalhada:** O cliente não envia um CompanyId livre como autoridade. CurrentTenantContext é request-scoped e memoiza resolução.

**19. Um usuário pode ter duas empresas hoje?**

- **Resposta curta:** Modelo permite; login ainda não seleciona entre elas.
- **Resposta detalhada:** Membership suporta múltiplos vínculos/roles, mas com 2 ou mais elegíveis login retorna company_selection_required. Switch-company ficou para futuro.

**20. O que significa fail-closed?**

- **Resposta curta:** Sem contexto tenant válido não se abrem dados operacionais.
- **Resposta detalhada:** O filtro exige CurrentCompanyId; sem ele não retorna linhas, e SaveChanges de entidade tenant-scoped rejeita escrita. Health/register/login não precisam desse contexto.

### Frontend

**21. Onde fica o access token?**

- **Resposta curta:** Em memória na aba.
- **Resposta detalhada:** session.ts não grava access em localStorage. F5 apaga a memória e a sessão é restaurada com cookie refresh HttpOnly.

**22. Como o refresh cookie chega à API?**

- **Resposta curta:** O browser o envia em requests com credentials: include.
- **Resposta detalhada:** Login/refresh/logout usam essa opção; JavaScript não lê o valor HttpOnly. CORS restringe origens permitidas.

**23. Por que TanStack Query em vez de Redux?**

- **Resposta curta:** Os dados dominantes são respostas da API.
- **Resposta detalhada:** Query cuida de cache, loading e invalidação; AuthProvider contém o pequeno estado de sessão. Outra store geral não traria benefício claro.

**24. O que acontece se duas requisições recebem 401?**

- **Resposta curta:** Compartilham uma Promise de refresh na aba.
- **Resposta detalhada:** session.ts evita duas rotações simultâneas locais e repete cada request uma vez. Outra aba ainda pode competir; isso é limitação conhecida.

**25. Rota protegida no React é segurança?**

- **Resposta curta:** Não, é navegação/UX.
- **Resposta detalhada:** Gate evita tela privada sem sessão no navegador, mas só a API com Bearer, contexto tenant e filtros aplica segurança real.

### Backend

**26. Qual endpoint cria usuário e empresa?**

- **Resposta curta:** POST /api/auth/register.
- **Resposta detalhada:** Program.cs chama RegisterCompanyOwnerService, que executa Identity e grava Company/Membership Owner numa transação. Não emite sessão nesse passo.

**27. O que são Minimal APIs aqui?**

- **Resposta curta:** Mapeamento direto de rotas HTTP a handlers.
- **Resposta detalhada:** Program.cs e arquivos *Endpoints.cs traduzem request/resultado sem colocar regras extensas no handler. Serviços em Infrastructure executam operação.

**28. Por que 401 e 403 são diferentes?**

- **Resposta curta:** 401 pede credencial válida; 403 recusa acesso autenticado.
- **Resposta detalhada:** Sem/expirado/adulterado JWT -> 401. JWT válido com Membership/Company/role não mais válidos -> 403 no contexto tenant.

**29. Por que /me consulta o banco?**

- **Resposta curta:** Claims assinadas podem estar desatualizadas.
- **Resposta detalhada:** CurrentTenantContext verifica existência, atividade e role atual. Assim token antigo não ignora desativação da empresa/vínculo.

**30. Como erro interno é exposto?**

- **Resposta curta:** Mensagem genérica sem stack/SQL.
- **Resposta detalhada:** Endpoints traduzem validação/conflito em ProblemDetails com códigos públicos. Falha de banco inesperada não entrega detalhes ao usuário.

### Estoque

**31. Por que CurrentStock não pode ser editado no produto?**

- **Resposta curta:** Saldo deve sempre corresponder a uma movimentação.
- **Resposta detalhada:** DTO de produto não inclui CurrentStock; InventoryService usa Product.RecordMovement e grava movimento no mesmo SaveChanges. Isso preserva trilha operacional.

**32. Como duas saídas de 4 não deixam saldo 5 negativo?**

- **Resposta curta:** Concorrência otimista rejeita uma atualização.
- **Resposta detalhada:** WHERE inclui CurrentStock original 5; após primeira gravar 1, segunda afeta zero linhas e falha 409, sem movimento.

**33. O histórico pode ser alterado?**

- **Resposta curta:** Não pela API nem por SaveChanges rastreado.
- **Resposta detalhada:** InventoryMovement é tratado como evento imutável; correção seria compensação futura. SQL administrativo direto é exceção e não passa pela proteção.

**34. O que significa estoque baixo?**

- **Resposta curta:** Ativo e saldo atual menor ou igual ao mínimo.
- **Resposta detalhada:** Produto zerado também é baixo. Essa é condição calculada, não tabela de alertas, em InventoryService e DashboardService.

**35. Saída de estoque é venda?**

- **Resposta curta:** Não necessariamente.
- **Resposta detalhada:** É movimento físico manual sem pedido, cliente, preço da transação ou faturamento por item. Por isso analytics a chama maior saída, não mais vendido.

### Financeiro

**36. Como é calculado saldo realizado?**

- **Resposta curta:** Receitas Paid menos despesas Paid.
- **Resposta detalhada:** FinanceService.SummaryAsync agrega todos os registros da empresa; Pending fica em a receber/a pagar. Não é saldo bancário.

**37. Por que Amount nunca é negativo?**

- **Resposta curta:** Type define o sinal econômico.
- **Resposta detalhada:** Income e Expense têm valores positivos; subtrair despesas no cálculo evita dupla semântica de sinais e facilita validação.

**38. Pode editar lançamento pago?**

- **Resposta curta:** Não pela API atual.
- **Resposta detalhada:** FinancialEntry.Update só aceita Pending para preservar histórico simples. Também não há reversão ou pagamento parcial.

**39. Pagar duas vezes duplica valor?**

- **Resposta curta:** Não; pay é idempotente para o mesmo lançamento.
- **Resposta detalhada:** PaidAt/status são gravados uma vez; chamada repetida retorna estado Paid. Version protege corrida entre pagamentos e edições.

**40. O summary respeita filtros da tabela?**

- **Resposta curta:** Não; resume todo histórico da empresa.
- **Resposta detalhada:** Busca/período/Status afetam listagem em FinancePage, enquanto cards de summary usam agregado independente. Dashboard tem semântica de período própria.

### Analytics

**41. Qual data determina receitas recebidas no período?**

- **Resposta curta:** PaidAt, não DueDate.
- **Resposta detalhada:** DashboardService soma Income/Paid pelo instante de realização. DueDate é usado para vencidos Pending.

**42. Por que pendentes não mudam com período?**

- **Resposta curta:** São situação atual.
- **Resposta detalhada:** A receber/a pagar e vencidos refletem obrigações abertas hoje; período selecionado afeta realizados e movimentações, não o estado atual.

**43. O que acontece quando período anterior é zero?**

- **Resposta curta:** Percentual é null, sem comparação numérica.
- **Resposta detalhada:** MetricComparison.Create evita divisão por zero e uma taxa enganosa. UI pode exibir ausência de comparação.

**44. Insights são IA?**

- **Resposta curta:** Não; são regras determinísticas.
- **Resposta detalhada:** InsightRules.Evaluate aplica condições explícitas, níveis e evidências a indicadores. Sem treinamento, geração probabilística ou previsão.

**45. Por que valor de estoque é só estimado?**

- **Resposta curta:** Multiplica saldo atual pelo custo cadastrado.
- **Resposta detalhada:** Não há compras, custo histórico médio, perdas contábeis ou avaliação oficial. É apoio gerencial aproximado.

### Limitações

**46. Por que não há Curva ABC?**

- **Resposta curta:** Não há venda/valor por produto confiável.
- **Resposta detalhada:** Saída física isolada não prova faturamento e quantidades de unidades distintas não são base monetária. ABC financeira ficou futura.

**47. Por que não fornecedores?**

- **Resposta curta:** Priorizou-se profundidade nos módulos centrais.
- **Resposta detalhada:** Fornecedores exigiriam relacionamentos com compras e processo próprio. O TCC demonstra integração estoque-finanças sem inventar esse fluxo.

**48. O SGF tem múltiplos depósitos?**

- **Resposta curta:** Não, um saldo agregado por produto.
- **Resposta detalhada:** Product.CurrentStock representa total atual; não há localização, transferência ou reserva. É limitação explícita.

**49. Há histórico completo do custo do produto?**

- **Resposta curta:** Não.
- **Resposta detalhada:** CostPrice atual pode mudar e valor estimado usa esse valor; não há custo médio/por lote. Evite apresentar estimativa como valor contábil.

**50. O tema acompanha o usuário em outro computador?**

- **Resposta curta:** Não, só neste browser.
- **Resposta detalhada:** sgf.theme reside em localStorage; preferência visual não foi persistida em usuário/banco. Trocar dispositivo pode mudar tema.

### Produção

**51. O SGF está pronto para público real?**

- **Resposta curta:** Não, é protótipo acadêmico robusto.
- **Resposta detalhada:** Há base funcional/testada, mas faltam rate limiting, HTTPS/proxy, secrets gerenciados, backup, observabilidade, LGPD e revisão operacional.

**52. Como seriam hospedados os componentes?**

- **Resposta curta:** React estático, API em servidor/container, PostgreSQL persistente.
- **Resposta detalhada:** Precisam de domínio/HTTPS, conexão privada, migrations controladas, monitoramento, backups e CI/CD. Nenhum provedor específico foi escolhido.

**53. O que muda se frontend e API forem cross-site?**

- **Resposta curta:** Cookie SameSite=Lax pode não servir.
- **Resposta detalhada:** Rever estratégia de cookie e CSRF junto com HTTPS/CORS. Configuração local localhost em portas distintas é cross-origin, mas mesmo site.

**54. Como lidar com migrations em produção?**

- **Resposta curta:** Aplicá-las controladamente após backup e testes.
- **Resposta detalhada:** Não usar seed demo nem alteração manual casual. Validar compatibilidade, janela de deploy e rollback antes de executar.

**55. Qual risco do refresh em duas abas?**

- **Resposta curta:** Rotação concorrente pode invalidar uma tentativa.
- **Resposta detalhada:** RevokedAt impede dois usos aceitos, mas não sincroniza abas nem recupera resposta perdida. Novo login pode ser necessário; família de tokens é futura.

## 36. Glossário

| Termo | Significado no SGF |
| --- | --- |
| API | Interface HTTP da aplicação em Sgf.Api para frontend e clientes; não é a interface visual. |
| REST | Estilo de recursos/verbos HTTP; o SGF usa GET/POST/PUT/PATCH em rotas de produtos, estoque e financeiro. |
| Endpoint | Combinação de método e rota, por exemplo POST /api/inventory/exits. |
| DTO | Objeto de transporte de request/response; SaveProductRequest não é a entidade Product. |
| C# | Linguagem do backend .NET. |
| .NET SDK/runtime | SDK compila/testa/aplica ferramentas; runtime executa a aplicação. |
| ASP.NET Core | Framework HTTP do backend, com DI, autenticação e Minimal APIs. |
| Dependency Injection (DI) | Contêiner fornece dependências configuradas em Program.cs/DependencyInjection.cs, sem cada classe construir tudo. |
| Scoped | Vida útil por request HTTP; CurrentTenantContext/DbContext não devem ser compartilhados entre requisições. |
| Middleware | Etapa do pipeline HTTP, como CORS, autenticação e autorização em Program.cs. |
| Identity | Biblioteca ASP.NET Core para usuário, senha/hash e tabelas correspondentes. |
| Hash | Transformação unidirecional; Identity aplica hash de senha, SHA-256 do refresh opaco gera chave de busca. |
| Salt | Valor incorporado por algoritmo de senha para evitar hashes iguais/reutilização de tabelas; Identity administra detalhes. |
| JWT | Token assinado com claims; access do SGF expira rapidamente e não é refresh. |
| Bearer | Esquema Authorization: Bearer em que posse do access token autentica a chamada. |
| Claim | Dado dentro do token, como sub, company_id e role; deve ser assinado e ainda pode envelhecer. |
| Signing Key | Segredo compartilhado para HMAC do JWT; fica fora do Git. |
| Issuer/Audience | Emissor/destinatário aceitos na validação JWT. |
| Refresh token | Credencial aleatória opaca no cookie para pedir novo access token. |
| Cookie HttpOnly | Cookie que o browser envia mas JavaScript não consegue ler. |
| SameSite/Secure | Regras de envio entre sites / somente HTTPS quando Secure; local dev é exceção de Secure. |
| CORS | Política que restringe quais origens de navegador podem ler respostas da API; não substitui autenticação. |
| SPA | Interface React navegada sem recarga integral a cada rota. |
| React Router | Biblioteca de rotas da SPA e do layout aninhado. |
| TanStack Query | Cache e ciclo de dados assíncronos do servidor no frontend. |
| Query/Mutation | Leitura remota / operação que altera servidor. |
| Cache/Invalidation | Resposta armazenada / marcação para refetch após mutation. |
| ORM | Mapeia objetos C# a tabelas; EF Core é o ORM usado. |
| DbContext | Unidade EF de consulta, tracking e SaveChanges por operação/requisição. |
| Migration | Script C# versionado que evolui schema PostgreSQL em ordem. |
| Tenant | Empresa isolada dentro do SaaS compartilhado. |
| Multi-tenancy | Uma implantação atende vários tenants com separação de acesso/dados. |
| Membership | Vínculo User-Company que carrega papel e atividade. |
| ICompanyScopedEntity | Contrato das entidades operacionais cujo dono é CompanyId. |
| Global Query Filter | Predicado EF adicionado às consultas normais da entidade, inclusive por ID. |
| Fail-closed | Contexto ausente/inválido nega dados/escrita, nunca amplia acesso. |
| IDOR | Acesso indevido ao recurso por conhecer/trocar seu ID; filtro tenant reduz esse risco. |
| Concurrency token | Propriedade original colocada no WHERE para detectar mudança/escrita indevida. |
| Optimistic concurrency | Verificar conflito ao gravar, sem bloqueio preventivo abrangente. |
| Transação | Grupo de mudanças confirmado ou revertido junto. |
| Rollback | Reversão do grupo quando não pode confirmar todas as partes. |
| Atomicidade | Ou toda operação se completa, ou nenhuma mudança parcial permanece. |
| FK/PK/índice | Chave estrangeira liga tabelas, primária identifica linha, índice acelera busca ou impõe unicidade. |
| Precision/scale | Dígitos totais / casas decimais de numeric; estoque 14,3 e financeiro 14,2. |
| UTC | Referência temporal dos eventos; o dashboard interpreta dia de negócio em UTC-03 fixo. |
| HTTP 400 | Request inválido conforme contrato/validações. |
| HTTP 401 | Falta credencial válida ou token expirado/adulterado. |
| HTTP 403 | Autenticado, mas contexto tenant/role não autoriza. |
| HTTP 404 | Recurso inexistente **ou invisível para este tenant**. |
| HTTP 409 | Conflito: SKU, status ou escrita concorrente, conforme código público. |
| Playwright | Testes em navegador real para fluxos de UI, cookies e responsividade. |
| ADR | Registro breve de uma decisão arquitetural e suas alternativas/trade-offs. |

## 37. Guia para estudar o código

Não tente memorizar tudo de uma vez. Abra arquivo, explique em voz alta o fluxo e faça uma pergunta de “e se falhar?” antes da etapa seguinte.

| Etapa sugerida | Abra estes arquivos | Você deve conseguir responder |
| --- | --- | --- |
| 1. Produto/arquitetura | README.md, docs/01-visao-do-produto.md, docs/03-arquitetura.md, src/backend/Sgf.sln | Que problema resolve? Por que uma API monolítica em camadas? |
| 2. Composição/HTTP | src/backend/Sgf.Api/Program.cs, Sgf.Infrastructure/DependencyInjection.cs | Onde são registradas autenticação, CORS, serviços e rotas? |
| 3. Modelo/EF | Sgf.Domain/Companies/*, Sgf.Infrastructure/Database/SgfDbContext.cs, Database/Configurations/* | Como Company, User e Membership se relacionam? Onde ficam índices/constraints? |
| 4. Migrations | os cinco arquivos de migration .cs (não Designer), ModelSnapshot | Que tabela surgiu em cada fase? O que a FK composta protege? |
| 5. Cadastro/Identity | RegisterCompanyOwnerService.cs, Program.cs, ADR 001/002 | Por que a transação abrange UserManager, Company e Membership? |
| 6. JWT/refresh | LoginService.cs, AccessTokenFactory.cs, RefreshTokenService.cs, RefreshTokenGenerator.cs, ADR 003/006 | Onde vêm claims? Como rotação e logout funcionam? |
| 7. Tenant/segurança | CurrentTenantContext.cs, ICompanyScopedEntity.cs, CompanyScoped*Extensions.cs, testes de isolamento, ADR 004/005 | Como A é impedida de ler/escrever B, inclusive Attach? |
| 8. Produtos | Product.cs, ProductService.cs, ProductEndpoints.cs, ProductForm.tsx/ProductsPage.tsx | Como SKU/CompanyId são controlados? Por que não DELETE? |
| 9. Estoque | InventoryMovement.cs, Product.RecordMovement, InventoryService.cs, InventoryEndpointTests.cs | Como saldo/histórico ficam atômicos? Por que duas saídas não geram negativo? |
| 10. Financeiro | FinancialEntry.cs, FinanceService.cs, FinancePage.tsx | O que é realizado vs pendente? Para que serve Version? |
| 11. Analytics | AnalyticsContracts.cs, DashboardService.cs, InsightRules.cs, AnalyticsPage.tsx | Quais somas têm período? Quais são estado atual? Insights são IA? |
| 12. Browser/UX | index.html, App.tsx, AuthProvider.tsx, session.ts, lib/api.ts, AppLayout.tsx, SettingsPage.tsx | O que acontece no F5 e no logout? Por que tema não está no banco? |
| 13. Validação/banca | src/backend/*Tests, src/frontend/tests, docs/15-roteiro-demonstracao.md, docs/14-divida-tecnica-e-trabalhos-futuros.md | Quais riscos foram testados e quais limites você assume? |

Use uma instância local do banco **de testes/desenvolvimento** para acompanhar as queries sem mexer em dados da banca. Comece pelos caminhos felizes, depois falhas: 401/403/404/409, transação, concorrência. Desenhe a sequência HTTP -> serviço -> EF -> resposta no papel.

## 38. Mapa de arquivos importantes

Caminhos relativos à raiz; abra os arquivos indicados, não apenas leia este resumo.

| Arquivo/caminho | Responsabilidade; quando é usado; relação |
| --- | --- |
| `AGENTS.md` | Convenções para agentes; orienta futuras mudanças, não executa em runtime. |
| `README.md`, `global.json`, `docker-compose.yml`, `.env.example` | Guia local, SDK, Postgres e variáveis de exemplo; consultados antes de rodar. |
| `src/backend/Sgf.Api/Program.cs` | Startup, JWT/CORS/cookie/auth/health e auth endpoints; compõe Infrastructure e rotas de negócio. |
| `src/backend/Sgf.Api/Identity/CurrentTenantContext.cs` | Resolve identidade/empresa/role por request e configura DbContext; usado por serviços/policies. |
| `src/backend/Sgf.Api/Authorization/CurrentTenantRoleAuthorizationHandler.cs` | Policies OwnerOnly/AdminOrOwner com role atual; usa contexto. |
| `src/backend/Sgf.Api/{Products,Inventory,Finance,Analytics}/*Endpoints.cs` | Fronteira HTTP dos quatro módulos; traduz resultados em status e chama interfaces. |
| `src/backend/Sgf.Application/Identity/ICurrentTenantContext.cs` | Contrato sem ASP.NET Core para serviços lerem tenant confiável. |
| `src/backend/Sgf.Application/{Products,Inventory,Finance}/*Contracts.cs` | Requests/responses, erros e interfaces; ponte API <-> implementação. |
| `src/backend/Sgf.Application/Analytics/AnalyticsContracts.cs` | Períodos, indicadores, comparação e resposta do dashboard. |
| `src/backend/Sgf.Application/Analytics/InsightRules.cs` | Regras puras de insight; recebe agregados do DashboardService. |
| `src/backend/Sgf.Domain/Companies/{Company,Membership,ICompanyScopedEntity}.cs` | Tenant, vínculo de identidade e marcação de entidade operacional. |
| `src/backend/Sgf.Domain/Products/Product.cs` | Invariantes de produto e método RecordMovement que muda saldo. |
| `src/backend/Sgf.Domain/Inventory/InventoryMovement.cs` | Evento físico de entrada/saída; salvo junto ao saldo. |
| `src/backend/Sgf.Domain/Finance/FinancialEntry.cs` | Invariantes financeiras e transição Pending -> Paid. |
| `src/backend/Sgf.Infrastructure/DependencyInjection.cs` | Registro de EF/Identity/serviços e TimeProvider; chamado por Program. |
| `src/backend/Sgf.Infrastructure/Database/SgfDbContext.cs` | DbSets, Identity, filtros e SaveChanges protegido; núcleo da persistência. |
| `src/backend/Sgf.Infrastructure/Database/MultiTenancy/CompanyScopedModelBuilderExtensions.cs` | Global Query Filters e CompanyId concurrency token. |
| `src/backend/Sgf.Infrastructure/Database/MultiTenancy/CompanyScopedChangeTrackerExtensions.cs` | Define/verifica CompanyId na escrita rastreada. |
| `src/backend/Sgf.Infrastructure/Database/Configurations/*Configuration.cs` | Mapeia tabelas, índices, constraints, precisão, FKs e concorrência. |
| `src/backend/Sgf.Infrastructure/Database/Migrations/*.cs` | Evolução do schema; comparável ao ModelSnapshot. |
| `src/backend/Sgf.Infrastructure/Identity/Registration/RegisterCompanyOwnerService.cs` | Cadastro transacional incluindo Identity. |
| `src/backend/Sgf.Infrastructure/Identity/Authentication/{LoginService,AccessTokenFactory,RefreshTokenService,RefreshTokenGenerator}.cs` | Credenciais, JWT, token opaco/hash e rotação. |
| `src/backend/Sgf.Infrastructure/{Products,Inventory,Finance,Analytics}/{ProductService,InventoryService,FinanceService,DashboardService}.cs` | Implementações reais, queries e SaveChanges para módulos. |
| `src/frontend/index.html`, `src/frontend/src/main.tsx`, `src/frontend/src/App.tsx` | Tema antes do React, providers e árvore de rotas. |
| `src/frontend/src/features/auth/{session,AuthProvider,AuthPages}.ts(x)` | Token em memória, restauração/refresh, estado e formulários públicos. |
| `src/frontend/src/lib/api.ts` | Base URL, fetch, erros/timeout; usado pelas features. |
| `src/frontend/src/app/{AppLayout,Sidebar,UserMenu,theme}.ts(x)` | Shell, navegação, sessão aparente e tema. |
| `src/frontend/src/features/{products,inventory,finance,analytics,settings}/*Page.tsx` | Telas, queries/mutations, filtros e UI; usam api.ts da feature. |
| `scripts/seed-demo.ps1` | Carga local manual para banca; cria via API e ajusta datas de IDs recém-criados no PostgreSQL. |
| `src/backend/Sgf.Infrastructure.Tests/MultiTenancy/CompanyScopedEntityIsolationTests.cs` | Teste de isolamento de leitura/escrita, inclusive anexação sem SELECT. |
| `src/backend/Sgf.Api.Tests/{Identity,Products,Inventory,Finance,Analytics}/*Tests.cs` | HTTP integrado, banco real, JWT e regras dos módulos. |
| `src/frontend/tests/*.spec.ts` | Fluxos reais de browser incluindo autenticação, F5, módulos e temas. |

`Sgf.Shared` permanece referenciado e sem função de código clara; não transforme sua presença em “módulo transversal implementado”. `features/overview/ModulePage.tsx` é legado de placeholder, não rota ativa.

## 39. Resumo para defesa

### Se eu tivesse 5 minutos para explicar tecnicamente o SGF

“Construímos uma aplicação acadêmica para pequenas empresas registrarem produtos, estoque e finanças e observarem indicadores que apoiam decisões. É uma SPA React/TypeScript com API ASP.NET Core .NET 10 em monólito modular e PostgreSQL via EF Core. Cada empresa é um tenant; usuário e empresa se ligam por Membership, que contém a role. Identity protege senha, JWT curto identifica sessão e refresh opaco em cookie HttpOnly permite renovação. A API revalida Membership/Company/role, filtra leitura por CompanyId e condiciona escrita ao tenant persistido, incluindo objetos anexados. Estoque registra saldo e movimento atomicamente, com concorrência otimista; financeiro distingue pendente de pago e calcula saldo realizado. Dashboard agrega no banco, compara períodos e aplica insights determinísticos, não IA. Há 160 testes backend aprovados e 20 cenários Playwright listados, incluindo isolamento e concorrência. O sistema é demonstrável, mas antes de publicar faltam rate limiting, HTTPS/segredos operacionais, backups, observabilidade e revisão de segurança.”

### Se eu tivesse 30 minutos

1. **3 min, problema e escopo:** pequena empresa, planilhas fragmentadas, entregas reais e ausência de vendas/fornecedores/integrações.
2. **4 min, arquitetura e modelo:** diagrama de camadas, Company, User, Membership, Product, Movement, FinancialEntry; por que monólito e banco compartilhado.
3. **5 min, identidade/tenant:** cadastro transacional; login JWT; refresh; revalidação scoped; filtros; Attach cross-tenant e SQL predicate; 401/403/404.
4. **5 min, operação:** criar produto, SKU único por empresa, inativação; entrada/saída com transação e concurrency token; financeiro Pending/Paid, idempotência.
5. **5 min, analytics:** fórmulas, períodos UTC-03, realizado vs estado atual, comparação sem divisão por zero, série, baixo estoque, top saída e insights determinísticos.
6. **3 min, frontend:** React Router, shell, Query, access em memória, cookie HttpOnly/F5, tema local.
7. **3 min, evidência:** migrations, testes de tenant/concorrência, Playwright, demo controlada; limites dos testes.
8. **2 min, limites/publicação:** sem IA/ABC/vendas; rate limiting, HTTPS/secrets, backups, LGPD e observabilidade.

## 40. Autoavaliação

**As 10 partes que mais preciso dominar:** (1) problema/escopo real; (2) camadas e dependências; (3) User-Membership-Company; (4) fluxo cadastro transacional; (5) JWT/refresh/logout e F5; (6) revalidação tenant; (7) filtro de leitura + proteção de escrita Attach; (8) estoque atômico/concorrente; (9) realizado vs pendente e período; (10) fórmulas/insights e limitações.

**As 10 perguntas mais difíceis:** Por que filtro EF não cobre Attach? Como CompanyId entra no SQL? Como revogar JWT já emitido? E se refresh rodar em duas abas? Como duas saídas simultâneas não deixam negativo? Por que Role em Membership e não IdentityRole? Por que pendentes não obedecem ao período? O que significa RepeatableRead no dashboard? Por que saída não é venda/ABC? O que falta objetivamente antes de publicar? Responda desenhando o fluxo e apontando os arquivos das seções 38 e 35.

**Onde é fácil se equivocar:** dizer que frontend protege dados; que JWT é criptografado; que logout invalida access imediatamente; que SKU é global; que saldo é alterável no produto; que toda movimentação é venda; que dashboard mostra estoque “histórico” ao trocar período; que saldo realizado é saldo bancário; que insights são IA; que 20 testes listados significam 20 passados nesta verificação.

**Limitações a assumir:** sem switch-company/gestão de membros, sem vendas/fornecedores/integração financeira, sem estoque por depósito/custo médio, sem revogação imediata de access, sem controles completos de publicação, sem histórico de custo/estoque snapshot e sem ABC/IA. Cada uma é uma fronteira clara de escopo, não algo a esconder.

**Diferenciais positivos do TCC:** integração ponta a ponta; modelagem de Membership por empresa; isolamento testado de leitura e escrita anexada; transações de cadastro/estoque; concorrência de saldo/refresh/financeiro; métricas com definição temporal honesta; insights explicáveis; UI utilizável em desktop/mobile e tema; migrations e testes com PostgreSQL real.

> **Regra final para defesa:** ao afirmar uma capacidade, localize primeiro o endpoint, o serviço, a entidade/migration e ao menos um teste. Ao discutir uma limitação, diga a consequência prática e a etapa que seria necessária para superá-la. O SGF apoia o gestor; ele não substitui o julgamento do gestor.

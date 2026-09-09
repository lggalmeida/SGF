# Fase F - Entrega e validacao

## Resultado

Implementados refresh token com rotacao, logout e autorizacao basica.
Nenhuma biblioteca nova, entidade operacional, tela ou switch-company foi adicionado.

Build da solucao: sucesso, zero avisos e erros.
Testes: 67 aprovados, zero falhas e zero ignorados (API 49, Infrastructure 17,
Application 1). A fase adicionou 23 casos executados de integracao.

Migration aplicada: `20260909223044_AddRefreshTokens` em `sgf_dev`.
Cria somente `RefreshTokens`; o [ADR 006](adr/006-refresh-token-and-authorization.md)
descreve colunas, FKs, indices, cookies e decisoes de seguranca.

## Arquivos criados nesta fase

Sob `src/backend/Sgf.Application/Identity/`:

- CurrentUserAuthorizationPolicies.cs
- ICurrentUserAuthorizationService.cs
- ILogoutUseCase.cs
- IRefreshTokenUseCase.cs
- LogoutRequest.cs
- RefreshTokenRequest.cs
- RefreshTokenResponse.cs
- RefreshTokenResult.cs

Sob `src/backend/Sgf.Infrastructure/Identity/Authentication/`:

- AccessTokenFactory.cs
- CurrentUserAuthorizationService.cs
- RefreshToken.cs
- RefreshTokenGenerator.cs
- RefreshTokenOptions.cs
- RefreshTokenService.cs

Demais arquivos:

- src/backend/Sgf.Api/Authorization/CurrentTenantRoleAuthorizationHandler.cs
- src/backend/Sgf.Api.Tests/Identity/RefreshAndAuthorizationTests.cs
- src/backend/Sgf.Infrastructure/Database/Configurations/RefreshTokenConfiguration.cs
- src/backend/Sgf.Infrastructure/Database/Migrations/20260909223044_AddRefreshTokens.cs
- src/backend/Sgf.Infrastructure/Database/Migrations/20260909223044_AddRefreshTokens.Designer.cs
- docs/adr/006-refresh-token-and-authorization.md
- docs/07-validacao-fase-f.md

## Arquivos modificados nesta fase

- .env.example: duracao e nome do cookie, sem segredo real.
- AGENTS.md: regras de refresh, cookies e autorizacao.
- README.md: configuracao da chave, migrations e estado da identidade.
- src/backend/Sgf.Api/Program.cs: endpoints, cookies, policies, CORS e validacao de configuracao.
- src/backend/Sgf.Api/appsettings.json: opcoes de refresh.
- src/backend/Sgf.Api/appsettings.Development.json: opcoes de refresh.
- src/backend/Sgf.Application/Identity/LoginResult.cs: resultado interno transporta cookie separado do DTO publico.
- src/backend/Sgf.Infrastructure/Database/SgfDbContext.cs: DbSet de refresh.
- src/backend/Sgf.Infrastructure/Database/Migrations/SgfDbContextModelSnapshot.cs: novo modelo persistido.
- src/backend/Sgf.Infrastructure/DependencyInjection.cs: registro dos casos de uso, gerador e opcoes.
- src/backend/Sgf.Infrastructure/Identity/ApplicationUser.cs: navegacao de refresh tokens.
- src/backend/Sgf.Infrastructure/Identity/Authentication/LoginService.cs: criacao da sessao no login.

O repositorio ja continha alteracoes nao commitadas da fase E; elas foram preservadas.

## Contratos e fluxo

POST /api/auth/login continua recebendo email/password e retorna 200 com
accessToken, tokenType, expiresAt, userId, companyId e role. Adicionalmente envia
o refresh pelo cookie HttpOnly, nunca no JSON. AccessTokenFactory centraliza a emissao.

POST /api/auth/refresh recebe somente o cookie. Calcula SHA-256, verifica validade,
usuario, membership e empresa. Usa a Role atual e a Company original; revoga o
token antigo e persiste um substituto atomicamente. Retorna 200 com o mesmo formato
seguro do login, ou 401 generico se a sessao for invalida. Uso concorrente e rejeitado.

POST /api/auth/logout revoga o token recebido, apaga cookie e retorna 204.
JWT existente pode continuar valido por ate 15 minutos, sujeito ao contexto do tenant.

OwnerOnly e AdminOrOwner usam o contexto scoped revalidado no PostgreSQL.
Membership com Role diferente da claim invalida o contexto, preservando a fase D.
Nao existe endpoint artificial para demonstrar policies.

## Comandos e evidencias

Na raiz, com Docker iniciado e a chave configurada no processo conforme README:

```powershell
docker compose up -d
./.dotnet/dotnet.exe build src/backend/Sgf.sln
./.dotnet/dotnet.exe test src/backend/Sgf.sln --no-build
./.dotnet/dotnet.exe ef database update --no-build --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api
./.dotnet/dotnet.exe run --no-build --project src/backend/Sgf.Api --launch-profile http
```

A migration foi gerada com `dotnet ef migrations add AddRefreshTokens`, usando
`--output-dir Database/Migrations`, e regenerada antes da aplicacao para incluir
o relacionamento Company e o mapeamento de concorrencia definitivo.

Validacao manual via HTTP em localhost:5206:

| Operacao | Resultado |
| --- | --- |
| GET /api/health | 200 |
| GET /api/health/database | 200 |
| POST /api/auth/register | 201 |
| POST /api/auth/login | 200 e cookie |
| GET /api/auth/me com JWT | 200, empresa correta |
| POST /api/auth/refresh | 200, novo JWT, mesma empresa |
| POST /api/auth/logout | 204 |
| Refresh apos logout | 401 |
| Preflight localhost:5173 | 204, origem exata e credentials=true |

Development e Production sem chave foram executados separadamente: ambos
recusaram inicializacao com erro explicito de Signing Key. `.env` esta ignorado;
`.env.example` esta rastreado. Nenhuma chave real foi adicionada aos arquivos.
A validacao manual criou uma conta/empresa identificada como Validacao Local Fase F.

## Problemas encontrados e limites

- PostgreSQL estava parado: resolvido com Docker Compose.
- A tarefa ainda apontava para tcc-saas, pasta renomeada para SGF: comandos e patches
  foram executados explicitamente no caminho atual. O workspace do aplicativo ainda
  precisa apontar para SGF para evitar esse problema em tarefas futuras.
- A primeira geracao da migration exigia chave JWT no processo de ferramentas:
  configurada somente no ambiente, sem enfraquecer validacao da API.
- Um teste enviava header Cookie vazio e falhava no cliente HTTP: corrigido para
  omitir o header. Dois avisos do analisador xUnit tambem foram corrigidos.
- O teste de concorrencia mostrou apenas um refresh aceito e um token ativo restante.
- Nao ha evidencia que atribua as interrupcoes da conversa a esses problemas locais.
- Sem limpeza automatica de tokens expirados, familias de sessao ou revogacao global.
  Esses limites sao deliberados e nao impedem os fluxos desta fase.
- Producao requer HTTPS, chave persistente secreta, conexao de banco e origens CORS
  configuradas. Publicacao em producao nao foi validada nesta tarefa.
- Frontend futuro deve coordenar um refresh por vez e usar credentials: include.

## Explicacao didatica

Access token e a credencial curta enviada em Authorization para usar a API.
Refresh token e a credencial mais longa para pedir outra credencial curta; o
navegador guarda o cookie sem disponibiliza-lo ao JavaScript.

Quando o JWT expira, o cliente chama refresh. Se a sessao continuar valida, recebe
novo JWT e novo cookie. Se estiver expirada, revogada ou sem acesso a empresa,
sera necessario novo login.

Se Owner virar Member, o JWT antigo diverge do Membership atual: o contexto e
recusado e as policies nao concedem privilegio. O refresh emite JWT com Member,
que tambem nao satisfaz OwnerOnly ou AdminOrOwner.

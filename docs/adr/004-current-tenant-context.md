# ADR 004 - Resolucao do Tenant Atual pela Identidade Autenticada

## Status

Aprovado.

## Contexto

O SGF e um SaaS multi-tenant. Depois do login, o access token JWT contem `sub`, `company_id` e `role`. Esses dados identificam o usuario, a empresa atual e o papel do usuario naquela empresa.

Mesmo assim, o backend nao deve confiar em `CompanyId` enviado livremente pelo cliente em body, query string, header, route parameter ou formulario. Se isso fosse aceito, um cliente poderia tentar informar o identificador de outra empresa e acessar dados indevidos.

## Decisao

O tenant atual sera resolvido por uma abstracao request-scoped chamada `ICurrentTenantContext`.

A origem inicial dos dados sera a identidade autenticada pelo JWT. Em seguida, o backend revalidara no PostgreSQL que:

- o usuario existe;
- o `Membership` para `UserId + CompanyId` existe;
- o `Membership` esta ativo;
- a `Company` existe;
- a `Company` esta ativa;
- a role do token continua igual a role atual do `Membership`.

A camada Application dependera apenas da abstracao, sem conhecer `HttpContext`, `ClaimsPrincipal` ou detalhes de ASP.NET Core.

## Consequencias

- Casos de uso futuros poderao receber `CompanyId` confiavel sem interpretar claims manualmente.
- `CompanyId` e `Role` continuam vindo do backend, a partir de `Membership`, e nao do frontend.
- Tokens antigos deixam de conceder acesso caso o membership ou a empresa sejam desativados.
- Mudancas de role no banco nao sao ignoradas por tokens antigos.
- A resolucao ocorre sob demanda, entao endpoints publicos como health, register e login continuam sem exigir tenant.

## Alternativas Consideradas

### Ler claims diretamente em cada controller

Seria simples no inicio, mas espalharia logica de seguranca pela API e aumentaria risco de endpoints esquecidos ou inconsistentes.

### Middleware obrigatorio para todas as requisicoes

Foi evitado porque nem toda rota precisa de tenant. Health checks, cadastro e login devem continuar funcionando sem usuario autenticado.

### Aceitar CompanyId enviado pelo frontend

Foi rejeitado por ser inseguro para isolamento multi-tenant. O frontend pode sugerir intencoes de navegacao no futuro, mas a autoridade sobre o tenant atual deve vir da autenticacao e da validacao no backend.

# ADR 005 - Isolamento de Dados Multi-Tenant com CompanyId e Global Query Filters

## Status

Aprovado.

## Contexto

O SGF usa banco compartilhado entre empresas. Por isso, futuras entidades de negocio pertencentes a uma empresa, como `Product`, `Supplier`, `StockMovement` e `FinancialTransaction`, precisarao ser isoladas por `CompanyId`.

Esse isolamento e requisito de seguranca. Uma empresa nao pode consultar, alterar ou excluir dados de outra empresa, mesmo que conheca o identificador do registro.

## Decisao

Futuras entidades de negocio tenant-scoped deverao implementar `ICompanyScopedEntity` e possuir `CompanyId`.

A leitura sera protegida por Global Query Filters do Entity Framework Core. O filtro usara o `CompanyId` confiavel da requisicao atual, definido a partir do `ICurrentTenantContext`.

A escrita tambem sera protegida:

- registros novos recebem `CompanyId` do tenant atual no backend;
- alteracoes e exclusoes exigem que o registro pertença ao tenant atual;
- `CompanyId` nao pode ser alterado arbitrariamente depois da criacao.

Se uma operacao tenant-scoped ocorrer sem contexto valido, o comportamento deve ser fail closed: nao retornar dados e nao permitir escrita.

## Fora do Escopo Desta Fase

Nenhuma entidade de negocio real foi criada nesta fase. `Product`, `Supplier`, `StockMovement` e entidades financeiras serao implementadas em etapas futuras.

Os testes do mecanismo usam uma entidade e um DbContext exclusivos do projeto de testes, sem gerar migration ou tabela permanente do SGF.

## IgnoreQueryFilters

`IgnoreQueryFilters()` pode quebrar o isolamento multi-tenant. Seu uso deve ser excepcional, justificado e revisado com atencao. Ele nao deve ser usado em fluxos normais de funcionalidades tenant-scoped.

## Consequencias

### Hardening F.1: escrita de entidades anexadas

CompanyId tambem e concurrency token no mapeamento de entidades ICompanyScopedEntity.
Assim, SaveChanges/SaveChangesAsync geram UPDATE e DELETE com predicado
`WHERE Id = @id AND CompanyId = @originalCompanyId`. As validacoes do ChangeTracker
exigem que o valor original e o atual sejam iguais ao tenant autenticado.
Um objeto anexado pode mentir sobre seu valor original; o predicado no PostgreSQL
impede que essa mentira altere um registro de outra empresa. Zero linhas afetadas
gera DbUpdateConcurrencyException, sem nova tentativa retirando o tenant.

Os testes constroem entidades manualmente, sem SELECT previo, e cobrem Attach,
Update e Remove, acesso legitimo, acesso cross-tenant e SQL efetivamente executado.
Isso nao cria coluna nova: CompanyId ja pertence ao contrato. Company, Membership
e Identity continuam fora desse mecanismo.

Essa protecao vale para escrita rastreada via SaveChanges. SQL bruto e operacoes
em lote ExecuteUpdate/ExecuteDelete nao passam pelo ChangeTracker nem aplicam
automaticamente tokens de concorrencia; exigem revisao explicita do predicado
de tenant quando futuramente necessarias. Nenhuma foi adicionada nesta fase.

- Consultas simples em entidades tenant-scoped ja nascem protegidas por `CompanyId`.
- Consultas por ID tambem respeitam o tenant atual, reduzindo risco de IDOR.
- O desenvolvedor ainda precisa garantir que casos de uso tenant-scoped resolvam o `ICurrentTenantContext` antes de operar no `DbContext`.
- Futuras tabelas tenant-scoped deverao avaliar indices com `CompanyId`, como `CompanyId + SKU` ou `CompanyId + CreatedAt`, conforme o caso real.
- Testes de isolamento entre tenants sao obrigatorios para novos modulos tenant-scoped.

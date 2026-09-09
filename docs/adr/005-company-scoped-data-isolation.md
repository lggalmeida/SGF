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

- Consultas simples em entidades tenant-scoped ja nascem protegidas por `CompanyId`.
- Consultas por ID tambem respeitam o tenant atual, reduzindo risco de IDOR.
- O desenvolvedor ainda precisa garantir que casos de uso tenant-scoped resolvam o `ICurrentTenantContext` antes de operar no `DbContext`.
- Futuras tabelas tenant-scoped deverao avaliar indices com `CompanyId`, como `CompanyId + SKU` ou `CompanyId + CreatedAt`, conforme o caso real.
- Testes de isolamento entre tenants sao obrigatorios para novos modulos tenant-scoped.

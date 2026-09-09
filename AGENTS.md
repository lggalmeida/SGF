# Instruções para Agentes do Projeto

Este projeto é um Trabalho de Conclusão de Curso em Sistemas de Informação. O papel dos agentes é ajudar no desenvolvimento técnico, mas também preservar a clareza didática das decisões tomadas.

Antes de implementar qualquer funcionalidade, leia a documentação em `/docs`.

## Objetivo do Projeto

Desenvolver o **SGF - Sistema de Gestão Facilitada**, uma plataforma SaaS para gestão integrada de estoque e finanças com análise de dados aplicada à tomada de decisão em pequenas empresas.

O projeto deve ser robusto o suficiente para se aproximar de uma solução publicável, mas sem complexidade desnecessária.

## Princípios Gerais

- Priorizar simplicidade, organização e boas práticas.
- Explicar decisões técnicas importantes quando forem introduzidas.
- Evitar abstrações prematuras.
- Não adicionar tecnologias sem necessidade real.
- Manter o projeto adequado ao contexto de um TCC.
- Preservar a capacidade de aprendizado do autor do projeto.
- Preferir código claro a soluções excessivamente genéricas.

## Arquitetura Definida

A arquitetura escolhida é um **monólito modular**.

Os agentes devem respeitar essa decisão e organizar o sistema por módulos de negócio, evitando transformar o projeto em uma arquitetura distribuída.

Não introduzir, sem solicitação explícita e justificativa forte:

- microsserviços;
- Kafka ou outros brokers de eventos;
- Kubernetes;
- Redis;
- CQRS;
- event sourcing;
- bancos separados por tenant;
- múltiplos serviços independentes;
- infraestrutura complexa de produção.

Essas tecnologias podem ser úteis em outros contextos, mas não são necessárias para o escopo atual do projeto.

## Stack Planejada

Backend:

- C#;
- ASP.NET Core Web API;
- Entity Framework Core.

Banco:

- PostgreSQL.

Frontend:

- React;
- TypeScript;
- TanStack Query.

Não substituir a stack sem autorização explícita do usuário.

## Organização Esperada do Backend

O backend deverá seguir uma organização modular e em camadas, separando responsabilidades.

Estrutura conceitual esperada:

```text
/backend
  /src
    /Api
    /Application
    /Domain
    /Infrastructure
    /Shared
```

Responsabilidades esperadas:

- `Api`: entrada HTTP, controllers, middlewares e configuração da aplicação.
- `Application`: casos de uso, validações de aplicação e orquestração de regras.
- `Domain`: entidades, regras centrais e conceitos de negócio.
- `Infrastructure`: Entity Framework Core, PostgreSQL, autenticação, migrations e integrações técnicas.
- `Shared`: utilitários comuns, resultados, paginação, exceções e tipos compartilhados.

Sempre que possível, organizar funcionalidades por módulo de negócio:

- Identity;
- Companies;
- Products;
- Suppliers;
- Stock;
- Finance;
- Dashboard;
- Insights.

## Organização Esperada do Frontend

O frontend deverá ser organizado por funcionalidades.

Estrutura conceitual esperada:

```text
/frontend
  /src
    /app
    /features
    /components
    /lib
    /types
```

Responsabilidades esperadas:

- `app`: rotas, providers e configuração geral.
- `features`: funcionalidades de negócio.
- `components`: componentes reutilizáveis.
- `lib`: cliente HTTP, configuração do TanStack Query e funções auxiliares.
- `types`: tipos compartilhados do frontend.

## Multi-Tenancy

A estratégia definida é **banco compartilhado com isolamento por `CompanyId`**.

Regras obrigatórias:

- entidades de negócio devem possuir `CompanyId`;
- consultas devem ser filtradas pela empresa atual;
- comandos devem validar se o usuário tem acesso à empresa;
- usuários não podem acessar dados de empresas às quais não pertencem;
- testes devem cobrir isolamento entre empresas.

Não implementar banco por tenant ou schema por tenant no escopo inicial.


### Isolamento de Dados Tenant-Scoped

Para futuras entidades de negócio pertencentes a uma empresa, como `Product`, `Supplier`, `StockMovement` e `FinancialTransaction`:

- implementar `ICompanyScopedEntity`;
- possuir `CompanyId` como identificador da empresa dona do registro;
- obter `CompanyId` a partir do `ICurrentTenantContext`, nunca livremente do body, query string, header ou rota;
- deixar o backend definir `CompanyId` na criação;
- impedir alteração arbitrária de `CompanyId` após a criação;
- garantir que alteração e exclusão respeitem o tenant atual;
- cobrir isolamento multi-tenant com testes, incluindo leitura, consulta por ID, criação, alteração e exclusão.

O uso de `IgnoreQueryFilters()` deve ser excepcional, justificado e revisado com atenção, pois pode quebrar isolamento entre empresas.
## Autenticação e Autorização

A estratégia planejada é autenticação com JWT e autorização baseada em vínculo entre usuário e empresa.

Modelo conceitual:

- um usuário pode pertencer a várias empresas;
- uma empresa pode ter vários usuários;
- o relacionamento `Membership` define o papel do usuário naquela empresa.

Papéis iniciais previstos:

- `Owner`;
- `Admin`;
- `Member`.

## Regras de Implementação

- Refresh tokens devem ser opacos, persistidos somente por hash e entregues em cookie HttpOnly.
- Rotacao deve revogar e substituir atomicamente, protegendo tambem uso concorrente.
- Privilegios de empresa devem usar OwnerOnly/AdminOrOwner com ICurrentTenantContext revalidado; nao confiar apenas na role do JWT.
- Cookies de autenticacao exigem revisao de SameSite, Secure, Origin e CORS; nunca usar AllowAnyOrigin com AllowCredentials.
- Nunca registrar senhas, tokens, cookies de autenticacao ou hashes em logs.

- Não implementar funcionalidades fora do MVP sem alinhar antes.
- Não remover decisões documentadas sem atualizar a documentação correspondente.
- Ao criar uma regra de negócio, documentar em `/docs/05-regras-de-negocio.md`.
- Ao introduzir decisão arquitetural nova, registrar em documento apropriado.
- Priorizar testes para regras críticas, principalmente multi-tenancy, estoque e financeiro.
- Não permitir que regras importantes fiquem apenas no frontend.

## Estilo de Trabalho

Ao trabalhar neste projeto:

- leia o contexto antes de alterar código;
- faça mudanças pequenas e coerentes;
- mantenha nomes claros e consistentes;
- prefira validações explícitas;
- explique alternativas quando uma decisão relevante for tomada;
- preserve a simplicidade do projeto.

Este repositório não é apenas um produto de software. Ele também é material acadêmico e deve comunicar bem as decisões tomadas.





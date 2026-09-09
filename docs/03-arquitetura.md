# Arquitetura

## Visão Geral

A arquitetura escolhida para o **SGF - Sistema de Gestão Facilitada** é um **monólito modular**.

Essa decisão significa que o sistema será desenvolvido como uma única aplicação backend, mas organizado internamente em módulos de negócio bem definidos. O objetivo é manter o projeto simples de executar e entender, sem abrir mão de separação de responsabilidades.

## Por Que Monólito Modular

Um monólito modular é adequado para este projeto porque:

- reduz a complexidade operacional;
- facilita o desenvolvimento por uma pessoa com conhecimento ainda em formação;
- evita infraestrutura desnecessária;
- permite boa organização interna;
- é suficiente para o escopo de um TCC;
- pode evoluir no futuro caso exista necessidade real.

## Alternativas Consideradas

### Microsserviços

Microsserviços dividem o sistema em vários serviços independentes. Essa abordagem pode ser útil em empresas com grande escala, equipes separadas e necessidades específicas de deploy independente.

Não foi escolhida porque adicionaria complexidade alta em comunicação, autenticação, deploy, observabilidade e consistência de dados.

### Monólito sem Modularização

Um monólito simples, sem separação clara por módulos, seria mais rápido no início, mas poderia dificultar a manutenção conforme o sistema crescesse.

Não foi escolhido porque o projeto possui vários domínios de negócio: estoque, financeiro, empresas, usuários e indicadores.

### Monólito Modular

Foi escolhido por equilibrar simplicidade e organização.

## Módulos Planejados

### Identidade e Acesso

Responsável por cadastro de usuários, login, autenticação e autorização.

### Empresas e Multi-Tenancy

Responsável pelo cadastro de empresas, vínculo entre usuários e empresas e isolamento dos dados por tenant.

### Produtos

Responsável pelo cadastro e manutenção dos produtos da empresa.

### Fornecedores

Responsável pelo cadastro de fornecedores.

### Estoque

Responsável pelas movimentações de entrada, saída, ajuste e consulta de saldo.

### Financeiro

Responsável por receitas, despesas, contas a pagar, contas a receber, pagamentos, recebimentos e fluxo de caixa.

### Dashboard

Responsável pela apresentação de indicadores consolidados.

### Insights

Responsável por gerar alertas e recomendações simples baseadas em regras.

### Auditoria Básica

Responsável por registrar informações como data de criação, data de alteração e usuários envolvidos.

## Organização Conceitual do Backend

A estrutura esperada do backend é:

```text
/backend
  /src
    /Api
    /Application
    /Domain
    /Infrastructure
    /Shared
```

### Api

Camada responsável por receber requisições HTTP e retornar respostas ao cliente.

Exemplos de responsabilidades:

- controllers;
- middlewares;
- configuração da aplicação;
- autenticação na entrada das requisições.

### Application

Camada responsável pelos casos de uso do sistema.

Exemplos:

- criar produto;
- registrar entrada de estoque;
- pagar conta;
- gerar indicadores do dashboard.

Essa camada coordena regras e operações, mas não deve conter detalhes de banco de dados.

### Domain

Camada responsável pelos conceitos centrais do negócio.

Exemplos:

- entidades;
- regras de domínio;
- validações fundamentais;
- enums e objetos de valor quando forem úteis.

### Infrastructure

Camada responsável pelos detalhes técnicos.

Exemplos:

- Entity Framework Core;
- PostgreSQL;
- migrations;
- implementação de repositórios;
- serviços de autenticação;
- configurações técnicas.

### Shared

Camada para elementos reutilizáveis e genéricos.

Exemplos:

- paginação;
- tipos de resultado;
- exceções;
- constantes compartilhadas.

## Organização Conceitual do Frontend

A estrutura esperada do frontend é:

```text
/frontend
  /src
    /app
    /features
    /components
    /lib
    /types
```

### app

Configuração geral da aplicação, rotas e providers.

### features

Funcionalidades organizadas por domínio:

- auth;
- companies;
- products;
- suppliers;
- stock;
- finance;
- dashboard;
- insights.

### components

Componentes reutilizáveis de interface.

### lib

Funções auxiliares, cliente HTTP, configuração do TanStack Query e utilitários.

### types

Tipos compartilhados do frontend.

## Multi-Tenancy

A estratégia definida é **banco compartilhado com isolamento por `CompanyId`**.

Isso significa:

- todas as empresas usam o mesmo banco;
- as tabelas de negócio possuem uma coluna `CompanyId`;
- o backend filtra os dados pela empresa atual;
- o usuário só pode operar em empresas às quais está vinculado.

Essa abordagem foi escolhida por ser simples, adequada ao escopo acadêmico e comum em SaaS de pequeno e médio porte.

## Autenticação e Autorização

A autenticação será baseada em JWT.

A autorização será baseada no relacionamento entre usuário e empresa.

Modelo conceitual:

```text
User
  -> Membership
    -> Company
    -> Role
```

Papéis previstos:

- `Owner`;
- `Admin`;
- `Member`.

O backend deverá validar as permissões. O frontend pode ocultar ações, mas não deve ser a única barreira de segurança.

## Resolução do Tenant Atual

A API possui uma abstração request-scoped para representar o contexto autenticado do tenant atual.

Esse contexto contém:

```text
UserId
CompanyId
Role
```

Esses valores são derivados do JWT validado e revalidados no PostgreSQL por meio do relacionamento `User -> Membership -> Company`.

O backend não aceita `CompanyId` enviado livremente pelo frontend para definir o tenant atual. Isso evita que uma requisição tente operar em outra empresa apenas alterando body, query string, header ou route parameter.

A resolução é feita sob demanda, para que endpoints públicos como health check, cadastro e login continuem funcionando sem contexto de tenant.
## Isolamento de Dados Tenant-Scoped

Futuras entidades de negócio pertencentes a uma empresa deverão implementar `ICompanyScopedEntity` e possuir `CompanyId`.

Para leitura, o Entity Framework Core aplicará Global Query Filters em entidades tenant-scoped. Assim, uma consulta como `db.Products.ToListAsync()` deverá considerar automaticamente a empresa atual.

Para escrita, o backend deverá definir o `CompanyId` na criação usando o contexto autenticado, e operações de alteração ou exclusão deverão validar que o registro pertence ao tenant atual.

A ausência de tenant válido deve falhar de forma segura: entidades tenant-scoped não devem retornar dados e não devem permitir escrita.

`IgnoreQueryFilters()` deve ser reservado para casos excepcionais de manutenção ou administração e precisa de revisão cuidadosa.
## Banco de Dados

O banco definido é PostgreSQL, acessado via Entity Framework Core.

Diretrizes iniciais:

- usar migrations;
- usar `CompanyId` em entidades de negócio;
- usar campos de auditoria;
- criar índices em campos importantes;
- evitar exclusão física quando houver impacto histórico.

Campos comuns sugeridos:

```text
Id
CompanyId
CreatedAt
UpdatedAt
CreatedByUserId
UpdatedByUserId
```

Para remoção lógica, quando necessário:

```text
DeletedAt
DeletedByUserId
```

## Tecnologias Fora da Arquitetura Inicial

Não serão usadas inicialmente:

- microsserviços;
- Kafka;
- Kubernetes;
- Redis;
- CQRS;
- event sourcing;
- banco separado por tenant.

Essas tecnologias não são necessárias para resolver o problema inicial e aumentariam a complexidade do projeto.






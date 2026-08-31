# Plataforma SaaS para Gestão Integrada de Estoque e Finanças

Este repositório contém o projeto de Trabalho de Conclusão de Curso em Sistemas de Informação com o tema:

**Desenvolvimento de uma plataforma SaaS para gestão integrada de estoque e finanças com análise de dados aplicada à tomada de decisão em pequenas empresas.**

O objetivo é desenvolver uma solução acadêmica robusta, organizada e próxima de um produto real, priorizando simplicidade, boas práticas e clareza arquitetural.

## Objetivo do Produto

A plataforma tem como finalidade apoiar pequenas empresas na gestão integrada de:

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

Neste momento, o projeto está em fase de planejamento técnico e documentação inicial. Nenhuma implementação do sistema foi realizada ainda.

# Roadmap

Este documento apresenta o roadmap inicial de desenvolvimento do projeto.

O objetivo é organizar a construção em etapas, evitando que o escopo fique grande demais para o contexto de um TCC.

## Etapa 1 - Planejamento e Documentação Inicial

Objetivo: definir a base conceitual do projeto.

Entregas:

- visão do produto;
- requisitos;
- arquitetura;
- modelo de domínio;
- regras de negócio;
- roadmap;
- orientações para agentes e colaboradores.

Status: em andamento.

## Etapa 2 - Estrutura Base do Projeto

Objetivo: criar a estrutura inicial do backend, frontend e banco de dados.

Entregas previstas:

- solução backend em ASP.NET Core Web API;
- estrutura modular inicial;
- projeto frontend com React e TypeScript;
- configuração inicial do PostgreSQL;
- configuração do Entity Framework Core;
- configuração inicial do TanStack Query;
- estrutura de pastas alinhada à arquitetura.

## Etapa 3 - Autenticação e Multi-Tenancy

Objetivo: implementar a base de segurança e isolamento por empresa.

Entregas previstas:

- cadastro de usuários;
- login;
- geração e validação de JWT;
- cadastro de empresas;
- vínculo usuário-empresa;
- papéis por empresa;
- definição de empresa atual;
- validação de acesso por `CompanyId`;
- testes de isolamento multi-tenant.

Essa etapa é crítica porque sustenta todo o modelo SaaS.

## Etapa 4 - Produtos e Fornecedores

Objetivo: implementar os cadastros operacionais iniciais.

Entregas previstas:

- cadastro de produtos;
- edição de produtos;
- inativação de produtos;
- categorias de produto;
- unidades de medida;
- cadastro de fornecedores;
- edição de fornecedores;
- inativação de fornecedores.

## Etapa 5 - Estoque

Objetivo: implementar o controle de estoque com histórico.

Entregas previstas:

- entrada de estoque;
- saída de estoque;
- ajuste de estoque;
- cálculo de saldo;
- histórico de movimentações;
- bloqueio de estoque negativo;
- alerta de estoque mínimo.

## Etapa 6 - Financeiro

Objetivo: implementar a gestão financeira básica.

Entregas previstas:

- cadastro de receitas;
- cadastro de despesas;
- contas a pagar;
- contas a receber;
- registro de pagamentos;
- registro de recebimentos;
- status financeiros;
- fluxo de caixa simples.

## Etapa 7 - Dashboard

Objetivo: apresentar indicadores consolidados.

Entregas previstas:

- resumo de estoque;
- resumo financeiro;
- produtos abaixo do estoque mínimo;
- contas vencidas;
- receitas e despesas por período;
- saldo previsto.

## Etapa 8 - Insights

Objetivo: gerar recomendações simples baseadas em dados.

Entregas previstas:

- alerta de reposição de estoque;
- alerta de contas vencidas;
- alerta de risco de caixa;
- identificação de despesas acima da média;
- apresentação dos insights no dashboard.

## Etapa 9 - Testes e Qualidade

Objetivo: aumentar confiabilidade do sistema.

Entregas previstas:

- testes de regras de estoque;
- testes de regras financeiras;
- testes de multi-tenancy;
- testes de autenticação e autorização;
- testes de endpoints principais;
- revisão de organização do código.

## Etapa 10 - Documentação Final do TCC

Objetivo: consolidar o projeto como entrega acadêmica.

Entregas previstas:

- descrição da arquitetura;
- justificativa das decisões técnicas;
- modelo de dados final;
- evidências de testes;
- telas do sistema;
- limitações conhecidas;
- trabalhos futuros.

## Prioridade Geral

A prioridade do desenvolvimento deve seguir esta ordem:

1. Segurança e multi-tenancy.
2. Cadastros essenciais.
3. Estoque.
4. Financeiro.
5. Dashboard.
6. Insights.
7. Testes e refinamentos.

Essa ordem evita construir telas ou indicadores antes de existir uma base confiável de dados.

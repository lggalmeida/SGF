# Regras de Negócio

Este documento registra as regras de negócio iniciais do MVP do **SGF - Sistema de Gestão Facilitada**.

As regras poderão ser refinadas conforme o projeto evoluir.

## Multi-Tenancy

RN01 - Todo dado de negócio deve pertencer a uma empresa.

RN02 - O usuário só pode acessar dados de empresas às quais está vinculado.

RN03 - Todas as consultas de entidades de negócio devem considerar o `CompanyId`.

RN04 - Todas as operações de criação, edição, consulta e remoção devem validar o contexto da empresa atual.

RN05 - O sistema não deve permitir que um registro de uma empresa seja associado a registros de outra empresa.

Exemplo: uma movimentação de estoque da Empresa A não pode referenciar um produto da Empresa B.

## Usuários e Permissões

RN06 - Um usuário pode participar de mais de uma empresa.

RN07 - O papel do usuário é definido por empresa.

RN08 - O papel `Owner` deve possuir acesso administrativo completo à empresa.

RN09 - O papel `Admin` deve poder gerenciar cadastros e operações principais.

RN10 - O papel `Operator` deve poder registrar operações do dia a dia, como movimentações de estoque.

RN11 - O papel `Viewer` deve possuir acesso apenas de leitura.

## Produtos

RN12 - Todo produto deve possuir nome.

RN13 - Todo produto deve pertencer a uma empresa.

RN14 - Produto inativo não deve ser usado em novas movimentações operacionais.

RN15 - Produto pode possuir estoque mínimo para geração de alertas.

RN16 - Produto pode possuir custo e preço de venda.

## Fornecedores

RN17 - Todo fornecedor deve pertencer a uma empresa.

RN18 - Fornecedor inativo não deve ser usado em novas operações.

RN19 - Fornecedor pode estar associado a entradas de estoque, despesas ou contas a pagar.

## Estoque

RN20 - Toda alteração de estoque deve gerar uma movimentação.

RN21 - Entrada de estoque aumenta o saldo do produto.

RN22 - Saída de estoque diminui o saldo do produto.

RN23 - Ajuste de estoque pode aumentar ou diminuir o saldo do produto.

RN24 - O saldo de estoque não deve ser alterado diretamente sem histórico.

RN25 - O sistema não deve permitir estoque negativo no MVP.

RN26 - Movimentações de estoque devem registrar data, produto, quantidade, tipo e usuário responsável.

RN27 - Produtos com saldo abaixo do estoque mínimo devem gerar alerta ou aparecer no dashboard.

## Financeiro

RN28 - Toda receita deve pertencer a uma empresa.

RN29 - Toda despesa deve pertencer a uma empresa.

RN30 - Conta a pagar pode estar pendente, paga, vencida ou cancelada.

RN31 - Conta a receber pode estar pendente, recebida, vencida ou cancelada.

RN32 - Uma conta paga não deve ser paga novamente.

RN33 - Uma conta recebida não deve ser recebida novamente.

RN34 - Pagamentos e recebimentos devem registrar data e valor.

RN35 - O fluxo de caixa deve considerar entradas e saídas financeiras.

RN36 - Contas vencidas devem aparecer em indicadores ou alertas.

## Dashboard

RN37 - Indicadores devem ser calculados a partir dos dados reais do sistema.

RN38 - No MVP, indicadores não precisam ser armazenados historicamente.

RN39 - O dashboard deve apresentar visão resumida de estoque e financeiro.

Indicadores iniciais:

- total de produtos;
- produtos abaixo do estoque mínimo;
- total de receitas no período;
- total de despesas no período;
- saldo previsto;
- contas a pagar vencidas;
- contas a receber vencidas.

## Insights

RN40 - Insights devem ser explicáveis ao usuário.

RN41 - Insights iniciais devem ser baseados em regras simples.

RN42 - O sistema não deve depender de inteligência artificial generativa para gerar os primeiros insights.

Exemplos de regras de insight:

- se o saldo do produto for menor que o estoque mínimo, gerar alerta de reposição;
- se uma conta estiver vencida, gerar alerta financeiro;
- se as despesas do mês forem maiores que as receitas, alertar risco financeiro;
- se uma despesa estiver muito acima da média histórica, indicar possível anomalia.

## Auditoria Básica

RN43 - Registros importantes devem possuir data de criação.

RN44 - Registros importantes devem possuir data de atualização.

RN45 - Quando possível, registrar o usuário responsável pela criação ou alteração.

RN46 - Movimentações críticas, como estoque e pagamentos, devem preservar histórico.

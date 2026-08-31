# Requisitos

Este documento descreve os requisitos iniciais do MVP da plataforma.

## Requisitos Funcionais

### Identidade e Acesso

RF01 - O sistema deve permitir o cadastro de usuários.

RF02 - O sistema deve permitir autenticação de usuários.

RF03 - O sistema deve permitir que um usuário esteja vinculado a uma ou mais empresas.

RF04 - O sistema deve permitir que o papel do usuário seja definido por empresa.

RF05 - O sistema deve restringir o acesso às funcionalidades conforme o papel do usuário.

### Empresas e Multi-Tenancy

RF06 - O sistema deve permitir o cadastro de empresas.

RF07 - O sistema deve isolar os dados por empresa.

RF08 - O sistema deve permitir que o usuário trabalhe no contexto de uma empresa atual.

RF09 - O sistema não deve permitir que um usuário acesse dados de empresas às quais não pertence.

### Produtos

RF10 - O sistema deve permitir o cadastro de produtos.

RF11 - O sistema deve permitir a edição de produtos.

RF12 - O sistema deve permitir a inativação de produtos.

RF13 - O sistema deve permitir a organização de produtos por categoria.

RF14 - O sistema deve permitir definir unidade de medida, custo, preço de venda e estoque mínimo.

### Fornecedores

RF15 - O sistema deve permitir o cadastro de fornecedores.

RF16 - O sistema deve permitir a edição de fornecedores.

RF17 - O sistema deve permitir a inativação de fornecedores.

### Estoque

RF18 - O sistema deve permitir registrar entrada de estoque.

RF19 - O sistema deve permitir registrar saída de estoque.

RF20 - O sistema deve permitir registrar ajustes de estoque.

RF21 - O sistema deve manter histórico das movimentações de estoque.

RF22 - O sistema deve calcular o saldo atual de estoque por produto.

RF23 - O sistema deve identificar produtos com estoque abaixo do mínimo definido.

### Financeiro

RF24 - O sistema deve permitir cadastrar receitas.

RF25 - O sistema deve permitir cadastrar despesas.

RF26 - O sistema deve permitir cadastrar contas a pagar.

RF27 - O sistema deve permitir cadastrar contas a receber.

RF28 - O sistema deve permitir registrar pagamento de uma conta a pagar.

RF29 - O sistema deve permitir registrar recebimento de uma conta a receber.

RF30 - O sistema deve apresentar um fluxo de caixa simples.

### Dashboard e Indicadores

RF31 - O sistema deve apresentar indicadores básicos de estoque.

RF32 - O sistema deve apresentar indicadores básicos financeiros.

RF33 - O sistema deve apresentar produtos com baixo estoque.

RF34 - O sistema deve apresentar contas vencidas ou próximas do vencimento.

### Insights

RF35 - O sistema deve gerar insights simples com base em regras de negócio.

RF36 - O sistema deve apresentar recomendações explicáveis ao usuário.

Exemplos de insights:

- produto abaixo do estoque mínimo;
- despesa acima da média;
- conta vencida;
- possível risco de falta de caixa;
- queda de receita em relação a período anterior.

## Requisitos Não Funcionais

RNF01 - O backend deve ser desenvolvido em C# com ASP.NET Core Web API.

RNF02 - O acesso a dados deve utilizar Entity Framework Core.

RNF03 - O banco de dados deve ser PostgreSQL.

RNF04 - O frontend deve ser desenvolvido com React e TypeScript.

RNF05 - O frontend deve utilizar TanStack Query para gerenciamento de dados assíncronos.

RNF06 - A arquitetura deve seguir o modelo de monólito modular.

RNF07 - O sistema deve ser multi-tenant com isolamento por `CompanyId`.

RNF08 - O sistema deve possuir autenticação baseada em JWT.

RNF09 - O sistema deve aplicar autorização conforme vínculo do usuário com a empresa.

RNF10 - O sistema deve priorizar clareza, manutenibilidade e simplicidade.

RNF11 - Regras críticas devem possuir testes automatizados.

RNF12 - O sistema deve evitar dependências e tecnologias que não sejam necessárias ao escopo do MVP.

## Requisitos de Segurança

RS01 - Senhas não devem ser armazenadas em texto puro.

RS02 - Usuários autenticados só podem acessar empresas às quais estão vinculados.

RS03 - Todas as operações de negócio devem respeitar o `CompanyId`.

RS04 - O backend deve validar permissões, mesmo que o frontend oculte funcionalidades.

RS05 - Tokens de autenticação devem ter tempo de expiração.

## Requisitos Acadêmicos

RA01 - O projeto deve documentar as decisões arquiteturais relevantes.

RA02 - O projeto deve explicar as principais escolhas técnicas.

RA03 - O projeto deve manter documentação atualizada durante a evolução do sistema.

RA04 - O projeto deve demonstrar relação entre problema, solução, modelagem e implementação.

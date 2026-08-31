# Modelo de Domínio

Este documento descreve o modelo inicial de domínio do **SGF - Sistema de Gestão Facilitada**.

O modelo ainda é conceitual e poderá ser refinado durante a implementação.

## Conceitos Principais

### Company

Representa uma empresa cadastrada na plataforma.

Cada empresa é um tenant do sistema, ou seja, uma unidade isolada de dados.

Relacionamentos:

- possui usuários vinculados;
- possui produtos;
- possui fornecedores;
- possui movimentações de estoque;
- possui registros financeiros.

### User

Representa uma pessoa que acessa o sistema.

Um usuário pode pertencer a uma ou mais empresas.

Relacionamentos:

- pode estar vinculado a várias empresas por meio de `UserCompany`.

### UserCompany

Representa o vínculo entre um usuário e uma empresa.

Esse relacionamento define qual papel o usuário possui dentro da empresa.

Campos conceituais:

```text
UserId
CompanyId
Role
```

Papéis iniciais:

- `Owner`;
- `Admin`;
- `Operator`;
- `Viewer`.

### Product

Representa um produto controlado pela empresa.

Campos conceituais:

```text
Id
CompanyId
Name
Sku
CategoryId
UnitOfMeasureId
CostPrice
SalePrice
MinimumStock
IsActive
```

Relacionamentos:

- pertence a uma empresa;
- pertence a uma categoria;
- possui movimentações de estoque.

### ProductCategory

Representa a categoria de um produto.

Exemplos:

- Bebidas;
- Alimentos;
- Material de escritório;
- Serviços.

### UnitOfMeasure

Representa a unidade de medida de um produto.

Exemplos:

- unidade;
- caixa;
- pacote;
- quilo;
- litro.

### Supplier

Representa um fornecedor da empresa.

Campos conceituais:

```text
Id
CompanyId
Name
Document
Email
Phone
IsActive
```

Relacionamentos:

- pertence a uma empresa;
- pode estar associado a movimentações de entrada;
- pode estar associado a despesas ou contas a pagar.

### StockMovement

Representa uma movimentação de estoque.

Toda alteração no estoque deve gerar uma movimentação.

Campos conceituais:

```text
Id
CompanyId
ProductId
SupplierId
Type
Quantity
UnitCost
OccurredAt
Reason
CreatedByUserId
```

Tipos iniciais:

- entrada;
- saída;
- ajuste.

Relacionamentos:

- pertence a uma empresa;
- pertence a um produto;
- pode estar associada a um fornecedor.

### StockBalance

Representa o saldo atual de um produto em estoque.

Pode ser implementado de duas formas:

- calculado a partir das movimentações;
- armazenado em uma tabela própria e atualizado a cada movimentação.

Decisão inicial recomendada: manter o saldo de forma controlada no sistema, sempre atualizado por movimentações, e nunca alterar saldo sem histórico.

### Revenue

Representa uma receita da empresa.

Campos conceituais:

```text
Id
CompanyId
Description
Amount
DueDate
ReceivedAt
Status
```

### Expense

Representa uma despesa da empresa.

Campos conceituais:

```text
Id
CompanyId
Description
Amount
DueDate
PaidAt
Status
SupplierId
```

### AccountPayable

Representa uma obrigação financeira que a empresa precisa pagar.

Status iniciais:

- pendente;
- paga;
- vencida;
- cancelada.

### AccountReceivable

Representa um valor que a empresa tem a receber.

Status iniciais:

- pendente;
- recebida;
- vencida;
- cancelada.

### Payment

Representa o pagamento ou recebimento associado a uma conta.

Esse conceito poderá ser refinado durante a implementação financeira.

### Insight

Representa um alerta ou recomendação gerada a partir de dados do sistema.

Exemplos:

- produto abaixo do estoque mínimo;
- conta vencida;
- despesa acima da média;
- risco de caixa negativo.

## Relacionamentos Iniciais

Modelo simplificado:

```text
Company 1 -> N Product
Company 1 -> N Supplier
Company 1 -> N StockMovement
Company 1 -> N Revenue
Company 1 -> N Expense
Company 1 -> N AccountPayable
Company 1 -> N AccountReceivable

User N -> N Company, por meio de UserCompany

Product 1 -> N StockMovement
Supplier 1 -> N StockMovement
Supplier 1 -> N Expense
```

## Regra Estrutural Mais Importante

Quase toda entidade de negócio deve possuir `CompanyId`.

Essa regra é essencial para garantir o isolamento entre empresas no modelo SaaS.

Exemplos de entidades com `CompanyId`:

- Product;
- Supplier;
- StockMovement;
- Revenue;
- Expense;
- AccountPayable;
- AccountReceivable;
- Insight.

Entidades globais ou técnicas, como `User`, podem não possuir `CompanyId`, pois um usuário pode estar vinculado a várias empresas.

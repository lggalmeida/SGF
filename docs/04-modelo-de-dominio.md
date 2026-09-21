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
- possui movimentações de estoque;
- possui registros financeiros.

### User

Representa uma pessoa que acessa o sistema.

Um usuário pode pertencer a uma ou mais empresas.

Relacionamentos:

- pode estar vinculado a várias empresas por meio de `Membership`.

### Membership

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
- `Member`.

### Product

Representa um produto controlado pela empresa.

Campos conceituais:

```text
Id
CompanyId
Name
Sku
Description
CostPrice
SalePrice
CurrentStock
MinimumStock
IsActive
CreatedAt
UpdatedAt
```

Relacionamentos:

- pertence a uma empresa;
- implementa ICompanyScopedEntity;
- na Fase J, possui saldo e mínimo numeric(14,3), inicialmente zero, e movimentações;
- categorias e unidades permanecem futuras.

### ProductCategory (futuro)

Representa a categoria de um produto.

Exemplos:

- Bebidas;
- Alimentos;
- Material de escritório;
- Serviços.

### UnitOfMeasure (futuro)

Representa a unidade de medida de um produto.

Exemplos:

- unidade;
- caixa;
- pacote;
- quilo;
- litro.

### Supplier (futuro)

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

### InventoryMovement (Fase J)

Representa uma movimentação de estoque.

Toda alteração no estoque deve gerar uma movimentação.

Campos conceituais:

```text
Id
CompanyId
ProductId
Type
Quantity
Notes
CreatedAt
UserId
```

Tipos iniciais:

- Entry (entrada);
- Exit (saída).

Relacionamentos:

- pertence a uma empresa;
- pertence a um produto;
- registra o usuário responsável, sem fornecedor ou custo nesta fase;
- vínculo composto (CompanyId, ProductId) impede associação a produto de outra empresa;
- histórico imutável pela API e por SaveChanges; não há edição ou exclusão comum.

### Saldo Atual (Fase J)

Representa o saldo atual de um produto em estoque.

É armazenado em Product.CurrentStock, sem tabela StockBalance. Saldo e movimento
são persistidos juntos em uma transação. CurrentStock é token de concorrência;
MinimumStock pode ser editado, mas CurrentStock não é aceito pela edição de produto.
Detalhes: [Estoque](11-estoque.md).

### FinancialEntry (Fase K)

Representa receita, despesa, conta a receber ou conta a pagar em uma única entidade.
Substitui os conceitos separados Revenue, Expense, AccountPayable, AccountReceivable
e Payment do planejamento inicial, sem tabelas redundantes.

Campos implementados:

```text
Id
CompanyId
Type (Income / Expense)
Description
Category (opcional)
Amount
DueDate
PaidAt
Status (Pending / Paid)
Notes (opcional)
CreatedAt
UpdatedAt
Version (controle interno de concorrência)
```

Implementa ICompanyScopedEntity e pertence a Company. Amount usa numeric(14,2),
DueDate usa date, timestamps usam UTC. Pending + Income representa conta a receber;
Pending + Expense representa conta a pagar. Não há fornecedor, vínculo com estoque,
exclusão ou reversão nesta fase. Detalhes: [Financeiro](12-financeiro.md).

### Insight

Representa um alerta ou recomendação gerada a partir de dados do sistema.

Na Fase L é um resultado calculado, não uma entidade persistida. Code, Level,
Message e Evidence são derivados dos indicadores do tenant atual. Não há tabela
Insight nem necessidade de migration. Ver [Dashboard e Analytics](13-dashboard-analytics.md).

Exemplos:

- produto abaixo do estoque mínimo;
- conta vencida;
- despesa acima da média;
- risco de caixa negativo.

## Relacionamentos Iniciais

Modelo simplificado:

```text
Company 1 -> N Product
Company 1 -> N InventoryMovement
Company 1 -> N FinancialEntry

User N -> N Company, por meio de Membership

Product 1 -> N InventoryMovement
User 1 -> N InventoryMovement
```

## Regra Estrutural Mais Importante

Quase toda entidade de negócio deve possuir `CompanyId`.

Essa regra é essencial para garantir o isolamento entre empresas no modelo SaaS.

Exemplos de entidades com `CompanyId`:

- Product;
- InventoryMovement;
- FinancialEntry.

FinancialEntry também é fonte das agregações analíticas. Insights são resultados derivados e não entidades persistidas.

Entidades globais ou técnicas, como `User`, podem não possuir `CompanyId`, pois um usuário pode estar vinculado a várias empresas.






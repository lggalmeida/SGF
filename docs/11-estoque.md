# Estoque - Fase J

## Modelo e persistencia

Product possui CurrentStock e MinimumStock, ambos inicialmente zero e numeric(14,3):
11 digitos inteiros e 3 decimais, ate 99.999.999.999,999. A API rejeita fracao excedente,
em vez de depender de arredondamento do banco. Minimo pode ser editado; saldo somente
muda por movimentacao. Estoque baixo significa produto ativo com saldo <= minimo.

InventoryMovement preserva Id, CompanyId, ProductId, Type (Entry/Exit), Quantity,
Notes opcional, CreatedAt UTC e UserId. Quantidade positiva, observacao ate 1.000
caracteres. Nao existem endpoints de edicao/exclusao; SaveChanges rejeita alteracao
ou exclusao do historico. Nomes de produto e usuario exibidos sao os atuais;
identificadores, quantidade, tipo e instante do evento permanecem preservados.

Migration: `20260919203320_AddInventory`. Produtos existentes recebem saldo/minimo
zero. FK composta (CompanyId, ProductId) aponta para Products(CompanyId, Id);
FK UserId aponta para Identity. Exclusoes relacionadas sao restritas para preservar
historico. Checks impedem saldo/minimo negativos, quantidade nao positiva e tipo invalido.

Indices CompanyId + CreatedAt e CompanyId + ProductId + CreatedAt atendem historico
geral e por produto. O indice UserId suporta a FK. Nao ha tabelas de alertas ou saldo.

## Atomicidade e concorrencia

InventoryService resolve o contexto confiavel, carrega o produto pelo filtro global,
valida atividade/quantidade/saldo e registra o movimento no dominio. Um SaveChanges
persiste saldo e movimento usando a transacao nativa do EF Core.

CurrentStock e IsActive sao concurrency tokens, junto a CompanyId da protecao F.1.
O UPDATE tem conceitualmente este predicado:

```sql
WHERE Id = @id AND CompanyId = @company
  AND CurrentStock = @saldoOriginal AND IsActive = @ativoOriginal
```

Duas saidas de 4 lendo saldo 5 nao vencem juntas: a primeira grava 1; a segunda
nao encontra a versao original e causa DbUpdateConcurrencyException. Toda a segunda
operacao e revertida e retorna 409. Se ja leu saldo 1, retorna estoque insuficiente.
Nao ha repeticao automatica de operacoes de negocio. Desativacao concorrente tambem
impede movimentacao baseada em produto anteriormente ativo.

Filtros globais isolam leituras; protecao de escrita e FK composta isolam persistencia.
CompanyId e UserId nao sao obtidos do request. Sem contexto valido nao ha operacao.
SQL administrativo/bulk nao passa por SaveChanges e continua exigindo revisao excepcional.

## API

Todas as rotas exigem autenticacao e contexto de tenant valido.

- GET /api/inventory: page, pageSize, search e lowStock.
- GET /api/inventory/movements: page, pageSize, productId e type (Entry/Exit).
- POST /api/inventory/entries: productId, quantity, notes opcional.
- POST /api/inventory/exits: mesmo contrato.

Pagina inicial 1, tamanho padrao 20 e maximo 100. Historico ordenado por CreatedAt
decrescente e Id como desempate. Produto de outro tenant se comporta como inexistente.
POST retorna 201 com Id, ProductId, Type, Quantity e CurrentStock. Validacao retorna
400; produto inexistente 404; inativo, saldo insuficiente, limite ou concorrencia 409.
Erros usam ProblemDetails sem SQL ou stack trace.

## Interface e validacao

/app/inventory possui Estoque e Movimentacoes. Busca, filtros e pagina ficam na URL;
F5 restaura sessao e visualizacao. Entrada/saida usam dialogo com saldo atualizado,
quantidade e observacao. TanStack Query invalida estoque, historico e produtos apos
sucesso. Em mobile, tabelas se adaptam a listas. Minimo fica no formulario de produto.

Testes de integracao usam PostgreSQL e migrations em bancos temporarios: quantidades,
saldo, historico, tenant, imutabilidade, rollback por falha real, saidas HTTP simultaneas
e conflito deterministico com dois DbContexts. Playwright valida fluxo 10 - 3 = 7,
rejeicao de 8, erro com saldo desatualizado, filtros, F5 e telas 1440/390px.

Aplicar todas as migrations com o comando do README; nao ha nova dependencia.
Em perda de rede, conferir historico antes de repetir: a gravacao pode ter ocorrido.
Nao ha chave de idempotencia nesta fase, nem ajuste/compensacao, deposito, custo medio,
compras, vendas ou integracao financeira. Publicacao segue as pendencias do README.

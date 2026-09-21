# Financeiro - Fase K

## Modelo e decisoes

Uma FinancialEntry representa receita/despesa e sua pendencia ou realizacao.
Nao criamos entidades separadas para contas ou pagamentos integrais.
Type: Income/Expense; Status: Pending/Paid. Valores sao positivos; Type define
o sinal no saldo. Categoria e texto opcional ate 100 caracteres, sem cadastro
auxiliar; filtro exato ignora maiusculas e espacos externos.

Description ate 200, Notes ate 2000. Amount numeric(14,2) admite 12 inteiros e
2 decimais; fracao excedente e rejeitada, nao arredondada silenciosamente.
DueDate e DateOnly/date (sem fuso). PaidAt, CreatedAt e UpdatedAt sao UTC.
Nao ha data de pagamento escolhida pelo cliente; confirmacao usa o instante atual.

## Persistencia e concorrencia

Migration: 20260919211207_AddFinancialEntries. Cria somente FinancialEntries,
PK Id, FK CompanyId -> Companies com Restrict, checks para descricao, valor
positivo, tipo valido e consistencia Status/PaidAt. Indice CompanyId + DueDate
atende tenant, intervalo e ordenacao. Nao adicionamos indices especulativos
para Type/Status, que inicialmente possuem poucos valores distintos.

Version e um Guid interno renovado em cada edicao/pagamento. Nao e um token JWT
nem vai para o cliente. EF inclui Version original e CompanyId no UPDATE:

```sql
WHERE Id = @id AND CompanyId = @company AND Version = @originalVersion
```

Isso impede edicao e pagamento concorrentes de sobrescreverem um ao outro.
Conflito gera 409. Pagamento repetido em registro Paid retorna o mesmo estado.
Se dois pagamentos concorrerem, o perdedor consulta o estado confirmado e o
retorna se ja estiver Paid; nao repete uma escrita nem duplica valores.
Cada operacao usa SaveChanges nativo. Nao ha tabela acumuladora de saldo.

Domain guarda validacoes e transicoes; Application define contratos;
Infrastructure implementa IFinanceService com EF; Api traduz erros.
Global Query Filters e protecao de escrita F.1 sao reutilizados. Contexto deve
ser resolvido antes de acessar dados. IDs de outro tenant retornam 404.
Sem tenant valido nao ha acesso. Nenhum request define CompanyId.

## API

- GET /api/finance: page (1), pageSize (20, maximo 100), search por descricao,
  type, status, category, from/to inclusivos por DueDate.
- GET /api/finance/{id}: detalhes.
- POST /api/finance: type, description, category, amount, dueDate, notes; retorna 201.
- PUT /api/finance/{id}: description, category, amount, dueDate, notes; somente Pending.
- PATCH /api/finance/{id}/pay: sem body; confirma pagamento/recebimento integral.
- GET /api/finance/summary: received, paid, receivable, payable, balance.

Listagem ordenada por DueDate crescente, CreatedAt decrescente e Id para desempate.
Contagem e itens sao consultas separadas; paginacao nao e snapshot transacional.
400 para validacao, 404 inexistente/outro tenant, 409 pago nao editavel/conflito,
401 sem JWT, 403 contexto invalido. Falhas internas retornam erro generico.

Summary utiliza uma unica consulta agregada sobre todos os registros da empresa.
Received/Paid somam somente Paid; Receivable/Payable somente Pending.
Balance = Received - Paid, sem saldo inicial ou integracao bancaria.
Os filtros da tabela nao afetam os cards de totais gerais. DueDate nao e usado
como data de realizacao; PaidAt preserva a base para evolucao mensal futura.

## Frontend e testes

/app/finance reutiliza AppLayout, sessao e cliente HTTP existentes.
FinancePage contem totais, filtros na URL, tabela/lista mobile e confirmacao de
pagamento. FinanceForm usa dialog nativo para criar/editar e consultar pagos.
TanStack Query invalida lista e summary apos gravacao, sem F5. Nao ha novas
dependencias, gerenciador de estado ou design system.

Testes PostgreSQL cobrem regras, pagamentos repetidos/concorrentes, conflito
edicao/pagamento, filtros, paginacao, saldo e isolamento. A factory aplica todas
as migrations em banco temporario do zero e o remove ao terminar.
Playwright cobre receita 1000, despesa 300, saldo 700, validacao, edicao,
pagamento, filtros, F5 e responsividade. Capturas em test-results, fora do Git.
Execucao e migrations seguem o README.

## Limites

Nao ha reversao, exclusao, parcelas, recorrencia, integracao com estoque, bancos
ou analytics. Lancamentos pagos nao sao editaveis pela API. A versao protege
concorrencia entre leitura e gravacao no backend; nao e controle de versao de
formularios abertos por longos periodos.
Criacao nao possui chave de idempotencia: em falha de rede, conferir a lista antes
de repetir. Pagamento do mesmo ID e idempotente. Mutations nao repetem
automaticamente apos erros de rede.
Pendencias de publicacao (HTTPS, segredos, rate limiting) seguem o README.

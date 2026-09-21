# Dashboard e Analytics - Fase L

## Finalidade academica

O SGF transforma registros operacionais em informacoes gerenciais explicaveis:

```text
Product / InventoryMovement / FinancialEntry
  -> filtros de empresa e periodo
  -> SUM, COUNT, GROUP BY e verificacao de ausencia de movimentos
  -> indicadores, comparacoes e listas
  -> regras deterministicas de insights
  -> apoio ao usuario na tomada de decisao
```

O sistema nao toma decisoes pelo empresario, nao usa machine learning e nao
preve demanda, lucro ou inadimplencia. Os resultados dependem da qualidade
e completude dos registros inseridos pelos usuarios.

## Periodos e referencia temporal

GET /api/dashboard aceita period:

| Chave | Intervalo |
| --- | --- |
| last30 (padrao) | Hoje e os 29 dias anteriores |
| currentMonth | Primeiro dia do mes ate hoje |
| previousMonth | Mes anterior completo |
| last90 | Hoje e os 89 dias anteriores |

Dias usam referencia fixa UTC-03:00, independente do fuso do servidor/browser.
Essa escolha simples atende a demonstracao local, mas nao representa todos os
fusos brasileiros nem regras historicas de horario de verao. Futuro suporte
regional exigira uma decisao explicita de fuso por empresa.

Datas From/To sao inclusivas na resposta. Nas consultas, intervalos sao
[inicio UTC, inicio do dia seguinte ao fim UTC), evitando perda de fracao de segundo.
PaidAt determina realizacao financeira, nao DueDate ou CreatedAt.
CreatedAt determina o periodo de movimentacoes. Eventos posteriores a AsOf
nao entram no periodo atual. Periodos que incluem hoje ainda estao em andamento.

Pendentes, vencidos, produtos ativos e saldo de estoque refletem o estado atual,
nao um snapshot historico do periodo escolhido. Vencido significa DueDate < hoje;
contas vencendo hoje nao sao vencidas. Pendentes incluem todos os vencimentos.

## Comparacao

O intervalo anterior e imediatamente anterior e tem o mesmo numero de dias
de calendario do intervalo selecionado. A API retorna ambas as datas e a tela
as apresenta; nao presumir que esse intervalo e sempre o mes calendario anterior.

Exemplo: 1 a 15 de setembro compara com 17 a 31 de agosto (15 dias cada).
Um mes anterior completo tambem compara com uma janela de igual duracao,
nao necessariamente com outro mes completo.

```text
variacao (%) = (valor atual - valor anterior) / valor anterior * 100
```

Percentual com uma casa decimal. Base anterior zero retorna null e a interface
mostra "Sem comparacao disponivel", inclusive quando atual tambem e zero.
Base positiva e atual zero resulta em -100%. Nao se afirma causalidade.
Hoje incompleto e registros faltantes podem afetar a comparacao; ela mede apenas
o que foi registrado. A variacao nao e uma previsao.

## Indicadores e formulas

| Indicador | Fonte e calculo |
| --- | --- |
| Receitas recebidas | SUM Amount, FinancialEntry Income/Paid, PaidAt no periodo |
| Despesas pagas | SUM Amount, Expense/Paid, PaidAt no periodo |
| Saldo realizado | Recebido - pago no periodo |
| A receber | SUM Amount, Income/Pending, todos os vencimentos |
| A pagar | SUM Amount, Expense/Pending, todos os vencimentos |
| A receber vencido | Income/Pending com DueDate anterior a hoje |
| A pagar vencido | Expense/Pending com DueDate anterior a hoje |
| Quantidade vencida | COUNT dos pendentes vencidos, ambos os tipos |
| Produtos ativos | COUNT Product IsActive |
| Estoque baixo | Ativos com CurrentStock <= MinimumStock |
| Sem estoque | Ativos com CurrentStock = 0 |
| Valor estimado a custo | SUM CurrentStock * CostPrice dos ativos |
| Entradas/saidas | SUM Quantity por Type, InventoryMovement no periodo |
| Movimentacoes | COUNT InventoryMovement no periodo |
| Maior saida | GROUP BY produto, SUM Quantity em Exit, ordem decrescente |
| Sem movimento ha 30 dias | Ativos criados ha pelo menos 30 dias sem Entry/Exit nos ultimos 30 dias |

Estoque baixo inclui zerados, inclusive quando minimo e zero. Nao somar esses
indicadores como categorias exclusivas. Valor estimado usa custo cadastrado atual,
nao custo medio/FIFO nem valor contabil oficial. Saldo financeiro nao e saldo bancario,
faturamento fiscal ou lucro. Saida fisica nao e necessariamente venda.

Produtos inativos nao entram no estado atual do estoque, mas seus movimentos
historicos continuam nos totais e ranking do periodo. Nomes e SKUs sao os atuais.
Produtos recem-criados nao recebem prematuramente o alerta de 30 dias.
Produto antigo sem qualquer movimento e incluido, com LastMovementAt null.
A janela de inatividade e de 30 dias corridos ate AsOf e independe do seletor.

## Serie e listas

Serie financeira diaria para intervalos de ate 31 dias; mensal acima disso.
Somente dias pertencentes ao intervalo entram nos meses parciais das extremidades.
O PostgreSQL agrega e o backend preenche os poucos intervalos sem dados com zero.
Soma da serie deve coincidir com recebido/pago dos cards.

Listas limitadas a 10 registros:

- Estoque baixo: zerados primeiro, depois menor saldo menos minimo, nome e ID.
- Maior saida: quantidade decrescente, nome e ID para desempate.
- Sem movimento: sem registro anterior primeiro, depois ultima movimentacao
  mais antiga, nome e ID. A contagem total pode ser maior que a lista.

Sem unidades de medida padronizadas, somar/comparar quantidades de produtos
diferentes e apenas um resumo numerico operacional, nao uma medida economica.
Por isso Curva ABC foi adiada: sem vendas ou bases comparaveis, sua interpretacao
como relevancia financeira seria indevida. Nao ha classificacao ABC nesta fase.

## Insights deterministas

Regras avaliadas nesta ordem, sem persistir uma tabela de insights:

| Codigo | Condicao | Nivel |
| --- | --- | --- |
| zero_stock | Quantidade de ativos zerados > 0 | attention |
| low_stock | Quantidade de ativos no limite/abaixo do minimo > 0 | attention |
| overdue_payable | Total a pagar vencido > 0 | attention |
| overdue_receivable | Total a receber vencido > 0 | attention |
| expense_increase | Aumento de despesas >= 10%, base anterior positiva | attention |
| income_decrease | Queda de recebidos >= 10%, base anterior positiva | attention |
| stale_products | Produtos sem movimento ha 30 dias > 0 | info |
| top_stock_out | Existe saida no periodo | info |

O limiar financeiro e aplicado ao percentual arredondado exibido (uma casa decimal).
Cada resposta inclui Code, Level, Message e Evidence. A interface permite abrir
o criterio; nao sao explicacoes geradas por IA. Regras falsas nao geram mensagens.
Empresa vazia retorna zeros, listas vazias e nenhum insight.

## API, arquitetura e seguranca

Um GET /api/dashboard?period=last30 retorna AsOf, Period, Financial, Comparisons,
FinancialSeries, Inventory, Movements, LowStock, TopStockOut, StaleProducts e Insights.
O mesmo contrato pequeno e limitado atende Visao Geral e Analytics; nao replica
dados operacionais completos nem cria dezenas de endpoints.
401 sem autenticacao, 403 contexto invalido, 400 periodo desconhecido.
Respostas no-store; nenhuma consulta aceita CompanyId livremente.

Application define contratos, periodos e regras puras. Infrastructure implementa
IDashboardService com EF Core; Api possui endpoint fino. TimeProvider nativo
fornece AsOf e pode ser fixado nos testes, sem nova biblioteca.

ICurrentTenantContext e resolvido antes das consultas. Todas as fontes usam os
Global Query Filters existentes, inclusive joins e subconsultas de movimentos.
Nao usamos IgnoreQueryFilters, SQL bruto ou escrita em producao.
Uma transacao curta RepeatableRead mantem os oito blocos de consulta sobre o
mesmo snapshot PostgreSQL, sem cache ou processamento em background.
SUM/COUNT/GROUP BY, ordenacao e Take(10) executam no banco; apenas grupos
agregados e listas limitadas chegam a memoria.

Nenhuma entidade/tabela/indice novo foi necessario. Nao ha migration da Fase L.
Indices existentes com CompanyId atendem o volume atual. Indice adicional em
CompanyId + PaidAt so deve ser considerado com medicao de consultas reais.

## Interface e verificacao

/app e /app/dashboard exibem indicadores, grafico financeiro, reposicao, ranking
e insights. /app/analytics detalha Financeiro, Estoque e Produtos.
Periodo e secao ficam na URL e sobrevivem ao F5. TanStack Query usa empresa e
periodo na chave; ao reabrir a tela, dados obsoletos sao consultados novamente.
O botao Atualizar permite nova leitura sem recarregar a pagina.

Recharts 3.10.1 e a unica dependencia direta adicionada, compativel com React 19.
Somente o grafico financeiro e carregado sob demanda; os demais dados usam
componentes React/CSS existentes. Grafico tem legenda, tooltip, suporte de teclado
da biblioteca e tabela equivalente de valores. Ranking mostra numeros e barras
sem chamar as saidas de vendas. Cores nao sao a unica forma de comunicar estado.

Testes de regras cobrem intervalos, ano bissexto, fuso, base zero e limiares.
Integracao com PostgreSQL verifica totais, series, limites, inatividade, rankings,
insights, listas limitadas e isolamento de todos os blocos entre empresas.
As fixtures aplicam todas as migrations do zero e removem seus bancos.

Playwright valida empresa vazia, empresa com dados, periodos, comparacoes, grafico,
insights, tabs, mobile e F5. Seu setup usa somente a API local e backdating
restrito aos IDs criados no teste via Docker/psql, em sgf_dev. Nao executar essa
fixture contra ambiente remoto. Nao existe seed automatico ou conjunto oficial
da banca; isso continua reservado a Fase M.

Limites: sem previsao, causalidade, sazonalidade, ABC, saldo historico de estoque
ou prova de completude dos registros. Apoia avaliar reposicao, compromissos
vencidos, variacao de desembolsos e produtos sem atividade; a decisao e humana.

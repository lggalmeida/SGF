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

RN10 - O papel `Member` deve representar o acesso operacional inicial de um usuário comum da empresa.

RN11 - Papéis mais específicos, como operador ou visualizador, só devem ser adicionados quando houver necessidade real.

## Produtos

RN12 - Todo produto deve possuir nome.

RN13 - Todo produto deve pertencer a uma empresa.

RN14 - Produto inativo não deve ser usado em novas movimentações operacionais.

RN15 - Produto pode possuir estoque mínimo para geração de alertas.

RN16 - Produto pode possuir custo e preço de venda.

### Núcleo Implementado na Fase I

- Nome obrigatório, até 200 caracteres; espaços nas extremidades são removidos.
- SKU obrigatório, até 64 caracteres, normalizado sem espaços e em maiúsculas.
- SKU é único por empresa, inclusive entre produtos inativos; empresas distintas
  podem utilizar o mesmo SKU.
- Descrição opcional, até 2.000 caracteres.
- Custo e venda obrigatórios, não negativos, até 9.999.999.999,99, com no máximo
  duas casas decimais. Venda abaixo do custo é permitida.
- Empresa e datas são definidas exclusivamente pelo backend; empresa e data de
  criação não podem ser alteradas pelo cliente.
- Produto inicia ativo; desativação e reativação preservam o registro e o SKU.
  Não há exclusão física nesta fase.
- Member, Admin e Owner com vínculo ativo podem gerenciar produtos.
- Estoque mínimo é configurável na Fase J; categorias e unidades permanecem futuras.

Contratos e detalhes: [Produtos](10-produtos.md).

## Fornecedores (trabalho futuro)

RN17 - Todo fornecedor deve pertencer a uma empresa.

RN18 - Fornecedor inativo não deve ser usado em novas operações.

RN19 - Fornecedor pode estar associado a entradas de estoque, despesas ou contas a pagar.

## Estoque

RN20 - Toda alteração de estoque deve gerar uma movimentação.

RN21 - Entrada de estoque aumenta o saldo do produto.

RN22 - Saída de estoque diminui o saldo do produto.

RN23 - Ajustes sofisticados ficam para evolução futura; nesta fase existem apenas Entry e Exit.

RN24 - O saldo de estoque não deve ser alterado diretamente sem histórico.

RN25 - O sistema não deve permitir estoque negativo no MVP.

RN26 - Movimentações de estoque devem registrar data, produto, quantidade, tipo e usuário responsável.

RN27 - Produto ativo com CurrentStock <= MinimumStock aparece como estoque baixo em Estoque e, desde a Fase L, no dashboard, sem tabela de alertas.

### Estoque Implementado na Fase J

- Saldo e mínimo começam em zero, aceitam até três casas decimais e nunca são negativos.
- Quantidade de movimentação deve ser positiva; observação opcional tem até 1.000 caracteres.
- Produto inativo ou de outro tenant não pode ser movimentado.
- Empresa e usuário vêm do contexto autenticado; o cliente fornece somente produto, quantidade e observação.
- Saldo e histórico são gravados atomicamente. Uma falha reverte ambos.
- Saída sem saldo e escrita concorrente conflitante retornam 409 sem criar movimento.
- CurrentStock, IsActive e CompanyId participam do controle de concorrência da escrita do produto.
- Histórico não pode ser editado ou excluído pela API; SaveChanges também rejeita essas operações.
- Edição comum de produto altera mínimo, nunca saldo. Member, Admin e Owner com contexto válido podem movimentar.

Contratos, concorrência e limitações: [Estoque](11-estoque.md).

## Financeiro

RN28 - Toda receita deve pertencer a uma empresa.

RN29 - Toda despesa deve pertencer a uma empresa.

RN30 - Na Fase K, conta a pagar é FinancialEntry Expense, com status Pending ou Paid.

RN31 - Na Fase K, conta a receber é FinancialEntry Income, com status Pending ou Paid.

RN32 - Uma conta paga não deve ser paga novamente.

RN33 - Uma conta recebida não deve ser recebida novamente.

RN34 - Pagamentos e recebimentos devem registrar data e valor.

RN35 - O fluxo de caixa deve considerar entradas e saídas financeiras.

RN36 - Contas vencidas devem aparecer em indicadores ou alertas.

### Financeiro Implementado na Fase K

- Descrição obrigatória até 200 caracteres, categoria opcional até 100 e observação
  opcional até 2.000; espaços externos removidos.
- Valor estritamente positivo, até 999.999.999.999,99, com no máximo duas casas decimais.
- Vencimento obrigatório como data sem horário. Não se presume que vencimento é pagamento.
- Novo lançamento é Pending, sem PaidAt. CompanyId, status e datas de auditoria
  não são definidos pelo cliente.
- Pagamento/recebimento integral muda para Paid e registra PaidAt UTC no servidor.
  Repetição preserva o mesmo registro e instante. Não há pagamento parcial.
- Apenas pendentes podem ser editados; Type não muda depois do cadastro.
  Não há reversão ou exclusão de lançamentos nesta fase.
- Versão interna e CompanyId protegem atualização concorrente e propriedade do registro.
- Saldo realizado = receitas Paid menos despesas Paid. Pendentes compõem apenas
  totais a receber/a pagar. O saldo pode ser negativo e não representa saldo bancário.
- Summary considera todo o histórico da empresa; filtros da tabela não alteram
  os totais gerais. Uma consulta SQL calcula os totais sobre o mesmo snapshot.
- Member, Admin e Owner com contexto válido podem gerenciar o financeiro.
- Estoque não gera lançamentos financeiros. A Fase L passou a derivar alertas e
  indicadores desses dados sem criar acoplamento entre os módulos.

Contratos, índices e limites: [Financeiro](12-financeiro.md).

## Dashboard

RN37 - Indicadores devem ser calculados a partir dos dados reais do sistema.

RN38 - No MVP, indicadores não precisam ser armazenados historicamente.

RN39 - O dashboard deve apresentar visão resumida de estoque e financeiro.

Indicadores iniciais:

- total de produtos;
- produtos abaixo do estoque mínimo;
- total de receitas no período;
- total de despesas no período;
- saldo realizado no período;
- contas a pagar vencidas;
- contas a receber vencidas.

## Insights

RN40 - Insights devem ser explicáveis ao usuário.

RN41 - Insights iniciais devem ser baseados em regras simples.

RN42 - O sistema não deve depender de inteligência artificial generativa para gerar os primeiros insights.

### Dashboard e Analytics Implementados na Fase L

- Realizados usam PaidAt; movimentos usam CreatedAt. Datas analíticas em UTC-03:00.
- Pendentes, vencidos e estoque são estado atual; não são reconstruídos pelo período.
- Comparação com intervalo anterior contíguo de igual duração; base zero não gera percentual.
- Estoque parado exige produto ativo criado há pelo menos 30 dias e ausência de movimento nessa janela.
- Insights são regras determinísticas com critério explícito, sem IA nem decisões automáticas.
- Saída física não representa necessariamente venda; valor de estoque é estimativa a custo.
- Todas as agregações são isoladas pela empresa autenticada. Fórmulas, prioridades,
  limites e interpretação acadêmica: [Dashboard e Analytics](13-dashboard-analytics.md).

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




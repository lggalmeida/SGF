# Produtos - Fase I

## Escopo e modelo

Primeiro modulo operacional completo: dominio, persistencia, API e frontend.
Product implementa ICompanyScopedEntity e possui Id, CompanyId, Name, SKU,
Description opcional, CostPrice, SalePrice, IsActive, CreatedAt e UpdatedAt.
Este documento registra a entrega da Fase I. Estoque foi acrescentado na Fase J;
categorias, unidades configuráveis, fornecedores e imagens permanecem futuros.

- Nome obrigatorio, trim nas extremidades, ate 200 caracteres.
- SKU obrigatorio, ate 64 caracteres apos remocao de espacos e conversao para
  maiusculas invariantes. A identidade do SKU e local a cada empresa.
- Descricao opcional, trim nas extremidades, ate 2000 caracteres; vazio vira null.
- Custo/venda obrigatorios: de zero a 9999999999.99, ate duas casas decimais.
  Venda abaixo do custo e permitida; fracao de centavo e rejeitada, nao arredondada.
- Produto inicia ativo. Apenas o backend define empresa e datas em UTC.
  Timestamps sao alinhados a microssegundos, precisao do PostgreSQL.
- Desativar preserva registro e SKU; reativar usa o mesmo registro.
  Nao ha DELETE fisico nem soft delete generico.

## Camadas e persistencia

Domain guarda entidade, normalizacao e validacoes centrais. Application define
contratos e IProductService. Infrastructure implementa os casos de uso com EF,
seguindo o padrao existente dos servicos de identidade; nao ha EF em Application.
Api mapeia os endpoints em ProductEndpoints e traduz resultados para HTTP.

Migration: 20260914002854_AddProducts. Cria somente Products, com PK Id,
FK CompanyId -> Companies (Restrict), limites de texto, checks de precos nao
negativos/nome/SKU preenchidos e indice unico (CompanyId, SKU).
Esse indice B-tree ja atende consultas pelo prefixo CompanyId; nao foi criado
outro indice redundante apenas de CompanyId.
Precos usam numeric(12,2): dez digitos inteiros e dois decimais, sem ponto
flutuante no banco. O indice unico tambem arbitra cadastros concorrentes.

## Contratos HTTP

Todos os endpoints exigem JWT e tenant ativo revalidado. Member, Admin e Owner
podem gerenciar produtos, sem novas policies de privilegio.

| Metodo/rota | Comportamento |
| --- | --- |
| GET /api/products | items, page, pageSize, totalCount |
| GET /api/products/{id} | produto ou 404 |
| POST /api/products | cria ativo, 201 e Location |
| PUT /api/products/{id} | atualiza campos editaveis, 200 |
| PATCH /api/products/{id}/status | recebe isActive booleano, 200 |

POST/PUT recebem name, sku, description, costPrice e salePrice.
Empresa, ID e datas nao fazem parte desses requests. Campos extras nao sao
usados para definir propriedade do registro. PATCH nao altera os demais campos.
A resposta retorna campos de produto e datas, sem CompanyId ou dados de identidade.

Listagem: page default 1 (limite 1000000), pageSize default 20 (1 a 100),
search opcional ate 200 caracteres, isActive opcional. Sem status retorna todos.
Busca por substring de nome ou SKU, sem diferenciar maiusculas/minusculas;
espacos no SKU seguem sua normalizacao. Ordenacao Name + Id estabiliza paginacao.
Busca simples e paginacao por offset sao suficientes; nao ha full-text ou
otimizacao prematura. Contagem e itens sao consultas separadas, nao snapshot
transacional para alteracoes simultaneas.

400 para validacao; 404 para inexistente ou ID de outro tenant; 409 para SKU
duplicado ou conflito de escrita; 401 sem autenticacao e 403 sem tenant valido.
Erros de persistencia nao expõem exception/SQL/stack trace. Escritas concorrentes
do mesmo produto nao possuem versionamento de negocio: a ultima escrita valida
prevalece; CompanyId como concurrency token protege a propriedade do registro.

## Seguranca multi-tenant

ProductService resolve ICurrentTenantContext antes de acessar Products.
O contexto revalida usuario, Membership, Company e role e configura o DbContext.
As consultas normais recebem Global Query Filter automaticamente, inclusive por ID.
SaveChanges atribui CompanyId na criacao, recusa troca de empresa e valida as
alteracoes. O concurrency token da F.1 inclui a empresa no predicado SQL:

```sql
UPDATE "Products" SET ... WHERE "Id" = @id AND "CompanyId" = @currentCompany;
```

Nenhum fluxo usa IgnoreQueryFilters, SQL bruto ou operacoes em lote.
Sem tenant nao ha acesso. O frontend nunca envia CompanyId nos requests de
produtos; o identificador aparece apenas na chave local do cache para separa-lo
por empresa. Autorizacao real continua no backend.

## Frontend

ProductsPage substitui somente a rota /app/products dentro do AppLayout.
Tabela desktop com nome, SKU, custo, venda, status e acoes; lista de cards mobile.
ProductForm e um dialog nativo reutilizado em criacao/edicao, com labels,
validacao nativa, cancelamento e Escape. Durante salvamento, controles ficam
desabilitados. Desativacao usa confirmacao nativa; reativacao nao exige confirmacao.
A busca e aplicada ao enviar o formulario de busca (Enter ou lupa).

TanStack Query lista dados e executa mutations. Depois de salvar, invalidamos
queries de produtos da empresa, voltamos a primeira pagina e exibimos feedback.
Filtros atuais sao preservados: um item fora deles pode nao aparecer ate limpar
os filtros. Nao duplicamos a lista em estado React; apenas campos do formulario,
filtros e item em edicao sao estado local. O formulario usa os dados completos
ja retornados pela lista, sem consulta redundante por ID.

Intl.NumberFormat pt-BR apresenta reais; JSON transporta numeros. Sessao,
refresh, logout e protecao de rotas continuam sendo os mecanismos da fase G.
Erros possuem mensagens controladas, sem exibir detalhes internos do backend.

## Testes e execucao

Seguir README para SDK, segredo JWT, PostgreSQL e execucao local.
Aplicar migrations antes de iniciar a API atualizada:

```powershell
dotnet build src/backend/Sgf.sln
dotnet test src/backend/Sgf.sln --no-build
dotnet ef database update --project src/backend/Sgf.Infrastructure --startup-project src/backend/Sgf.Api
npm --prefix src/frontend run typecheck
npm --prefix src/frontend run build
npm --prefix src/frontend test
```

ProductEndpointTests usa API real em memoria e PostgreSQL em banco temporario,
aplicando todas as migrations desde zero e removendo o banco ao terminar.
Cobre validacoes, normalizacao, duplicidade concorrente, precos, pagina,
busca, status, Member/Admin, consulta por ID, isolamento em ambas as direcoes,
tentativas de trocar empresa e UPDATE/DELETE com Product anexado manualmente.
Os testes criticos genericos da F.1 continuam existindo sem alteracao.

products.spec.ts percorre criacao, edicao, busca, validacao, duplicidade,
desativacao/reativacao, F5 e payload sem CompanyId em 1440 e 390px.
Testes anteriores de autenticacao/layout permanecem, com expectativa de Produtos
atualizada de placeholder para estado vazio.
Capturas locais em src/frontend/test-results/products-1440.png,
products-390.png, product-form-1440.png e product-form-390.png.

## Resultado da validacao local

- dotnet build: sucesso, zero avisos e erros.
- dotnet test: 110 aprovados (76 API, 33 Infrastructure, 1 Application).
  Produtos adiciona 18 casos executados, incluindo theories de validacao e roles.
- Migration aplicada em sgf_dev; bancos de integracao recriados do zero pelas
  fixtures com todas as migrations. EF nao detectou alteracoes pendentes no modelo.
- npm run typecheck e npm run build: sucesso.
- Playwright/Edge: 12 aprovados, incluindo 2 fluxos completos de Produtos.
- HTTP independente: health API/banco Healthy, cadastro 201, /me 200,
  produto criado 201, editado 200, busca retornando 1 resultado, duplicado 409
  e status inativo persistido.
- Capturas desktop/mobile inspecionadas; sem overflow horizontal nos testes.

Problemas resolvidos: precisao de timestamps (.NET versus PostgreSQL), largura
do botao herdada do CSS global e seletor de teste do filtro de status.
Um timeout isolado no cadastro da suite anterior nao se repetiu na execucao final;
nao foi alterada a autenticacao para contorna-lo. Testes de navegador e verificacao
HTTP criam contas/produtos identificaveis no banco de desenvolvimento.

## Arquivos desta fase

Criados:

- src/backend/Sgf.Domain/Products/Product.cs
- src/backend/Sgf.Application/Products/ProductContracts.cs
- src/backend/Sgf.Infrastructure/Products/ProductService.cs
- src/backend/Sgf.Infrastructure/Database/Configurations/ProductConfiguration.cs
- src/backend/Sgf.Infrastructure/Database/Migrations/20260914002854_AddProducts.cs
- src/backend/Sgf.Infrastructure/Database/Migrations/20260914002854_AddProducts.Designer.cs
- src/backend/Sgf.Api/Products/ProductEndpoints.cs
- src/backend/Sgf.Api.Tests/Products/ProductEndpointTests.cs
- src/frontend/src/features/products/api.ts
- src/frontend/src/features/products/ProductsPage.tsx
- src/frontend/src/features/products/ProductForm.tsx
- src/frontend/src/features/products/products.css
- src/frontend/tests/products.spec.ts
- docs/10-produtos.md

Modificados:

- src/backend/Sgf.Api/Program.cs
- src/backend/Sgf.Infrastructure/DependencyInjection.cs
- src/backend/Sgf.Infrastructure/Database/SgfDbContext.cs
- src/backend/Sgf.Infrastructure/Database/Migrations/SgfDbContextModelSnapshot.cs
- src/frontend/src/App.tsx
- src/frontend/tests/layout.spec.ts
- README.md
- docs/04-modelo-de-dominio.md
- docs/05-regras-de-negocio.md

Alteracoes nao commitadas da fase H foram preservadas. Nenhuma dependencia nova.

## Limites deliberados da Fase I

Naquele momento ainda não havia estoque ou financeiro; ambos foram implementados
nas fases seguintes. Importação, imagens, fornecedores e categorias continuam
fora do escopo. Não há bloqueio otimista para edição comum dos dados descritivos.
Filtros/pagina nao persistem apos F5; sessao e acesso a Produtos permanecem.
As pendencias de HTTPS, segredos e rate limiting para publicacao continuam
registradas no README e nao foram ampliadas nesta fase.

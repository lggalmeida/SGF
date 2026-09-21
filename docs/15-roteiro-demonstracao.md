# Roteiro de Demonstração e Defesa

Tempo-alvo: **8 a 12 minutos**. O roteiro é um guia de navegação, não um discurso
decorado. Antes da apresentação, carregue o cenário com
`./scripts/seed-demo.ps1` e valide login, dashboard e logout.

## Cenário Preparado

- empresa: **Mercado Exemplo LTDA**;
- usuário: `demo@sgf.local`;
- senha: definida localmente no momento da carga;
- 10 produtos ativos e um inativo;
- um produto zerado e quatro produtos em estoque baixo (incluindo o zerado);
- um produto sem movimentação recente;
- entradas e saídas com diferentes volumes;
- receitas e despesas pagas no período atual e anterior;
- contas futuras e vencidas a pagar e a receber.

Os números foram escolhidos para serem coerentes e fáceis de explicar. Nos
últimos 30 dias, as receitas realizadas somam R$ 12.500,00, as despesas
R$ 5.830,00 e o saldo realizado R$ 6.670,00. Períodos anteriores permitem
comparação. Pendências não entram nesse saldo.

## Preparação Imediata

1. Docker, API e frontend ativos.
2. Health da API e banco respondendo Healthy.
3. Banco demo carregado e login confirmado.
4. Navegador em 100% de zoom, sem ferramentas de desenvolvimento abertas.
5. Dashboard em `Últimos 30 dias`.
6. Uma aba reserva na tela de login.
7. Terminal preparado apenas para recuperação; não exibir segredos.

## Demonstração

### 1. Problema e proposta — 45 segundos

**Mostrar:** tela de login e nome SGF.  
**Explicar:** pequenas empresas frequentemente separam estoque e finanças em
planilhas. O SGF integra esses dados e os transforma em indicadores explicáveis.  
**Valor acadêmico:** aplicação prática de arquitetura, segurança, regras de
negócio e análise de dados.

### 2. Login e contexto SaaS — 45 segundos

**Clicar:** entrar com a conta demo.  
**Mostrar:** empresa atual no header e nome do usuário.  
**Explicar:** o token traz a empresa da sessão, mas Membership, Company e papel
são revalidados no banco. O frontend nunca escolhe CompanyId livremente.  
**Valor acadêmico:** autenticação não é o mesmo que autorização e isolamento.

### 3. Dashboard — 90 segundos

**Mostrar:** cards financeiros, gráfico, situação de estoque e insights.  
**Clicar:** alternar entre últimos 30 dias e período anterior/mês atual.  
**Explicar:** realizados usam PaidAt; estoque é estado atual; comparação usa
intervalo anterior de mesma duração. Base anterior zero não produz percentual
enganoso.  
**Valor acadêmico:** dados operacionais são agregados no PostgreSQL e apresentados
como informação gerencial.

### 4. Produtos — 60 segundos

**Clicar:** Produtos, abrir um item e a ação Novo produto sem necessariamente salvar.  
**Mostrar:** SKU, preços, mínimo e status.  
**Explicar:** SKU é único dentro da empresa; inativação preserva histórico;
CompanyId não faz parte do formulário.  
**Valor acadêmico:** modelagem multi-tenant e validação em camadas.

### 5. Estoque — 90 segundos

**Clicar:** Estoque, filtro de estoque baixo, histórico e registrar uma entrada
ou saída pequena.  
**Mostrar:** saldo atualizado sem F5 e movimento no histórico.  
**Explicar:** saldo e movimento são atômicos; quantidade é decimal; movimento é
imutável; concorrência usa atualização condicional para impedir estoque negativo.  
**Valor acadêmico:** integridade transacional e controle de concorrência.

### 6. Financeiro — 90 segundos

**Clicar:** Financeiro, filtrar pendentes e abrir o cadastro de receita/despesa.  
**Mostrar:** cards, conta vencida e ação de pagar/receber.  
**Explicar:** valores são sempre positivos; Type define entrada ou saída; saldo
realizado considera somente Paid, enquanto pendentes compõem receber/pagar.  
**Valor acadêmico:** regra simples e auditável, sem simular sistema contábil.

### 7. Analytics — 90 segundos

**Clicar:** Analytics e percorrer Financeiro, Estoque e Produtos.  
**Mostrar:** série temporal, maior saída e produto parado.  
**Explicar:** maior saída física não é “mais vendido”; valor de estoque é
estimativa a custo. Insights usam regras determinísticas e evidências.  
**Valor acadêmico:** apoio à decisão com interpretação honesta dos dados.

### 8. Configurações e tema — 35 segundos

**Clicar:** Configurações e alternar o tema.  
**Mostrar:** conta, empresa, papel, dark mode e sessão.  
**Explicar:** preferência visual fica no navegador e sobrevive ao logout; segurança
permanece no backend.  
**Valor acadêmico:** acabamento, acessibilidade e separação entre UX e segurança.

### 9. Multi-tenancy — 45 segundos

**Mostrar:** diagrama da documentação ou explicar sobre uma tela de negócio.  
**Explicar:** Global Query Filters protegem leitura; CompanyId é aplicado na
criação; concorrência condiciona UPDATE/DELETE ao tenant persistido; testes
tentam acesso cruzado.  
**Valor acadêmico:** isolamento tratado como requisito de segurança.

### 10. Conclusão — 30 segundos

**Explicar:** o SGF é um protótipo funcional robusto com práticas de produção,
não um SaaS público pronto. A contribuição é demonstrar a cadeia
`dados operacionais -> processamento -> indicadores -> apoio à decisão`.

## Perguntas Prováveis da Banca

### Por que SaaS?

Uma única aplicação atende várias empresas com manutenção centralizada. O
CompanyId e a Membership permitem isolamento e papéis por organização.

### Como funciona o multi-tenancy?

O JWT identifica a empresa da sessão, o backend revalida vínculo e empresa, e
o EF Core filtra entidades tenant-scoped. Escritas também verificam o CompanyId
persistido, inclusive para entidades anexadas.

### Por que monólito modular?

Ele reduz custo operacional e cognitivo para o escopo do TCC, mantendo limites
claros entre domínio, aplicação, infraestrutura e API. Microsserviços não
resolveriam um problema presente.

### Por que PostgreSQL?

Oferece transações, constraints, tipos decimal/date adequados, boa integração
com EF Core e recursos suficientes para agregações analíticas do projeto.

### Como analytics ajuda na decisão?

Consolida saldo, pendências, vencidos, ruptura, movimentação e comparação de
períodos. O gestor identifica prioridades com evidências, mantendo a decisão.

### Por que não usar IA?

As regras determinísticas são explicáveis, testáveis e compatíveis com o volume
e a qualidade dos dados atuais. IA adicionaria complexidade sem necessidade.

### Como o sistema evita estoque negativo?

Valida saldo e usa concorrência otimista/update condicional. Entre duas saídas
simultâneas, somente uma escrita compatível com o saldo conhecido é aceita; a
outra retorna conflito e não cria movimento.

### Como protege senhas?

ASP.NET Core Identity gera e verifica hashes com algoritmo e parâmetros próprios.
Senha e PasswordHash nunca fazem parte das respostas.

### Qual a diferença entre access e refresh token?

O access token curto autoriza requisições. O refresh opaco e mais duradouro fica
em cookie HttpOnly, é armazenado por hash e rotacionado para obter novo access.

### O que ocorre se um Owner virar Member?

Policies privilegiadas consultam o papel atual revalidado no contexto do tenant,
não confiam apenas na claim antiga do JWT.

### Por que o tema fica no navegador?

É uma preferência de apresentação sem impacto de segurança ou negócio. Persistir
no backend criaria contrato e tabela sem benefício necessário ao TCC.

### Quais são as limitações?

Sem switch-company, fornecedores, vendas/compras, integrações, pagamentos
parciais, observabilidade e hardening completo de produção. A lista detalhada
está em `14-divida-tecnica-e-trabalhos-futuros.md`.

### Como colocaria em produção?

HTTPS e proxy, segredos gerenciados, CORS/domínios restritos, rate limiting,
logs e métricas, backups testados, CI/CD, política LGPD e testes de carga e
segurança.

## Plano de Contingência

- API indisponível: conferir terminal e `/api/health`;
- banco indisponível: `docker compose ps` e `/api/health/database`;
- sessão expirada: voltar ao login; o refresh deve restaurá-la automaticamente;
- gráfico vazio: selecionar `Últimos 30 dias` e confirmar a carga demo;
- porta ocupada: encerrar processo anterior antes da apresentação;
- falha irrecuperável: usar screenshots finais e explicar o fluxo sem ocultar a falha.

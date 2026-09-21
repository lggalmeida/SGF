# Layout principal - Fase H

## Escopo

Este documento registra a entrega da estrutura visual da Fase H. Na versão
atual, Produtos, Estoque, Financeiro, Dashboard, Analytics e Configurações já
substituíram os placeholders preservando o mesmo AppLayout.

## Organizacao

- `src/frontend/src/app/AppLayout.tsx`: sidebar, header e area de conteudo compartilhados.
- `app/Sidebar.tsx`: navegacao com identificacao visual e semantica da pagina ativa.
- `app/UserMenu.tsx`: dados da sessão, atalho para Configurações e logout existente.
- `app/navigation.ts`: titulos, rotas, descricoes e icones em um unico lugar.
- `components/PageContent.tsx`: PageHeader, StatCard e EmptyState, todos usados.
- `features/overview/`: visao geral demonstrativa e paginas de modulo.
- `app/layout.css` e `index.css`: layout e tokens visuais.

O Outlet do React Router recebe cada pagina dentro do mesmo AppLayout.
Futuros modulos substituirao apenas o conteudo de suas rotas, sem duplicar
sidebar, header ou autenticacao. Nenhuma permissao e decidida pela sidebar.

## Rotas e sessao

`/app` e `/app/dashboard` exibem a visao geral. As rotas `/app/products`,
`/app/inventory`, `/app/finance`, `/app/analytics` e `/app/settings`
usam a mesma barreira de autenticacao da fase G. Meu perfil direciona a
Configuracoes. Na fase L.1, essa pagina passou a exibir os dados de `/api/auth/me`,
a reutilizar o logout existente e a controlar o tema visual do aplicativo.

Nome, empresa e role vem de useAuth e do contrato existente de /api/auth/me.
A empresa aparece no header; nome completo, email e role ficam no menu do usuario.
Access token em memoria, cookie HttpOnly, refresh e logout nao foram alterados.
F5 restaura a sessao antes de renderizar a rota protegida.

## Identidade visual e acessibilidade

CSS simples, fonte do sistema, verde como cor principal, neutros claros e destaque
ambar. Tokens definem cores, raio de 8px e sombra discreta. Lucide React e a unica
nova dependencia, para icones consistentes; nao foi adicionado design system.
Os formularios existentes preservam estados de erro, sucesso e envio.

Sidebar persistente a partir de 900px. Abaixo disso, dialog nativo modal com
fundo inerte, fechamento por Escape, botao ou navegacao, e retorno de foco.
O navegador pode levar o foco a sua propria interface durante a tabulacao, mas
nao aos controles da pagina ao fundo. O menu do usuario usa details/summary,
fecha por Escape ou clique externo e oferece a acao de logout.
Ha foco visivel, labels nos botoes de icone, link para pular ao conteudo e
respeito a prefers-reduced-motion. Grades se reorganizam sem largura fixa excessiva.

### Tema claro e escuro - Fase L.1

A preferencia `light` ou `dark` e salva em `localStorage` com a chave `sgf.theme`.
Sem preferencia salva, o SGF usa `prefers-color-scheme`. Um script minimo no
`index.html` aplica o atributo `data-theme` antes do React, reduzindo a troca
visual durante o carregamento. Nenhum token, senha ou dado do usuario e persistido.

Os modulos compartilham tokens CSS semanticos para fundo, superficie, texto,
borda, estados e graficos. A configuracao vale tambem para login e cadastro e
sobrevive ao logout. O contrato atual de `/api/auth/me` nao expoe o status
cadastral da Company; por isso a tela exibe o nome da empresa sem inferir status.

## Validacao

Executar os comandos de testes e execucao do README, com API e PostgreSQL ativos.
Os testes de autenticação foram preservados. Testes de layout cobrem navegação,
página ativa, acesso a Configurações, F5, drawer, teclado, logout mobile e
proteção de rota interna.

Viewports: 1440, 1024, 768 e 390px; a suite anterior cobre tambem 360px.
As capturas sao geradas em `src/frontend/test-results/` (nao versionadas):
dashboard-desktop.png, dashboard-notebook.png, dashboard-768.png,
dashboard-390.png e menu-390.png. Capturas atuais são regeneradas na Fase M.
Os testes criam contas frontend-...@example.com no PostgreSQL local.

O layout continua sem decidir autorização: os módulos reais dependem da proteção
do backend. Estado final e pendências de publicação estão no README e no
documento 14.

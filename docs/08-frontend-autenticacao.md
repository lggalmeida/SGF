# Fase G - Frontend de autenticacao

Este documento preserva o recorte histórico da Fase G. Sidebar, módulos de
negócio e dashboard citados como ausentes foram entregues nas fases posteriores;
o estado atual e os comandos vigentes estão no README.

## Estrutura

- src/frontend/src/App.tsx: rotas /login, /register e /app; guards e layout.
- src/frontend/src/features/auth/AuthPages.tsx: formularios e pagina da sessao.
- src/frontend/src/features/auth/AuthProvider.tsx: estado React e consulta /me.
- src/frontend/src/features/auth/session.ts: token em memoria, refresh e logout.
- src/frontend/src/lib/api.ts: fetch, base URL e erros HTTP.
- src/frontend/src/App.css e index.css: estilos responsivos simples.
- src/frontend/public/sgf-mark.png: marca bitmap simples; index.html usa titulo SGF e pt-BR.
- src/frontend/.env.example: VITE_API_BASE_URL, sem segredo.
- src/frontend/playwright.config.ts e tests/auth.spec.ts: testes de navegador.

React Router foi adicionado para navegacao declarativa, historico e guards.
Playwright e apenas dependencia de desenvolvimento para validar as telas e cookies
no navegador. Nao foram adicionados gerenciadores de estado ou bibliotecas de formulario.

## Sessao e fluxo

O access token fica exclusivamente em uma variavel do modulo de sessao, em memoria.
Nao vai para localStorage/sessionStorage nem logs. O provider mantem o usuario
retornado por /me e o estado restoring, anonymous ou authenticated.
TanStack Query executa /me, deduplica consultas e permite cancelamento/limpeza de cache;
campos e envio do formulario sao estado local React/DOM.

Na abertura, uma tentativa de refresh compartilha a mesma promise mesmo no replay
de efeitos do StrictMode. O browser envia o cookie HttpOnly; JavaScript nunca le
seu valor. Apos refresh bem-sucedido, /me fornece os dados e libera /app.
Falha inicial mostra login sem um alerta assustador. F5 repete esse mesmo fluxo.

Login envia email/senha, recebe JWT, consulta /me e navega para /app.
Cadastro cria usuario/empresa/membership pela API existente e redireciona para
login com confirmacao; nao cria sessao automaticamente.
Formularios usam campos nativos, labels, limites, mensagens em portugues e
desabilitam envio enquanto pendente. A senha e apagada do campo apos cada envio.
Erros HTTP sao traduzidos; detalhes internos do backend nao sao exibidos.

## Refresh automatico e protecao

Somente chamadas autenticadas usam Authorization Bearer. Um 401 tenta refresh
uma vez e repete a chamada original uma vez. Chamadas concorrentes na mesma aba
compartilham a promise. Um 401 atrasado de token antigo reutiliza o token ja renovado.
401 novamente, refresh falho ou /me proibido limpam a sessao. Um contador de
geracao impede respostas antigas de restaurarem um token apos encerramento.

Cookies sao incluidos somente em login, refresh e logout. Cadastro e /me usam
credentials: omit. VITE_API_BASE_URL e centralizada; a URL nao e segredo.
Guard aguarda a restauracao: /app exige sessao; /login e /register redirecionam
usuarios autenticados para /app. Isso e navegacao, nao autorizacao de seguranca:
o backend continua validando tenant e role.

## Logout

Logout limpa token, dados e cache local, aguarda refresh que ja esteja em andamento
e pede revogacao do cookie resultante. A tela aguarda o termino antes de permitir
novo login. Se a rede falhar, a sessao local continua encerrada e ha um aviso.

Um unico booleano nao sensivel, sgf.logoutPending, em sessionStorage evita
restauracao automatica nessa aba apos F5 enquanto a revogacao nao foi confirmada.
Na abertura, tenta logout novamente, sem refresh. Nao armazena senha, token ou usuario.
Se o navegador bloquear storage, a garantia local dura apenas enquanto a pagina
esta aberta. Falha de rede nao pode garantir revogacao no servidor.

## Validacao e limites

Executar npm install, npm run build e npm run typecheck. Nao ha lint configurado.
Com API/PostgreSQL ativos, npm test executa Playwright (Chromium ou Edge pelo
PLAYWRIGHT_CHANNEL). As contas de teste ficam no banco local; use apenas ambiente
de desenvolvimento. Traces estao desativados para nao capturar credenciais.
Screenshots desktop/mobile ficam em test-results, ignorado pelo Git.

Cobertura: cadastro, login valido/invalido, validacoes de senha/email duplicado,
rota protegida, F5, redirecionamentos de autenticados, HttpOnly, ausencia de tokens
no storage, refresh concorrente, refresh falho, limite de repeticao, logout normal
e com falha de rede, e layout mobile.

O mecanismo compartilha refresh apenas na mesma aba. Abas simultaneas podem
competir pela rotacao do cookie e exigir novo login; coordenacao entre abas nao
foi adicionada nesta fase. Nao ha garantia contra XSS apenas por usar memoria.
Publicacao exige HTTPS, origens CORS corretas e fallback de rotas SPA para index.html.
Rate limiting de login continua pendente no backend antes da publicacao publica.
Na Fase G ainda não havia sidebar, dashboard ou módulos de negócio.
Switch-company permanece futuro.

## Resultado desta entrega

- npm install: concluido; React Router 7.18.3 e Playwright 1.63.0 adicionados.
- npm test: 8 testes aprovados com Edge instalado; mobile tambem passou em 3 repeticoes isoladas.
- npm run typecheck e npm run build: aprovados. Nao ha script de lint.
- GET /api/health e /api/health/database: 200. Migrations locais ja estavam atualizadas.
- Nenhum arquivo do backend foi alterado nesta fase; alteracoes anteriores da F.1 foram preservadas.
- Foram modificados App.tsx, App.css, index.css, lib/api.ts, index.html, package.json,
  package-lock.json, README.md e .gitignore. A URL do .env.example existente foi mantida.
- Foram criados os tres arquivos features/auth, a marca PNG, configuracao/testes Playwright
  e este documento.

Nos testes iniciais, limpar o cache removia a consulta observada e deixava /app
carregando. Foi corrigido entregando explicitamente o resultado de /me ao estado
do provider. A verificacao de HttpOnly foi ajustada para o Path real do cookie.
Uma falha isolada de carregamento mobile nao se repetiu nas tres execucoes
isoladas nem na suite final; sua causa nao foi confirmada.

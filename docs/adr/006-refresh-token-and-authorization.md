# ADR 006 - Refresh Token, Logout e Autorizacao

## Contexto e decisao

O JWT de acesso dura 15 minutos. Uma validade curta limita o tempo de uso de uma
credencial roubada, mas exige uma forma de renovar o acesso sem reenviar a senha.
Usamos refresh token opaco: 64 bytes aleatorios criptograficos em Base64, valido
por 7 dias. Apenas seu SHA-256 em hexadecimal e persistido. Hash rapido e adequado
aqui porque o token possui alta entropia; senhas continuam exclusivamente no Identity.
Nao usamos JWT longo, blacklist, familias de tokens ou servico externo.

## Persistencia e rotacao

RefreshTokens possui Id, UserId, CompanyId, TokenHash, CreatedAt, ExpiresAt e RevokedAt.
O hash tem indice unico. Ha indices CompanyId e (UserId, CompanyId), e FKs para
AspNetUsers (cascade) e Companies (restrict). E persistencia de autenticacao,
nao uma entidade de negocio com Global Query Filter.

Login cria a sessao somente apos validar credenciais e a unica empresa disponivel.
Refresh busca pelo hash, exige token nao revogado/expirado, usuario existente,
membership ativo e empresa ativa. Mantem a Company original e usa a Role atual.
Nao aceita UserId, CompanyId ou Role do cliente.

Cada refresh revoga o token anterior e cria outro em um unico SaveChanges
transacional. RevokedAt e um token de concorrencia do EF: duas tentativas sobre
o mesmo token nao podem ambas confirmar a gravacao. A perdedora recebe 401.
Cada substituto dura 7 dias a partir da renovacao; nao ha prazo absoluto de sessao.
O cliente futuro deve executar apenas um refresh por vez.

## HTTP e cookies

POST /api/auth/login e POST /api/auth/refresh retornam apenas accessToken,
tokenType, expiresAt, userId, companyId e role no JSON. O refresh bruto aparece
somente em Set-Cookie: sgf_refresh_token, HttpOnly, SameSite=Lax, Path=/api/auth,
sem Domain. Secure e obrigatorio fora de Development/Testing. Respostas de auth
usam Cache-Control: no-store. Nao registrar senha, tokens, cookies ou hashes em logs.

Localmente, usar localhost tanto na API (5206) quanto no frontend (5173), sem
misturar localhost e 127.0.0.1. O frontend futuro usara credentials: include.
CORS permite credenciais somente para Cors:AllowedOrigins, com origens explicitas.
POSTs de auth com Origin fora dessa lista recebem 403 antes de alterar cookies.
Clientes nao navegador podem omitir Origin; SameSite complementa a protecao no navegador.
Producao exige HTTPS e frontend/API no mesmo site para esta configuracao SameSite.
Uma hospedagem cross-site exigira outra decisao de cookies e protecao CSRF.

Refresh invalido recebe 401 generico e limpa cookie. Logout recebe o cookie,
revoga quando possivel, limpa-o e retorna 204 inclusive se a sessao ja terminou.
Logout encerra apenas a sessao representada pelo cookie. JWT ja emitido permanece
valido ate expirar, sujeito a revalidacao do tenant. Nao ha revogacao global nem
limpeza automatica dos registros expirados nesta etapa.

## Autorizacao

OwnerOnly aceita Owner; AdminOrOwner aceita Owner e Admin. Autenticacao normal
continua para Member. As policies nativas usam ICurrentTenantContext scoped,
revalidado no PostgreSQL. Nao usar Authorize(Roles=...) para privilegios de empresa.
Preservamos a fase D: divergencia entre Role do JWT e Membership invalida contexto.
Um Owner rebaixado a Member perde acesso com o JWT antigo; refresh fornece um novo
JWT Member. Tenant invalido nunca satisfaz policies. Nenhum endpoint ficticio foi criado.

## Configuracao e limites

Jwt:SigningKey deve ser configurada fora do Git, com pelo menos 32 bytes aleatorios.
Desde a F.1, todos os ambientes, inclusive Testing, exigem chave explicita valida;
nao ha fallback efemero. Factories configuram a chave de testes por host, usando
a mesma configuracao na emissao e validacao, sem alterar ambiente global.
Issuer/Audience devem existir, AccessTokenMinutes aceita
1 a 60 e RefreshToken:Days aceita 1 a 30. Defaults: 15 minutos e 7 dias.
Nao ha IsActive em ApplicationUser; usuario removido e rejeitado. Inativacao
do acesso existente acontece por Membership ou Company.

Testes de integracao com PostgreSQL cobrem rotacao, concorrencia, revogacao,
expiracao, empresa preservada, hash, logout e policies consultando o contexto real.
Limites deliberados: sem familias/deteccao de roubo, lista de dispositivos ou
recuperacao de sessao apos perda da resposta de rotacao. Nesses casos, novo login.

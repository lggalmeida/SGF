# ADR 003 - Access Token JWT

## Status

Aprovado.

## Contexto

Na Fase C, o SGF passou a permitir login com e-mail e senha. A API precisa devolver uma forma segura para o cliente se identificar nas proximas requisicoes, sem reenviar a senha a cada chamada.

## Decisao

O SGF usara access tokens JWT assinados para autenticar requisicoes HTTP.

Nesta fase, o token sera emitido somente quando:

- o usuario existir;
- a senha for validada pelo ASP.NET Core Identity;
- existir exatamente um `Membership` ativo;
- a `Company` vinculada estiver ativa.

Se houver mais de um membership ativo, a API retornara `company_selection_required` e nao escolhera uma empresa automaticamente.

## Claims Utilizadas

```text
sub        = identificador do usuario
company_id = identificador da empresa atual
role       = papel do usuario naquela empresa
jti        = identificador unico do token
iat        = data/hora de emissao
```

`company_id` e `role` sempre vem do `Membership` validado no backend. Eles nao sao aceitos livremente do frontend.

## Configuracao

A configuracao JWT possui:

- `Issuer`;
- `Audience`;
- `SigningKey`;
- `AccessTokenMinutes`.

A `SigningKey` nao deve ser versionada com valor real. Em desenvolvimento, ela deve ser fornecida por variavel de ambiente ou mecanismo equivalente de segredo local.

## Duracao

A duracao inicial do access token e de 15 minutos.

Essa duracao e curta o suficiente para reduzir o impacto de vazamento de token e simples o bastante para esta fase. Refresh token sera tratado separadamente em etapa futura.

## Consequencias

- Endpoints protegidos podem usar autenticacao JWT Bearer.
- `GET /api/auth/me` usa o JWT para identificar usuario, empresa atual e papel.
- Token adulterado ou expirado deve ser rejeitado.
- O token nao deve conter senha, `PasswordHash` ou dados sensiveis.

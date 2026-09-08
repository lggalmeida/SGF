# ADR 001 - Usuario, Membership e Company

## Status

Aprovado.

## Contexto

O SGF sera uma plataforma SaaS utilizada por pequenas empresas. Um mesmo usuario podera, futuramente, participar de mais de uma empresa. Alem disso, o papel do usuario deve depender da empresa em que ele esta atuando.

Exemplo: uma pessoa pode ser `Owner` na Empresa A e `Member` na Empresa B.

## Alternativas Consideradas

### Usuario pertence diretamente a uma unica empresa

Nesta alternativa, o usuario teria um `CompanyId` direto.

Vantagens:

- implementacao inicial mais simples;
- menos tabelas;
- menos regras de selecao de empresa atual.

Desvantagens:

- baixa flexibilidade;
- dificulta cenarios com usuarios em mais de uma empresa;
- exigiria refatoracao relevante caso o produto evolua.

### Usuario se relaciona com empresa por meio de Membership

Nesta alternativa, o usuario nao pertence diretamente a uma empresa. O vinculo fica em uma tabela intermediaria chamada `Membership`.

Vantagens:

- permite um usuario em varias empresas;
- permite papel diferente por empresa;
- representa melhor um SaaS real;
- reduz risco de refatoracao futura.

Desvantagens:

- adiciona uma tabela;
- exige validacao cuidadosa da empresa atual;
- torna autenticacao e autorizacao um pouco mais elaboradas nas proximas fases.

## Decisao

O SGF utilizara o modelo:

```text
User -> Membership -> Company
```

`Company` representa o tenant. `Membership` representa o vinculo entre usuario e empresa, incluindo o papel do usuario naquela empresa.

Os papeis iniciais serao:

- `Owner`;
- `Admin`;
- `Member`.

Esses papeis pertencem ao `Membership`, e nao a uma role global do usuario.

## Motivo da Escolha

A decisao equilibra simplicidade e evolucao futura. Embora seja um pouco mais complexa do que vincular o usuario diretamente a uma unica empresa, ela e mais adequada para um SaaS e evita uma refatoracao estrutural quando o sistema precisar permitir usuarios em mais de uma empresa.

## Consequencias

- A tabela `Memberships` deve impedir duplicidade para o mesmo `UserId` e `CompanyId`.
- A autorizacao futura devera considerar a empresa atual e o papel do usuario nessa empresa.
- O backend nao devera confiar em `CompanyId` enviado livremente pelo cliente.
- O isolamento multi-tenant sera reforcado nas proximas fases com validacao de contexto e filtros por empresa.

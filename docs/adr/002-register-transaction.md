# ADR 002 - Transacao no Cadastro Inicial

## Status

Aprovado.

## Contexto

Na Fase B, o SGF passou a possuir um fluxo inicial de cadastro em que uma pessoa cria sua conta e, ao mesmo tempo, cria sua primeira empresa. Esse fluxo grava tres informacoes relacionadas:

```text
ApplicationUser
Company
Membership
```

Esses dados precisam nascer juntos. Um usuario sem empresa inicial, ou uma empresa sem membership de dono, deixaria o sistema em estado inconsistente.

## Decisao

O cadastro inicial sera executado dentro de uma transacao de banco de dados usando Entity Framework Core.

O caso de uso abre uma transacao no `SgfDbContext`, cria o usuario com ASP.NET Core Identity, cria a `Company`, cria o `Membership` com papel `Owner` e confirma a transacao apenas depois que todas as etapas forem concluidas com sucesso.

Se qualquer etapa falhar, a transacao e revertida.

## Vantagens

- evita cadastro parcial;
- mantem consistencia entre usuario, empresa e membership;
- usa recursos nativos do EF Core e do banco PostgreSQL;
- nao exige Unit of Work proprio, repository generico ou outra abstracao adicional.

## Desvantagens

- o caso de uso precisa conhecer que a operacao envolve persistencia transacional;
- falhas de banco precisam ser tratadas com cuidado para nao expor detalhes internos ao cliente.

## Consequencias

- O endpoint de cadastro deve retornar erro simples quando a transacao falhar.
- Senha e `PasswordHash` nunca devem ser retornados pela API.
- O fluxo nao emite JWT nesta fase; login e tokens serao tratados em etapa futura.

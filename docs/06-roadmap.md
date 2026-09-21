# Roadmap

Este documento resume o percurso concluído do SGF e separa a entrega acadêmica
das possíveis evoluções.

## Etapas Concluídas

| Fase | Entrega | Estado |
| --- | --- | --- |
| Planejamento | visão, requisitos, arquitetura, domínio e regras | Concluída |
| Fundação | .NET, React, PostgreSQL, EF Core, Docker, health e testes | Concluída |
| A–F.1 | identidade, tenant, JWT, refresh, autorização e hardening | Concluída |
| G | autenticação no frontend e restauração de sessão | Concluída |
| H | layout responsivo compartilhado | Concluída |
| I | produtos | Concluída |
| J | estoque e movimentações | Concluída |
| K | financeiro | Concluída |
| L | dashboard, analytics e insights | Concluída |
| L.1 | configurações e temas claro/escuro | Concluída |
| M | acabamento, demonstração e preparação para banca | Concluída |

## Escopo Final do TCC

1. Segurança, identidade e isolamento multi-tenant.
2. Produtos com SKU único por empresa.
3. Entradas, saídas, saldo e histórico de estoque.
4. Receitas, despesas, pendências e pagamentos.
5. Indicadores financeiros e operacionais por período.
6. Insights determinísticos e explicáveis.
7. Interface responsiva, temas e cenário de demonstração.
8. Testes automatizados e documentação das decisões.

## Itens Retirados do MVP

Fornecedores, categorias, unidades configuráveis e ajustes especiais de estoque
apareciam no planejamento inicial. Foram retirados para manter profundidade nas
regras críticas, multi-tenancy, concorrência e analytics. Isso é redução
consciente de escopo, não entrega parcial mascarada.

## Próximos Passos

Antes de publicação pública, priorizar segurança operacional, HTTPS, segredos,
rate limiting, observabilidade e backup. Evoluções funcionais só devem começar
depois dessa base. A classificação completa está em
[Dívida técnica e trabalhos futuros](14-divida-tecnica-e-trabalhos-futuros.md).

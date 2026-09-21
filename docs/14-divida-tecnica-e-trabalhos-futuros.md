# Dívida Técnica e Trabalhos Futuros

Este documento separa o que é necessário antes de uma publicação pública do
que é uma evolução possível do produto. A classificação evita apresentar como
falha aquilo que foi conscientemente deixado fora do escopo do TCC.

## Antes de Publicação Pública

### Segurança e identidade

- aplicar rate limiting nos endpoints de login, cadastro e refresh;
- configurar lockout progressivo, confirmação de e-mail e recuperação de senha;
- armazenar chaves JWT, credenciais e certificados em um gerenciador de segredos;
- revisar políticas de senha, expiração de sessões e revogação por dispositivo;
- adicionar famílias de refresh token e detecção de reuso quando o risco justificar;
- definir Content Security Policy e demais cabeçalhos HTTP de segurança;
- executar revisão de segurança e teste de invasão no ambiente publicado.

### Operação

- publicar atrás de proxy reverso com HTTPS obrigatório;
- restringir `AllowedHosts`, origens CORS e cookies ao domínio real;
- adicionar logs estruturados, métricas, tracing e alertas;
- definir backup automático do PostgreSQL e testar restauração;
- definir política de retenção e limpeza de refresh tokens revogados/expirados;
- automatizar deploy, migrations e rollback;
- configurar monitoramento de disponibilidade e capacidade;
- validar LGPD, termos de uso e política de privacidade.

### Qualidade

- executar testes em CI em ambiente reproduzível;
- testar navegadores suportados e acessibilidade com ferramentas dedicadas;
- realizar testes de carga com volume compatível com o público esperado;
- definir estratégia para migrações sem indisponibilidade relevante.

## Melhorias Futuras

- switch-company e seleção segura de empresa;
- convites, gestão de membros e papéis por empresa;
- recuperação de senha, confirmação de e-mail e MFA;
- fornecedores, compras, vendas e vínculo controlado entre estoque e financeiro;
- categorias e unidades de medida;
- múltiplos depósitos, lotes, validade, reserva e inventário;
- pagamentos parciais, recorrência e contas bancárias;
- exportação e importação controlada de dados;
- filtros e relatórios adicionais;
- Curva ABC por faturamento após existir domínio de vendas;
- previsões e modelos estatísticos somente após volume e qualidade de dados;
- sincronização de preferência de tema entre dispositivos.

## Fora do Escopo do TCC

- notas fiscais, boletos, PIX e integração bancária;
- contabilidade fiscal e DRE completa;
- marketplace e aplicativo mobile nativo;
- alta disponibilidade multi-região;
- microsserviços, Kafka, Kubernetes e Redis;
- data warehouse, ETL ou processamento distribuído;
- machine learning e inteligência artificial generativa.

Esses itens não são necessários para demonstrar o objetivo acadêmico: integrar
dados operacionais de estoque e finanças e gerar informações explicáveis que
apoiem a tomada de decisão de uma pequena empresa.

## Riscos Residuais Conhecidos

- um access token já emitido permanece tecnicamente válido até sua curta expiração;
  o contexto do tenant e as policies revalidam acesso e papel no banco;
- refreshes simultâneos em abas diferentes podem competir durante a rotação;
- paginação por offset pode perder estabilidade sob muitas escritas concorrentes;
- o valor de estoque é uma estimativa por custo cadastrado, não avaliação contábil;
- saídas de estoque não equivalem necessariamente a vendas;
- indicadores não preservam snapshots históricos de estoque;
- a carga demo depende de Docker, API e banco locais disponíveis.

## Decisões Conscientes

O SGF é um monólito modular porque o escopo e a equipe não justificam distribuição.
O isolamento usa banco compartilhado com CompanyId porque equilibra segurança,
custo e simplicidade. Analytics usa regras determinísticas porque são auditáveis,
adequadas aos dados existentes e coerentes com o objetivo de apoio, não automação,
da decisão.

---
description: Aciona o @handoffexporter-architect para planejar uma mudança (impacto, plano, handoff PLAN_READY para dev).
argument-hint: <descrição da tarefa / evolução (ex.: "desenhar o split do JSON")>
---

Use o agent **@handoffexporter-architect** com `analyze-impact` seguido de `plan-*`.

Contexto a passar:
$ARGUMENTS

Esperado:
1. Análise de impacto (arquivos, serviços, schema, dependências)
2. Plano detalhado em 3-8 passos acionáveis
3. Padrões a aplicar (determinismo, schema-safe, sanitização sem segredos)
4. Riscos conhecidos (ex.: versão do TFS on-prem para a fase de builds)
5. Handoff `PLAN_READY` anexado para @handoffexporter-dev (ledger `.claude/handoffs.json` ou MCP)
6. `<promise>COMPLETE</promise>`

Se a tarefa envolver o pipeline de dados (WIQL, mapeamento de campos, desenho do split)
→ encadear com @handoffexporter-transformer (`map-export-flow` / `design-split`) antes do plano final.

---
description: Aciona o @handoffexporter-dev para executar um PLAN_READY pendente (ou plano descrito inline).
argument-hint: <tarefa ou descrição do plano (opcional se houver handoff pendente)>
---

Use o agent **@handoffexporter-dev** com `implement-plan`.

Contexto:
$ARGUMENTS

O agent deve:
1. Carregar `PLAN_READY` do ledger `.claude/handoffs.json` (ou MCP `get_pending_handoff`)
2. Ler TODOS os arquivos do plano
3. Implementar passo a passo (TaskCreate / TaskUpdate por step)
4. Self-critique obrigatório com 3 bugs previstos
5. `dotnet build` (corrigir todos os erros)
6. Rodar o export e **validar o JSON** (sintaxe + spot-check de schema)
7. Documentar em `docs/dev/<task>.md`
8. Postar `ENTREGA_PRONTA` para @handoffexporter-qa
9. `<promise>COMPLETE</promise>`

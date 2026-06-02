---
description: Aciona o @handoffexporter-qa para validar uma ENTREGA_PRONTA (gate APROVADO/REPROVADO).
argument-hint: <tarefa ou contexto da entrega (opcional se houver handoff pendente)>
---

Use o agent **@handoffexporter-qa** com `review`.

Contexto:
$ARGUMENTS

O agent deve:
1. Carregar `ENTREGA_PRONTA` do ledger `.claude/handoffs.json` (ou MCP)
2. Executar o checklist completo:
   - `dotnet build` (Debug)
   - Export roda e produz JSON válido (`ConvertFrom-Json`)
   - Contrato `HandoffJson` (Source/Request/Items/Handoff; campo ausente = null)
   - Mapeamento de campos NDD (DefinicoesDeNegocio/Tecnicas)
   - Hierarquia PBI→US + determinismo (rodar 2×)
   - Escopo MacGyver (`Central de Soluções\MacGyver`)
   - Sem segredos no output
   - Split-check (se aplicável): index + cross-refs pai/filho
3. Gate decision: APROVADO / REPROVADO / APROVADO COM RESSALVAS
4. Postar handoff(s) correspondente(s)
5. Documentar em `docs/qa/<task>.md`
6. `<promise>COMPLETE</promise>`

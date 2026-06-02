---
description: Aciona o @handoffexporter-transformer para mapear/desenhar o pipeline de export (WIQL, campos, sanitização, split).
argument-hint: <modo: map-export-flow | design-split | audit-field-mapping | audit-sanitization | map-relations>
---

Use o agent **@handoffexporter-transformer**.

Contexto:
$ARGUMENTS

Modos:
- `map-export-flow` — documenta o fluxo input (TFS) → transform → JSON, com refs de linha
- `design-split` — desenha PBI-pai.json + US-filha.json + index.json (naming, cross-refs, sanitização)
- `audit-field-mapping` — audita o mapeamento dos campos NDD (DefinicoesDeNegocio/Tecnicas) e fallbacks
- `audit-sanitization` — avalia a qualidade de `ExtractTextFromHtml` (perda de tabelas/listas/encoding)
- `map-relations` — mapeia `Hierarchy-Forward` / `AttachedFile` (e relações ignoradas)

Saída esperada (formato na seção 7 de `handoffexporter-transformer.md`):
- Inventário (input/output, touch points)
- Achados (file:line)
- Plano / Desenho
- Determinismo & Sanitização
- Risk assessment

Atualizar `.claude/agent-memory/handoffexporter-transformer/MEMORY.md` com os findings.

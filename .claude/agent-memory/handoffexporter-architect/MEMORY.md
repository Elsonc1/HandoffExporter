# HandoffExporter Architect — Memory

> Atualizar após cada planejamento. **NÃO há auto-load** — este arquivo só entra em
> contexto quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Roadmap (ver `docs/architecture/mcp-server-and-tfs-evolution-spec.md`)

- [x] Fase 1 — Split do JSON ✅ implementada + QA APROVADO (`docs/qa/fase-1-split.md`); logging via ILogHelper; 21 testes verdes
- [x] Fase 2a — Enrich Item com State + AcceptanceCriteria (ndd.DefinicoesTecnicas) ✅ implementado + emitido no split
- [ ] Fase 2b — Polir escopo do time MacGyver (`Central de Soluções\MacGyver`)
- [~] Fase 3 — Builds/timeline/logs — **DESBLOQUEADA + PLANEJADA** (`docs/architecture/fase-3-builds-plan.md`, PLAN_READY h-3). ⚠️ scrub de segredos em logs é obrigatório
- [ ] Fase 4 — MCP server local (lê os sub-arquivos do split)
- [ ] Fase 5 — Segmentação por repositório (multi-collection: Integrações) — análise na §12 da spec

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`.
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

# HandoffExporter Architect — Memory

> Atualizar após cada planejamento. **NÃO há auto-load** — este arquivo só entra em
> contexto quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Roadmap (ver `docs/architecture/mcp-server-and-tfs-evolution-spec.md`)

- [x] Fase 1 — Split do JSON ✅ implementada e validada offline (`docs/dev/fase-1-split.md`); pendente QA formal
- [ ] Fase 2 — Polir escopo do time MacGyver (`Central de Soluções\MacGyver`); + enriquecer Item com State/AcceptanceCriteria (ndd.DefinicoesTecnicas)
- [ ] Fase 3 — Extensão TFS REST: builds / timeline / logs — **TFS 2022.2 confirmado + PAT com Build read → desbloqueada** (Build API api 6.0; Pipelines API disponível)
- [ ] Fase 4 — MCP server local (lê os sub-arquivos do split)

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`.
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

# HandoffExporter Architect — Memory

> Atualizar após cada planejamento. **NÃO há auto-load** — este arquivo só entra em
> contexto quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Roadmap (ver `docs/architecture/mcp-server-and-tfs-evolution-spec.md`)

- [ ] Fase 1 — Sanitização + split do JSON (PBI pai / US filhas / index)
- [ ] Fase 2 — Polir escopo do time MacGyver (`Central de Soluções\MacGyver`)
- [ ] Fase 3 — Extensão TFS REST: builds / timeline / logs (confirmar versão do TFS on-prem)
- [ ] Fase 4 — MCP server local (lê os sub-arquivos do split)

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`.
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

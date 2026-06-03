# HandoffExporter Architect — Memory

> Atualizar após cada planejamento. **NÃO há auto-load** — este arquivo só entra em
> contexto quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Roadmap (ver `docs/architecture/mcp-server-and-tfs-evolution-spec.md`)

- [x] Fase 1 — Split do JSON ✅ QA APROVADO; logging via ILogHelper
- [x] Fase 2a — Enrich Item com State + AcceptanceCriteria (ndd.DefinicoesTecnicas) ✅
- [x] Fase 2b — Escopo MacGyver ✅ flag `--team` (area + splitDir default `export/<team>`)
- [~] Fase 3 — Builds/timeline/logs — DESBLOQUEADA + PLANEJADA (`fase-3-builds-plan.md`, PLAN_READY h-3). build def = pipeline CI (≠ repo); pré-req: confirmar se MacGyver usa Azure Pipelines. ⚠️ scrub de segredos em logs
- [~] Fase 4 — MCP server local — PLANEJADO (`fase-4-mcp-server-plan.md`); recomendado .NET (ModelContextProtocol) como dotnet tool, stdio
- [~] Fase 5 — Repos **multi-PROJECT** (corrigido: single collection NDD-DECollection, project Integrações) — ✅ START: `GitQueryService`+`RepoWriter`+`--includeRepos` (lista repos+branches). Falta join work item↔repo (§12.6) + commits/PRs
- **Testes: 30/30 verdes** (`Tests/`, xUnit). Lembrete: `<Compile Remove="Tests/**/*.cs" />` no csproj principal.

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`.
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

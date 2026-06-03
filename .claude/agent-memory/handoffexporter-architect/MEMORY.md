# HandoffExporter Architect — Memory

> Atualizar após cada planejamento. **NÃO há auto-load** — este arquivo só entra em
> contexto quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Roadmap (ver `docs/architecture/mcp-server-and-tfs-evolution-spec.md`)

- [x] Fase 1 — Split do JSON ✅ QA APROVADO; logging via ILogHelper
- [x] Fase 2a — Enrich Item com State + AcceptanceCriteria (ndd.DefinicoesTecnicas) ✅
- [x] Fase 2b — Escopo MacGyver ✅ flag `--team` (area + splitDir default `export/<team>`)
- [~] Fase 3 — Builds/timeline/logs — PLANEJADA (`fase-3-builds-plan.md`, PLAN_READY h-3). ✅ CONFIRMADO: MacGyver USA `azure-pipelines.yml` → pronta p/ implementar (Build API em NDD-DECollection/Integrações). ⚠️ scrub de segredos em logs obrigatório
- [x] Fase 4 — MCP server local ✅ IMPLEMENTADO: `Mcp/HandoffExporter.Mcp` (.NET, ModelContextProtocol 1.3.0, stdio, dotnet tool `ndd-handoff-mcp`), 6 tools sobre `Services/HandoffStore.cs` (testado); `docs/mcp/INSTALL.md` (Claude Code + VS Code/Copilot). Smoke test OK (initialize+tools/list). Falta: extensão VS Code 1-clique (4d) + validar live
- [~] Fase 5 — Repos multi-PROJECT (single collection NDD-DECollection, project Integrações) — ✅ START: lista repos+branches. ✅ vínculo confirmado = **PR**. Falta: join via `_apis/git/repositories/{id}/pullRequests/{prId}/workitems` + commits/PRs
- **Testes: 41/41 verdes** (`Tests/`, xUnit). Build: `<Compile Remove>` p/ `Tests/**` E `Mcp/**` no csproj principal (glob da raiz).

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`.
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

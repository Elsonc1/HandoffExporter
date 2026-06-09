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
- [x] Fase 5 — Repos multi-PROJECT (NDD-DECollection/Integrações) — ✅ repos+branches + **join via PR** (`pull-requests.json`/`commits.json`/`links.json` + MCP `get_links` + `--reposTop`). ⏳ re-rodar `--includeRepos true` p/ gerar PRs/commits live (1ª run do usuário foi só repos+branches)
- ✅ VALIDADO LIVE (usuário, 2026-06-03): 1694 work items MacGyver → 831 PBI/863 US/9 assets/982 raw; 14 repos da Integrações; MCP `ndd-handoff-mcp` instalado + `.mcp.json`
- **Split por WorkItemType** ✅ (fix 2026-06-03): cada item na pasta do tipo real (pbi/us/st/spike/...; slug p/ desconhecidos). index v3.0 (counts-por-tipo + `items` lookup). MCP renomeado: `list_items`/`get_item`. ⏳ usuário precisa RE-RODAR o export (disco está no formato antigo).
- **Fix "item sumindo" (206366, 2026-06-03):** all-artifacts montava a árvore só a partir de `roots = não-em-childIds`; um item em escopo cuja cadeia de pais não chega a um root (ciclo de TFS migrado / re-parent por 'Sub Módulo') era descartado. Agora usa `Services/WorkItemTree.TopLevel` = roots + **órfãos** (garante 100% dos itens em escopo). `BuildItemWithChildren` recebe `written`. Fetch por batch tem **fallback per-id** + **WARN de completude** (lista ids sem detalhe). UNDER (sub-áreas) também aplicado.
- **Testes: 60/60 verdes** (`Tests/`, xUnit; `WorkItemTreeTests` cobre ciclo-sem-root/órfão). Build: `<Compile Remove>` p/ `Tests/**` E `Mcp/**` no csproj principal (glob da raiz).

## Decisions Log

- _(vazio)_

## Patterns / Quirks

- TFS api-version 6.0; auth Basic com PAT (`":{pat}"` Base64).
- Area path sempre `Central de Soluções\{area}`. WIQL usa **`UNDER`** (não `=`) p/ incluir sub-áreas (ex.: `MacGyver\Team Works`) — fix 2026-06-03: issue 206366 sumia com `=`. (`WorkItemQueryService` linhas ~106/177/238)
- Schema `HandoffJson` é contrato versionado (`Handoff.Version`).

# Fase 3 — Builds / Timeline / Logs (plano de implementação)

> **Status: DESBLOQUEADA.** TFS confirmado = **Azure DevOps Server 2022.2**; PAT tem **Build read**.
> Plano do `@handoffexporter-architect` para o `@handoffexporter-dev` executar.
> Handoff `PLAN_READY` (taskId `fase-3-builds`) no `.claude/handoffs.json`.

## Objetivo
Adicionar ao snapshot offline os **builds** do(s) pipeline(s) do time MacGyver: status,
resultado, timeline (etapas que falharam) e **logs em texto puro** — para o agent varrer
"por que o build X quebrou" sem MCP live.

## Endpoints (api-version 6.0, Basic PAT — mesmo `HttpClient`)
Base: `https://tfs.ndd.tech/{collection}/{project}/_apis/build/...`

| Para | Endpoint |
|------|----------|
| Definitions | `GET _apis/build/definitions?api-version=6.0` |
| Builds (lista) | `GET _apis/build/builds?definitions={defId}&$top=N&api-version=6.0` |
| Build | `GET _apis/build/builds/{buildId}?api-version=6.0` |
| Timeline | `GET _apis/build/builds/{buildId}/timeline?api-version=6.0` |
| Logs (lista) | `GET _apis/build/builds/{buildId}/logs?api-version=6.0` |
| Log (texto) | `GET _apis/build/builds/{buildId}/logs/{logId}?api-version=6.0` |

## Design
- **`Models/BuildVO.cs`** (novo): `BuildDefinitionRef{Id,Name}`, `Build{Id,BuildNumber,Status,Result,StartTime,FinishTime,SourceBranch,Definition}`, `Timeline{Records[]}`, `TimelineRecord{Id,Name,Type,Result,Log{Id},Issues[]}`, `BuildLogRef{Id,LineCount}`.
- **`Services/BuildQueryService.cs`** (novo): `GetDefinitionsAsync()`, `GetBuildsAsync(defId,top)`, `GetBuildAsync(id)`, `GetTimelineAsync(id)`, `GetLogsAsync(id)`, `GetLogContentAsync(id,logId)`. Tudo via `_tfsService._httpClient`, log via `ILogHelper`.
- **`Program.cs`**: flag `--includeBuilds <true/false>` e `--buildsTop <N>` (default 5 últimos por definition); config `IncludeBuilds`, `BuildDefinitions` (filtro opcional por nome/id). Não quebra o fluxo atual (default false).
- **Escrita** (no `HandoffSplitter` ou novo `BuildWriter`):
  ```
  export/macgyver/
    builds/build-<id>.json   # definition, buildNumber, status, result, datas, branch,
                             # + resumo da timeline (etapas falhas com logId), refs de log
    logs/build-<id>-<logId>.log   # TEXTO puro (não JSON-escapado) — ótimo p/ Grep
  ```

## Passos (para o dev)
1. Criar `Models/BuildVO.cs` com os DTOs.
2. Criar `Services/BuildQueryService.cs` (GET + desserialização + paginação).
3. `Program.cs`: parse `--includeBuilds`/`--buildsTop`; após o export de work items, se ligado, buscar builds.
4. Escrever `builds/` e `logs/` (texto puro); resumo da timeline no build-<id>.json.
5. **Scrub de segredos nos logs** (ver risco abaixo) antes de gravar.
6. `dotnet build` (0 erros) + `dotnet test`.
7. **Novos testes** em `Tests/`: parse de build/timeline (com fixtures JSON), escrita de `builds/`/`logs/`, scrub de segredo, determinismo.
8. Doc `docs/dev/fase-3-builds.md` + handoff `ENTREGA_PRONTA` para QA.

## Self-critique / riscos
1. **⚠️ SEGREDOS EM LOG (alto):** logs de build podem imprimir connection strings/tokens. O passo 5 (scrub por regex: `password=`, `Bearer `, `pat`, `AccessToken`, `;Password=`) é **obrigatório** antes de gravar em `logs/`. Sem isso, NÃO aprovar.
2. **Logs grandes:** truncar/limitar por tamanho (ex.: manter só etapas com `result=failed` por default) e logar o que foi truncado (sem cap silencioso).
3. **Qual pipeline é do MacGyver?** Filtrar definitions por nome/pasta do time — confirmar convenção (questão aberta).
4. **Paginação** de builds/logs em lotes; tratar 404 de timeline (builds antigos sem timeline).

## DoD
- `--includeBuilds true` gera `builds/` + `logs/` (texto, sem segredos), determinístico.
- Testes novos verdes; QA APROVADO.

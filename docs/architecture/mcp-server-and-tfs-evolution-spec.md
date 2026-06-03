# HandoffExporter — Spec de Evolução: Split de JSON, Escopo MacGyver, Extensão TFS e MCP Server Local

> **Status:** Fase 0 aprovada; **Fase 1 (split) implementada e validada offline**
> (ver `docs/dev/fase-1-split.md`). Este documento é a fonte de verdade do roadmap.
> Cada fase vira um ciclo architect → dev → qa.
> **Autor inicial:** squad `@handoffexporter-*` · **Data:** 2026-06-02 · **TFS:** Azure DevOps Server 2022.2

---

## 1. Objetivo & Posicionamento

O HandoffExporter exporta hoje o quadro do TFS on-prem (Azure DevOps Server,
`https://tfs.ndd.tech`) para um JSON de handoff. Ele é a alternativa **batch/offline**
a um MCP server que conversaria com o TFS ao vivo:

| | MCP live (ex.: fork community) | **HandoffExporter (batch/offline)** |
|---|---|---|
| Como | agent consulta o TFS na hora | export periódico → arquivos que o agent lê (Grep/Read) |
| Forte em | "pega o log DESTE build agora" | "snapshot de PBIs/US/configs/logs para varrer offline" |
| Risco | depende do fork funcionar na sua versão de TFS | nenhum — só fala com a API do TFS no momento do export |

Como o TFS é on-prem e o MCP community é incerto, o HandoffExporter é o caminho
confiável. Esta evolução tem 4 objetivos:

1. **Split** do JSON gigante em sub-arquivos legíveis (PBI pai / US filhas + índice).
2. **Escopo do time MacGyver** (`Central de Soluções\MacGyver`) como cidadão de primeira classe.
3. **Extensão TFS** para builds / timeline / logs / definições de pipeline.
4. **MCP server local** opcional, que serve os sub-arquivos do split (sem tocar o TFS).

---

## 2. Estado Atual (baseline — não mudar sem motivo)

**Fluxo** (`Program.cs`): carrega `config/config.xml` → lê args CLI → conecta TFS
(`TFSAplicationProcess`) → WIQL (`WorkItemQueryService`) → resolve PBIs e suas US via
`System.LinkTypes.Hierarchy-Forward` → normaliza em `HandoffJson` → grava no `OutputFile`.

**Endpoints usados hoje** (base `https://tfs.ndd.tech/{org}/{project}`, api-version **6.0**, Basic PAT):

| Uso | Endpoint |
|-----|----------|
| WIQL | `POST _apis/wit/wiql?api-version=6.0` |
| Work items (batch) | `GET _apis/wit/workitems?ids=...&$expand=relations&api-version=6.0` |
| Work item (single) | `GET _apis/wit/workitems/{id}?$expand=relations&fields=...&api-version=6.0` |

**Modos:** `pbi` (default), `all-artifacts`, e `--pbiId <id>` (árvore focada).

**Schema de saída** (`HandoffJson`):

```
HandoffJson { Source, Request, ExportedAtUtc, Items[], Handoff }
  Source  { Type:"azure-devops", Collection, Project }
  Request { AreaPath, PbiId?, IncludeIssues, Mode }
  Item    { Id, WorkItemType, Title, RawHtml, SanitizedText, Assets[], Attachments[], Children[] }
  Handoff { Version, Generator }
```

**Mapeamento de campos (dev-guide):** US → Description de `ndd.DefinicoesDeNegocio`
(fallback `System.Description`); AcceptanceCriteria de `ndd.DefinicoesTecnicas`
(fallback `Microsoft.VSTS.Common.AcceptanceCriteria`).

**Dores observadas:** `output.json` ≈ 1,4 MB e `test-content.json` ≈ 3,8 MB num único
arquivo — grande demais para caber confortavelmente no contexto de um agent; `RawHtml` e
data-URIs base64 inflam o arquivo; navegar US específica exige reparsear tudo.

---

## 3. Arquitetura-alvo (3 camadas)

```
┌─ Camada 1: EXPORT ───────────────────────────────────────────────┐
│ HandoffExporter fala com o TFS (WIQL + workitems + [novo] build). │
│ Saída: HandoffJson "bruto" (como hoje) por execução.              │
└──────────────────────────────────────────────────────────────────┘
                 │  (pós-processamento, determinístico)
                 ▼
┌─ Camada 2: SANITIZE + SPLIT ─────────────────────────────────────┐
│ Quebra o HandoffJson em sub-arquivos pequenos:                    │
│   index.json · pbi/PBI-<id>.json · us/US-<id>.json · assets/      │
│ Remove segredos e base64 do conteúdo voltado ao agent.           │
└──────────────────────────────────────────────────────────────────┘
                 │  (arquivos em disco — já legíveis via Read/Grep)
                 ▼
┌─ Camada 3: MCP SERVER LOCAL (opcional) ──────────────────────────┐
│ Lê os sub-arquivos e expõe tools (list_pbis, get_pbi, get_us,    │
│ search, get_build_log...). NÃO toca o TFS. stdio local.          │
└──────────────────────────────────────────────────────────────────┘
```

> **Princípio:** já na Camada 2 os agents conseguem trabalhar (Read/Grep direto nos
> arquivos). A Camada 3 (MCP) é uma conveniência para clientes que falam MCP — não é
> pré-requisito para o valor.

---

## 4. Fase 1 — Sanitização + Split do JSON

### 4.1 Layout proposto

```
export/
  macgyver/
    index.json                 # catálogo da area
    pbi/
      PBI-193404.json          # 1 PBI (pai) + lista de US filhas
    us/
      US-193405.json           # 1 User Story (filha) + ref ao pai
      US-193406.json
    assets/
      193405-img-01.png        # imagens/base64 extraídas (opcional)
    raw/                       # (opcional) RawHtml original, fora do caminho do agent
      193405.html
```

### 4.2 `index.json` (entrada única do agent)

```json
{
  "area": "Central de Soluções\\MacGyver",
  "exportedAtUtc": "2026-06-02T13:00:00Z",
  "generator": "HandoffExporter",
  "version": "2.0",
  "counts": { "pbi": 12, "us": 47, "issues": 3 },
  "pbis": [
    {
      "id": 193404,
      "title": "Configuração do VO de integração",
      "state": "In Development",
      "workItemType": "Product Backlog Item",
      "path": "pbi/PBI-193404.json",
      "childUsIds": [193405, 193406]
    }
  ]
}
```

### 4.3 `pbi/PBI-<id>.json` (pai)

```json
{
  "id": 193404,
  "workItemType": "Product Backlog Item",
  "title": "...",
  "state": "In Development",
  "areaPath": "Central de Soluções\\MacGyver",
  "description": "<texto sanitizado>",
  "childUsIds": [193405, 193406],
  "children": [ { "id": 193405, "path": "us/US-193405.json" } ],
  "attachments": [ { "fileName": "...", "url": "..." } ]
}
```

### 4.4 `us/US-<id>.json` (filha)

```json
{
  "id": 193405,
  "workItemType": "User Story",
  "title": "...",
  "state": "...",
  "parentPbiId": 193404,
  "description": "<de ndd.DefinicoesDeNegocio, sanitizado>",
  "acceptanceCriteria": "<de ndd.DefinicoesTecnicas, sanitizado>",
  "assets": [ { "fileName": "193405-asset-1.png", "path": "assets/193405-asset-1.png", "contentType": "image/png" } ]
}
```

### 4.5 Regras de sanitização e determinismo

- **Sem segredos:** nunca gravar PAT/`Key`/tokens. Redigir qualquer match óbvio.
- **Base64/data-URI:** extrair para `assets/` e referenciar por path; não inflar o JSON do agent.
- **`RawHtml`:** manter fora dos arquivos voltados ao agent (mover para `raw/` ou omitir); o agent consome `SanitizedText`/`description`.
- **Campo ausente = `null`** (regra de qualidade do dev-guide), nunca string vazia silenciosa.
- **Determinístico:** ordenar listas por `Id`; nomes de arquivo estáveis (`PBI-<id>`, `US-<id>`); mesmo input do TFS → mesmos bytes de saída (facilita diff/versionamento).
- **Cross-ref bidirecional:** PBI → `childUsIds[]` + paths; US → `parentPbiId`.

### 4.6 Onde implementar (decisão do `@handoffexporter-transformer` + `architect`)

Duas opções (recomendar uma no plano):

- **(A)** Novo `Services/HandoffSplitter.cs` que recebe o `HandoffJson` em memória (ou lê o `output.json`) e escreve a árvore `export/macgyver/`. Acionado por novo modo `--split <dir>` em `Program.cs`. **Recomendado** — separação limpa, testável, não mexe na lógica de export.
- **(B)** Splitter standalone (script/projeto separado) que consome `output.json`. Útil se quiser desacoplar do build do exporter.

---

## 5. Fase 2 — Escopo do Time MacGyver

- MacGyver = `Central de Soluções\MacGyver`, que já é o valor de `AreaOrId`/`AreaPath`
  no `config.xml` (`ConfigVO`). A WIQL em `WorkItemQueryService` já monta
  `'Central de Soluções\{areaCondition}'` — então **a base já existe**.
- Evolução: tratar MacGyver como destino nomeado do split (`export/macgyver/`) e,
  opcionalmente, suportar sub-areas via `ConfigVO.ControlTargetDate.Areas` (já modelado:
  `AreaNames`).
- Conveniência opcional: flag `--team macgyver` que resolve para a area + diretório de saída,
  evitando digitar a area toda vez.
- **Sem segredo de config no output** — o `Key` (PAT) vive em `config.xml`; o split nunca
  o propaga.

---

## 6. Fase 3 — Extensão TFS REST (builds / timeline / logs / pipeline)

Reaproveita o mesmo `HttpClient`/Basic-PAT de `TFSAplicationProcess`. Sugestão: novo
`Services/BuildQueryService.cs` + modelos em `Models/` + novo modo (`--mode builds` ou
flag `--includeBuilds`).

| Para | Endpoint (a validar contra a versão do TFS) |
|------|----------------------------------------------|
| Build definitions | `GET _apis/build/definitions?api-version=6.0` |
| Builds (lista) | `GET _apis/build/builds?definitions={defId}&$top=N&api-version=6.0` |
| Build (single) | `GET _apis/build/builds/{buildId}?api-version=6.0` |
| Timeline | `GET _apis/build/builds/{buildId}/timeline?api-version=6.0` |
| Logs (lista) | `GET _apis/build/builds/{buildId}/logs?api-version=6.0` |
| Log (conteúdo, texto) | `GET _apis/build/builds/{buildId}/logs/{logId}?api-version=6.0` |
| Release defs (clássico) | `GET _apis/release/definitions` (host **vsrm**; api-version pode diferir) |

### 6.1 ✅ Versão do TFS — confirmada: Azure DevOps Server 2022.2

Confirmado **Azure DevOps Server 2022.2** (build `AzureDevopsServer_20260204.3`). Suporta
a REST API 6.0/7.1, a **Build API** (`_apis/build/*`, para timeline/logs) **e** a
**Pipelines API** (`_apis/pipelines`, YAML). O PAT atual **tem scope de Build read**.
→ Fase 3 desbloqueada: usar a Build API (api 6.0, consistente com o código atual) para
builds/timeline/logs; a Pipelines API fica disponível se quisermos as definições YAML.

### 6.2 Saída proposta (alinha com o split)

```
export/macgyver/
  builds/
    build-<id>.json            # definição, status, result, datas, refs de log + resumo da timeline
  logs/
    build-<id>-<logId>.log     # log em TEXTO puro (não JSON-escapado) — ótimo p/ Grep
```

`build-<id>.json` deve trazer: `definitionName`, `buildNumber`, `status`, `result`,
`startTime`, `finishTime`, `sourceBranch`, e um resumo da timeline (etapas que falharam,
com `logId`) para o agent saber qual `.log` abrir.

---

## 7. Formato de saída que os agents leem bem (diretrizes transversais)

1. **Arquivos pequenos e estáveis** — um PBI/US/build por arquivo, nome previsível.
2. **Um índice** (`index.json`) — o agent faz 1 Read do índice e depois Reads alvo.
3. **Logs em texto puro** — não embrulhar log de build dentro de JSON (Grep funciona melhor em `.log`).
4. **`SanitizedText`/`description` sobre `RawHtml`** — HTML cru fica em `raw/` ou fora.
5. **Ordenação determinística** — diffs limpos entre exports; versionável.
6. **Sem segredos** — PAT/tokens nunca chegam ao disco voltado ao agent.

Com isso, **um agent já trabalha sem MCP**: `Grep` na pasta `export/macgyver/` + `Read`
alvo. É exatamente o valor "offline" do HandoffExporter.

---

## 8. Fase 4 — MCP Server Local (design, sem código)

Camada de conveniência sobre os arquivos do split. **Não toca o TFS** — lê
`export/macgyver/`.

- **Transport:** stdio (local). **Stack sugerida:** .NET (o time já é C#) ou Node — decidir no plano.
- **Resources:** expor `index.json` como resource navegável.
- **Tools sugeridas:**
  - `list_pbis(area?)` → do `index.json`
  - `get_pbi(id)` → `pbi/PBI-<id>.json`
  - `get_us(id)` → `us/US-<id>.json`
  - `search(query)` → varre `description`/`title`/`acceptanceCriteria`
  - `list_recent_builds(definition?)` → de `builds/`
  - `get_build(id)` / `get_build_log(buildId, logId)` → `builds/` + `logs/`
- **Config (claude):** entrada em `.mcp.json`/settings apontando o server para o diretório do split.

> Observação honesta: como os arquivos já estão em disco e são legíveis, o MCP é
> **opcional**. Vale a pena se você quer expor o snapshot a clientes MCP (ex.: outro
> assistente) com tools nomeadas, ou padronizar o acesso. Para o squad
> `@handoffexporter-*`, Read/Grep direto já basta.

---

## 9. Como amarrar no fluxo

```
[agendado / sob demanda]
  dotnet run -- --areaPath MacGyver --mode all-artifacts --output output.json   (Camada 1)
        │
        ▼
  dotnet run -- --split export/macgyver   (Camada 2: HandoffSplitter)
        │
        ├── (opcional) export builds/logs  →  export/macgyver/builds, /logs   (Fase 3)
        ▼
  [opcional] MCP server local aponta para export/macgyver/   (Camada 3)
        │
        ▼
  Agents @handoffexporter-* (e outros) leem via Read/Grep ou via MCP
```

Papéis do squad nesta evolução:
- **architect** — escreve/atualiza esta spec, planeja cada fase, decide A vs B no split, confirma versão do TFS.
- **transformer** — desenha o split (4.x), audita mapeamento de campos e sanitização.
- **dev** — implementa `HandoffSplitter`, `BuildQueryService`, novos modos.
- **qa** — valida build, JSON, contrato, escopo MacGyver, cross-refs do split, ausência de segredos.

---

## 10. Faseamento & DoD

| Fase | Entregável | Definition of Done |
|------|-----------|--------------------|
| 0 | Esta spec | ✅ Aprovada |
| 1 | Split (`HandoffSplitter` + `--split`) | ✅ **Implementada e validada offline** (`docs/dev/fase-1-split.md`); pendente QA formal (`/handoffexporter:review`) |
| 2 | Escopo MacGyver | `--team macgyver` (ou doc do uso de area); sub-areas opcionais |
| 3 | Builds/timeline/logs | versão do TFS confirmada; `BuildQueryService`; `builds/` + `logs/` em texto; QA APROVADO |
| 4 | MCP server local | tools mínimas (`list_pbis`/`get_pbi`/`get_us`/`search`); aponta p/ o split; doc de config |
| 5 | Segmentação por repositório (multi-project: Integrações) | ✅ **START**: lista repos+branches (`GitQueryService`/`RepoWriter`, `--includeRepos`). Falta o join work item↔repo (§12.6) |

---

## 11. Questões — respostas do usuário (2026-06-02)

| # | Questão | Resposta / Decisão |
|---|---------|--------------------|
| 1 | Versão do TFS on-prem | ✅ **Azure DevOps Server 2022.2** — Build API + Pipelines API disponíveis (ver 6.1) |
| 2 | Onde fica o split | **Dentro do repo** (`export/`). Repo ainda não está no GitHub; `.gitignore` agora ignora `export/`, `tmp/`, os JSON grandes e `**/config.xml` (PAT) |
| 3 | RawHtml | **Manter em `raw/<id>.html`** (implementado na Fase 1) |
| 4 | PAT tem Build read | ✅ **Sim** — Fase 3 desbloqueada |
| 5 | MCP em .NET ou Node | _(decidir na Fase 4)_ |
| 6 | Periodicidade do export | _(em aberto — sob demanda por enquanto)_ |

> ⚠️ **Segurança:** o repo será subido ao GitHub e o `config/config.xml` contém o PAT.
> O `.gitignore` foi atualizado para ignorá-lo (`**/config.xml`). Antes do push, confirmar
> que nenhum `config.xml` com PAT entrou no índice do git.

---

## 12. Segmentação por repositório (multi-project, single collection)

> ✅ **CORRIGIDO (multi-project, NÃO multi-collection):** o MacGyver usa **uma única
> Collection `NDD-DECollection`** com dois **Projects**: `Central de Soluções` (Kanban/work items)
> e `Integrações` (repos — `https://tfs.ndd.tech/NDD-DECollection/Integrações`).
> ✅ **Fase 5 START implementada:** lista repos + branches (`GitQueryService` + `RepoWriter`),
> flag `--includeRepos`, saída `repos/`. Ver `docs/dev/fase-2b-5-repos.md`.

### 12.1 O cenário
| Domínio | Endpoint (host `tfs.ndd.tech`) | Status |
|---------|--------------------------------|--------|
| Work items (Kanban) | `NDD-DECollection/Central de Soluções` | ✅ exporta PBIs/US |
| Repositórios (código) | `NDD-DECollection/Integrações` (Project, mesma collection) | ✅ Fase 5 START: lista repos + branches |

### 12.2 O desafio arquitetural (resolvido na Fase 5 START)
Mesma collection, **project diferente** — basta um segundo base URL
`https://tfs.ndd.tech/NDD-DECollection/{Uri.EscapeDataString("Integrações")}` = `.../Integra%C3%A7%C3%B5es`,
reusando o **mesmo HttpClient/PAT**. Implementado em `GitQueryService` (recebe o `HttpClient`
autenticado do `TFSAplicationProcess`).

### 12.3 Git REST API (ADO Server 2022.2) — endpoints
Base: `https://tfs.ndd.tech/{collection}/{project}/_apis/git/...`, api 6.0, Basic PAT.

| Para | Endpoint |
|------|----------|
| Repositórios | `GET _apis/git/repositories?api-version=6.0` |
| Repositório | `GET _apis/git/repositories/{repoId}` |
| Branches/refs | `GET _apis/git/repositories/{repoId}/refs?filter=heads` |
| Commits | `GET _apis/git/repositories/{repoId}/commits?searchCriteria.$top=N` |
| Pull requests | `GET _apis/git/repositories/{repoId}/pullrequests?searchCriteria.status=all` |
| Work items de um PR | `GET _apis/git/repositories/{repoId}/pullRequests/{prId}/workitems` |
| Árvore/arquivos | `GET _apis/git/repositories/{repoId}/items?scopePath=/&recursionLevel=Full` |

### 12.4 Config (implementado)
`ConfigVO` ganhou `ReposProject` (mesma collection do work-items source):

```xml
<ReposProject>Integrações</ReposProject>
```
CLI: `--includeRepos true [--reposProject Integrações]`. Default do project = `Integrações`.

### 12.5 Layout de saída segmentado por repo
```
export/macgyver/
  index.json              (+ repos[] e relatedRepos nos work items)
  pbi/ , us/              (work items — como na Fase 1)
  repos/
    <repo-name>/
      repo.json           id, defaultBranch, size, remoteUrl, project
      branches.json
      pull-requests.json  (com workItemIds vinculados)
      commits.json        (últimos N; ids citados em #<id>)
```

### 12.6 O join work item ↔ repositório (cross-collection)
Três fontes possíveis de vínculo, em ordem de confiabilidade:
1. **PR → work items** (`pullRequests/{id}/workitems`) — vínculo explícito e confiável.
2. **Mensagem de commit** citando `#<id>`.
3. **Nome de branch** (`feature/193404-...`).

Resultado: enriquecer cada US/PBI com `relatedPullRequests[]`/`relatedRepos[]`, e cada repo com `relatedWorkItems[]`. Isso conecta o Kanban (Central de Soluções) ao código (Integrações) no mesmo snapshot offline.

### 12.7 Sub-passos da Fase 5
- (a) `ConfigVO.ReposSource` + segundo endpoint em `TFSAplicationProcess` (ou um `GitQueryService` próprio).
- (b) Export de `repos/` (repo.json, branches, PRs, commits).
- (c) Join work item ↔ repo no `index.json` + nos arquivos pbi/us.
- (d) Tests + QA.

### 12.8 Questões a confirmar (antes de implementar a Fase 5)
1. ✅ **RESOLVIDO:** "Integrações" é um **Project** na collection `NDD-DECollection` (multi-project, single collection).
2. O PAT atual tem scope de **Code (read)** na Integrações (além de Work Items + Build)?
3. Quais repos pertencem ao MacGyver — todos da Integrações ou um subconjunto (por nome/convenção)?
4. O vínculo work item ↔ repo deve vir de **PR**, **commit message** ou **branch**? (ou todos, com prioridade)

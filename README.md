# HandoffExporter

Exportador **batch/offline** de contexto do **TFS on-prem** (Azure DevOps Server 2022.2 da NDD Tech, `https://tfs.ndd.tech`) para um **snapshot JSON sanitizado e segmentado** que agentes de IA (Claude Code / VS Code + GitHub Copilot) leem por `Read`/`Grep` — ou via um **MCP server local** que acompanha o projeto.

É a alternativa **confiável e offline** a um MCP que conversaria com o TFS ao vivo: roda contra a API do TFS no momento do export e gera arquivos em disco; os agentes trabalham sobre o snapshot, sem dependência de rede.

Foco atual: o time **MacGyver** (`Central de Soluções\MacGyver`).

---

## Sumário

- [Arquitetura](#arquitetura)
- [Pré-requisitos](#pré-requisitos)
- [Configuração](#configuração)
- [Build & Testes](#build--testes)
- [Uso (CLI)](#uso-cli)
- [Estrutura da saída (snapshot)](#estrutura-da-saída-snapshot)
- [MCP Server local](#mcp-server-local)
- [Regras de mapeamento e sanitização](#regras-de-mapeamento-e-sanitização)
- [Diagnóstico & Troubleshooting](#diagnóstico--troubleshooting)
- [Roadmap](#roadmap)
- [Estrutura do repositório](#estrutura-do-repositório)

---

## Arquitetura

Três camadas complementares:

```
┌─ 1. EXPORT ──────────────────────────────────────────────────────┐
│ Fala com o TFS (WIQL + workitems + git da Integrações) e          │
│ normaliza em HandoffJson (árvore PBI → US → ...).                 │
└───────────────────────────────────────────────────────────────────┘
                 │  (determinístico, sem segredos)
                 ▼
┌─ 2. SANITIZE + SPLIT ────────────────────────────────────────────┐
│ Quebra em sub-arquivos pequenos, segmentados por WorkItemType:    │
│ index.json · pbi/ us/ st/ spike/ ... · assets/ · raw/ · repos/    │
└───────────────────────────────────────────────────────────────────┘
                 │  (arquivos legíveis por Read/Grep)
                 ▼
┌─ 3. MCP SERVER LOCAL (opcional) ─────────────────────────────────┐
│ ndd-handoff-mcp (stdio) expõe o snapshot ao Claude Code / VS Code │
│ via tools (list_items, get_item, search, list_repos, get_links).  │
└───────────────────────────────────────────────────────────────────┘
```

**Solution (`HandoffExporter.sln`) — 3 projetos .NET 10:**

| Projeto | Tipo | Papel |
|---------|------|-------|
| `HandoffExporter` | exe | exportador + split (CLI) |
| `Tests` | xUnit | 60 testes (split, repos, store, tree) |
| `Mcp` | exe / dotnet tool | MCP server `ndd-handoff-mcp` |

**TFS:** collection `NDD-DECollection`; projects `Central de Soluções` (work items / Kanban) e `Integrações` (repositórios). REST API `api-version=6.0`, auth **Basic** com PAT.

---

## Pré-requisitos

- **.NET 10 SDK**
- Um **PAT** do TFS com permissão de **Work Items (read)** e, para repos/builds, **Code (read)** / **Build (read)**.
- Acesso de rede ao `https://tfs.ndd.tech`.

---

## Configuração

O exportador lê `config/config.xml` (relativo ao app — efetivamente `bin/Debug/config/config.xml`) quando rodado **sem argumentos**. Argumentos de CLI têm precedência.

```xml
<ConfigVO>
  <Organization>NDD-DECollection</Organization>
  <Project>Central de Soluções</Project>
  <AreaOrId>MacGyver</AreaOrId>
  <Mode>all-artifacts</Mode>
  <OutputFile>output.json</OutputFile>
  <ReposProject>Integrações</ReposProject>
  <Key>SEU_PAT_AQUI</Key>
</ConfigVO>
```

> ⚠️ **Segurança:** `config.xml` contém o **PAT**. O `.gitignore` já ignora `**/config.xml`, `output.json`, `logs.json`, `export/` e `tmp/`. **Nunca** commite o `config.xml` com o PAT — commite um `config.example.xml` sem a `Key`.

---

## Build & Testes

```powershell
dotnet build HandoffExporter.sln -c Debug
dotnet test  Tests/HandoffExporter.Tests.csproj      # 60 testes
```

> O `.csproj` principal está na raiz; os projetos `Tests/` e `Mcp/` são excluídos do seu glob de compilação (`<Compile Remove="Tests/**;Mcp/**" />`).

---

## Uso (CLI)

```powershell
dotnet run --project HandoffExporter.csproj -- [opções]
```

| Flag | Descrição |
|------|-----------|
| `--team <nome>` | Atalho de time: define `areaPath` e o diretório de split padrão (`export/<nome>`) |
| `--collection <c>` | Collection do TFS (ex.: `NDD-DECollection`) |
| `--project <p>` | Project (ex.: `Central de Soluções`) |
| `--areaPath <a>` | Area (ex.: `MacGyver`) — WIQL usa `UNDER` (inclui sub-áreas) |
| `--mode <m>` | `pbi` (PBIs + US filhas) ou `all-artifacts` (tudo da área) |
| `--pbiId <id>` | Exporta um PBI específico + filhos |
| `--includeIssues <bool>` | Inclui Issues no modo área |
| `--output <arquivo>` | Caminho do JSON bruto (envelope `HandoffJson`) |
| `--split <dir>` | Gera o snapshot segmentado nesse diretório |
| `--splitFrom <arquivo>` | **Offline**: quebra um `HandoffJson` existente, sem chamar o TFS |
| `--includeRepos <bool>` | Inclui repos (branches + PRs + commits) do `--reposProject` |
| `--reposProject <nome>` | Project dos repositórios (default `Integrações`) |
| `--reposTop <N>` | Qtde de PRs/commits por repo (default 25) |
| `--inspect <id>` | **Diagnóstico**: busca um work item direto e mostra type/area/relations/campos |

### Exemplos

```powershell
# Export completo do MacGyver: work items + split por tipo + repos/PRs/commits
dotnet run --project HandoffExporter.csproj -- --team macgyver `
  --collection NDD-DECollection --project "Central de Soluções" `
  --mode all-artifacts --output output.json --includeRepos true --reposProject "Integrações"

# Um PBI específico
dotnet run --project HandoffExporter.csproj -- --collection NDD-DECollection `
  --project "Central de Soluções" --areaPath MacGyver --pbiId 193403 --output pbi-193403.json --split export/pbi-193403

# Split offline de um output.json já existente (não toca o TFS)
dotnet run --project HandoffExporter.csproj -- --splitFrom output.json --split export/macgyver

# Diagnóstico de um item que não aparece
dotnet run --project HandoffExporter.csproj -- --inspect 206366 `
  --collection NDD-DECollection --project "Central de Soluções" --areaPath MacGyver
```

---

## Estrutura da saída (snapshot)

Itens são segmentados pela **`WorkItemType` real** (não pela posição na árvore):

```
export/macgyver/
  index.json                 # counts por tipo + roots (árvore) + items (lookup id→path)
  pbi/PBI-<id>.json          # Product Backlog Item
  us/US-<id>.json            # User Story
  st/ST-<id>.json            # Sprint Task
  spike/SPIKE-<id>.json
  issue/ISSUE-<id>.json
  <slug-do-tipo>/<SLUG>-<id>.json   # tipos não mapeados (ex.: request-produto/REQUEST-PRODUTO-<id>.json)
  assets/<id>-asset-<n>.<ext># imagens base64 extraídas dos data-URIs
  raw/<id>.html              # RawHtml original (fora do JSON do agente)
  repos/
    index.json               # catálogo de repos
    <repo>/repo.json         # metadata + branches
    <repo>/pull-requests.json# PRs + workItemIds
    <repo>/commits.json
    links.json               # JOIN work item ↔ repo via PR: [{workItemId,repo,prId,prTitle}]
```

Cada arquivo de item carrega: `id, workItemType, title, state, description, acceptanceCriteria,
parentId, parentPath, childIds, children[{id,workItemType,path}], attachments, assets, rawPath`.

**Garantias:** determinístico (ordenado por id; mesmos bytes para o mesmo input), **sem segredos** no output, data-URIs base64 extraídos para `assets/`, e **todo item em escopo é exportado** (roots + órfãos — itens não alcançáveis por ciclo/re-parent não somem).

Mapa de tipos: `Product Backlog Item*`→`pbi/PBI` · `User Story`→`us/US` · `Sprint Task`→`st/ST` · `Spike`→`spike/SPIKE` · `Bug`→`bug/BUG` · `Task`→`task/TASK` · `Issue`→`issue/ISSUE` · `Feature` · `Epic` · `Test Case`→`testcase/TC` · `Impediment`. Demais tipos → slug do próprio nome. (Detalhes em `docs/dev/split-by-type.md`.)

---

## MCP Server local

`ndd-handoff-mcp` (.NET, `ModelContextProtocol`, stdio) expõe o snapshot — **read-only, sem tocar o TFS**.

```powershell
# Empacotar e instalar como dotnet tool
dotnet pack Mcp/HandoffExporter.Mcp.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg Ndd.HandoffExporter.Mcp
# atualizar: dotnet tool update --global --add-source ./nupkg Ndd.HandoffExporter.Mcp
```

**Claude Code** (`.mcp.json`):
```json
{ "mcpServers": { "handoff-macgyver": {
  "command": "ndd-handoff-mcp",
  "args": ["--export", "C:\\...\\HandoffExporter\\export\\macgyver"] } } }
```

**VS Code / Copilot** (`.vscode/mcp.json`):
```json
{ "servers": { "handoff-macgyver": {
  "type": "stdio", "command": "ndd-handoff-mcp",
  "args": ["--export", "${workspaceFolder}/export/macgyver"] } } }
```

| Tool | O que faz |
|------|-----------|
| `list_items` | catálogo (index.json): counts por tipo, roots, items |
| `get_item(id)` | work item completo de qualquer tipo (resolve via index) |
| `search(query)` | busca em title/description/acceptanceCriteria |
| `list_repos` / `get_repo(name)` | repositórios (metadata + branches) |
| `get_links(id)` | vínculos work item ↔ repo via PR |

Guia completo: `docs/mcp/INSTALL.md`.

---

## Regras de mapeamento e sanitização

- **User Story** → `description` de `ndd.DefinicoesDeNegocio` (fallback `System.Description`); `acceptanceCriteria` de `ndd.DefinicoesTecnicas` (fallback `Microsoft.VSTS.Common.AcceptanceCriteria`).
- `state` de `System.State`; hierarquia via `System.LinkTypes.Hierarchy-Forward`; anexos via `AttachedFile`.
- HTML → texto via HtmlAgilityPack; `RawHtml` original preservado em `raw/`.
- Campo ausente → `null` (não string vazia). PAT/segredos **nunca** são serializados.
- **Logging**: tudo via `ILogHelper` → `logs.json` (JSONL `{Timestamp, Level, Message}`) + console.

---

## Diagnóstico & Troubleshooting

- **Um work item não aparece no export?** Rode `--inspect <id>` — mostra se ele é buscável (HTTP), o `WorkItemType`, a `AreaPath`, relações, campos presentes e se está sob a área (WIQL `UNDER`). Salva `inspect-<id>.json`.
- **Logs do `all-artifacts`**: observe `fetched N items`, `N itens topo (roots+orfaos)`, e os WARNs `batch X falhou ... tentando ids individualmente` / `N/M ids sem detalhe apos fetch` (um lote que falha cai para fetch **per-id**, evitando perda silenciosa).
- **`MSB3027: file is locked` no build**: há um `HandoffExporter.exe`/`ndd-handoff-mcp` ainda rodando — finalize o processo e rebuilde.
- **`NU1900` (NuGet vulnerabilities)**: o feed interno `tfs.ndd.tech/.../_packaging` é inacessível offline — é só warning; o restore usa o cache.

---

## Roadmap

| Fase | Descrição | Status |
|------|-----------|--------|
| 1 | Sanitização + split do JSON | ✅ |
| 2a | Enriquecer `Item` com `State`/`AcceptanceCriteria` | ✅ |
| 2b | Escopo do time (`--team`) | ✅ |
| — | Split por `WorkItemType` (pbi/us/st/spike/...) | ✅ |
| 4 | MCP server local instalável | ✅ |
| 5 | Repos multi-project (Integrações) + join via PR | ✅ |
| 4d | Extensão VS Code 1-clique (auto-registra o MCP) | ⏳ planejado |
| 3 | Builds / timeline / logs (Build API) | ⏳ planejado (PLAN_READY) |

Specs e planos detalhados em `docs/architecture/`.

---

## Estrutura do repositório

```
HandoffExporter/
  Program.cs                 # CLI, envelope HandoffJson, modos, --inspect
  Services/
    TFSAplicationProcess.cs  # HttpClient TFS (Basic PAT), GetWorkItemAsync, sanitização HTML
    WorkItemQueryService.cs  # WIQL (área UNDER, PBIs, all-artifacts, issues) + resiliência de fetch
    WorkItemTree.cs          # roots + órfãos (garante export de todo item em escopo)
    HandoffSplitter.cs       # split por tipo + index + assets/raw
    GitQueryService.cs       # repos/branches/PRs/commits (project Integrações)
    RepoWriter.cs            # repos/ + links.json (join via PR)
    HandoffStore.cs          # leitor read-only do snapshot (base do MCP)
  Models/                    # WorkItemVO, RepoVO
  Config/                    # ConfigVO, ConfigManager (config.xml)
  Logging/                   # LogHelper (logs.json), ILogHelper
  Mcp/                       # HandoffExporter.Mcp — ndd-handoff-mcp (tools sobre HandoffStore)
  Tests/                     # xUnit (60 testes)
  docs/                      # architecture/, dev/, qa/, mcp/
  .claude/                   # squad handoffexporter-* (architect/transformer/dev/qa/orchestrator)
```

> Conversação/documentação em **pt-BR**; identificadores em inglês ou misto, conforme o padrão existente.

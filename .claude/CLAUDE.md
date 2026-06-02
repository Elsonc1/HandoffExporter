# HandoffExporter — CLAUDE.md

Este arquivo configura o comportamento do Claude Code ao trabalhar no projeto
HandoffExporter (`C:\Users\elson.lopes\source\repos\HandoffExporter\`).

O HandoffExporter é um exportador de contexto em C# .NET 10 que lê work items do
**TFS on-prem** da NDD Tech (Azure DevOps Server em `https://tfs.ndd.tech`) e os
normaliza num JSON de handoff (PBI → User Stories). É a alternativa **batch/offline**
a um MCP server que conversaria com o TFS ao vivo.

## Linguagem

- Conversação: português (pt-BR)
- Código/identificadores: inglês ou misto, conforme o padrão existente

## Contexto do Projeto

| Item | Valor |
|------|-------|
| Projeto | `HandoffExporter.csproj` (SDK-style, **net10.0**, Nullable + ImplicitUsings) |
| Entry point | `Program.cs` (envelope `HandoffJson`; modos `pbi` / `all-artifacts` / `--pbiId`) |
| TFS REST | `https://tfs.ndd.tech/{org}/{project}/_apis/...`, api-version **6.0**, Basic PAT |
| Area do time MacGyver | `Central de Soluções\MacGyver` (= `AreaOrId`/`AreaPath` no `config.xml`) |
| Campos NDD | US: `ndd.DefinicoesDeNegocio` → Description; `ndd.DefinicoesTecnicas` → AcceptanceCriteria |
| Deps | Newtonsoft.Json 13.x, HtmlAgilityPack 1.12.x |

Docs de referência:
- `docs/architecture/handoff-exporter-dev-guide.md` — contrato atual do export
- `docs/architecture/mcp-server-and-tfs-evolution-spec.md` — **spec da evolução** (split JSON, MCP local, builds/logs/pipeline, escopo MacGyver)

## Agents Disponíveis

| Agent | Função | Model | Slash command |
|-------|--------|-------|---------------|
| `@handoffexporter-architect` | Planejamento, impacto, spec | opus | `/handoffexporter:plan` |
| `@handoffexporter-transformer` | Pipeline de export: WIQL, mapeamento, sanitização, split | opus | `/handoffexporter:transform` |
| `@handoffexporter-dev` | Implementação | sonnet | `/handoffexporter:implement` |
| `@handoffexporter-qa` | Validação + gate | sonnet | `/handoffexporter:review` |
| `@handoffexporter-orchestrator` | Roteamento | sonnet | `/handoffexporter:route` |

Detalhes em `.claude/agents/*.md`.

## Fluxo Recomendado

```
Tarefa (evolução / mudança no export)
   ↓ /handoffexporter:plan <descrição>
@handoffexporter-architect → PLAN_READY (handoff)
   ↓ /handoffexporter:implement
@handoffexporter-dev → ENTREGA_PRONTA (handoff)
   ↓ /handoffexporter:review
@handoffexporter-qa → QA_APROVADO ou QA_REPROVADO + BUG_REPORT
   ↓ se REPROVADO
@handoffexporter-dev fix → ENTREGA_PRONTA (loop, máx 3 iterações)
```

Quando a tarefa envolve o pipeline de dados (WIQL, mapeamento de campos, desenho do split):

```
@handoffexporter-architect (plano inicial)
   ↓ delega map-export-flow / design-split
@handoffexporter-transformer (mapeia/desenha)
   ↓ resultado volta
@handoffexporter-architect (plano refinado) → PLAN_READY
```

## Build / Run

```powershell
# Build
dotnet build "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -c Debug

# Run — MacGyver, todos os artefatos
dotnet run --project "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -- `
  --collection NDD-DECollection --project "Central de Solucoes" --areaPath MacGyver `
  --mode all-artifacts --output output.json
```

## Memory por Agent

Cada agent tem `.claude/agent-memory/<agent>/MEMORY.md`.

> **NÃO há auto-load.** O Claude Code não carrega esses arquivos automaticamente.
> Cada agent tem, no início do corpo, a instrução **FIRST ACTION** para ler o próprio
> MEMORY.md antes de começar. Mantenha em cada um:
> - Tasks ativas (o que está em curso)
> - Patterns aplicados (para não reaprender)
> - Quirks recorrentes (para self-critique acelerada)
> - Decisions log (para auditoria histórica)

## Handoffs (sem MCP)

O HandoffExporter, por default, **não** tem o MCP de handoff da NDD conectado. Os
agents trocam handoffs por um ledger local **`.claude/handoffs.json`**:

```json
{ "handoffs": [
  { "id": "h-1", "from": "handoffexporter-architect", "to": "handoffexporter-dev",
    "signal": "PLAN_READY", "consumed": false, "payload": { } }
]}
```

O produtor anexa um registro; o consumidor lê o pendente endereçado a ele e marca
`consumed: true`. Se o MCP de handoff estiver conectado, use `post_handoff` /
`get_pending_handoff` / `complete_handoff` no lugar do ledger.

## Não Crie

- Edições de `.cs`/`.csproj`/`.xml` fora do escopo do plano carregado pelo dev
- Segredos (PAT/`Key`) serializados em qualquer arquivo de saída
- Mudança no schema `HandoffJson` sem bump de `Handoff.Version` + aprovação do architect

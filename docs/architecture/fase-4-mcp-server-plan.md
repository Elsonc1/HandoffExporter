# Fase 4 — MCP Server local instalável (handoff do MacGyver embutido)

> **Plano/decisão.** Meta: um MCP Server **local e instalável** que expõe o snapshot do
> MacGyver (a saída do split, `export/macgyver/`) ao Claude Code via **stdio** — sem tocar o TFS.

## Recomendação de stack: **.NET** (não Node)
- O time já é C#/.NET 10 → mantém tudo na mesma stack e build.
- SDK: **`ModelContextProtocol`** (C# MCP SDK oficial, NuGet).
- Distribuição: **dotnet tool** (`dotnet tool install -g ndd.handoff.mcp`) ou **exe self-contained single-file**.
- Transport: **stdio** (é como o Claude Code lança MCP servers locais).

> Alternativa Node (`@modelcontextprotocol/sdk`) só se você preferir — mas perde a vantagem "mesma stack + instalável como dotnet tool".

## Arquitetura
- Novo projeto **`HandoffExporter.Mcp`** (na solution) — read-only sobre os arquivos do split.
- Diretório do snapshot configurável: arg `--export <dir>` ou env `HANDOFF_EXPORT_DIR` (default `./export/macgyver`).
- **NÃO** fala com o TFS — só lê `index.json`, `pbi/`, `us/`, `repos/` (e futuramente `builds/`/`logs/`).

## Tools expostas (read-only)
| Tool | Lê | Retorna |
|------|-----|---------|
| `list_pbis` | `index.json` | PBIs {id,title,state,childUsIds} |
| `get_pbi(id)` | `pbi/PBI-<id>.json` | PBI completo |
| `get_us(id)` | `us/US-<id>.json` | US completa (description, acceptanceCriteria, parentPbiId) |
| `search(query)` | varre pbi+us | matches em title/description/acceptanceCriteria |
| `list_repos` | `repos/index.json` | repos {name,defaultBranch} |
| `get_repo(name)` | `repos/<name>/repo.json` | metadata + branches |
| `get_build` / `get_build_log` | `builds/`,`logs/` | *(quando a Fase 3 existir)* |

**Resources:** expor `index.json` e `repos/index.json` como MCP resources navegáveis.

## "handoff do MacGyver embutido" — duas opções
- **(A) Aponta para um diretório** (`--export`): o snapshot é gerado pelo HandoffExporter e o MCP só lê. Mais simples; snapshot sempre atualizável re-rodando o export.
- **(B) Snapshot embutido (baked-in):** o instalador empacota uma cópia de `export/macgyver/` dentro do tool (recurso embutido), com fallback para `--export`. Bom para distribuir "pronto pra uso" sem rodar o export antes. **Recomendo A + opção B no pack.**

## Config no Claude Code (.mcp.json / settings)
```json
{
  "mcpServers": {
    "handoff-macgyver": {
      "command": "ndd-handoff-mcp",
      "args": ["--export", "C:/.../HandoffExporter/export/macgyver"]
    }
  }
}
```

## Faseamento (4a → 4d)
- **4a** Skeleton `HandoffExporter.Mcp` + stdio + tools `list_pbis`/`get_pbi`/`get_us`/`search`.
- **4b** Tools de repos (`list_repos`/`get_repo`).
- **4c** Empacotar como **dotnet tool** (`dotnet pack`) + opção de snapshot embutido.
- **4d** Doc de instalação + entrada `.mcp.json`.

## Decisão pendente do usuário
1. **.NET (recomendado)** ou Node?
2. Snapshot **embutido (B)** ou só **aponta-para-dir (A)**?

> Nota honesta: como o split já está em disco e é legível, os agents `@handoffexporter-*`
> já trabalham via Read/Grep **sem** o MCP. O MCP agrega valor para (1) outros clientes MCP,
> (2) tools nomeadas + search, (3) distribuição instalável "plug-and-play".

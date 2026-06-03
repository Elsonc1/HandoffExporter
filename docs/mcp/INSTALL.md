# HandoffExporter MCP Server — instalação e uso

MCP server **local (stdio)** que expõe o snapshot do MacGyver (a saída do `--split`) ao
**Claude Code** e ao **VS Code (GitHub Copilot)**. É **read-only** e **não toca o TFS** — só lê
os arquivos gerados pelo HandoffExporter.

- Projeto: `Mcp/HandoffExporter.Mcp.csproj` (.NET 10, SDK `ModelContextProtocol` 1.3.0).
- Comando instalado: **`ndd-handoff-mcp`**.

## 1. Gerar o snapshot (uma vez, ou agendado)

```powershell
dotnet run --project HandoffExporter.csproj -- `
  --team macgyver --collection NDD-DECollection --project "Central de Soluções" `
  --mode all-artifacts --output output.json `
  --includeRepos true --reposProject "Integrações"
# → gera export/macgyver/{index.json, pbi/, us/, assets/, raw/, repos/}
```

## 2. Instalar o MCP server (dotnet tool)

```powershell
dotnet pack Mcp/HandoffExporter.Mcp.csproj -c Release -o ./nupkg
dotnet tool install --global --add-source ./nupkg Ndd.HandoffExporter.Mcp
# atualizar depois: dotnet tool update --global --add-source ./nupkg Ndd.HandoffExporter.Mcp
```

Sem instalar (modo dev): `dotnet run --project Mcp/HandoffExporter.Mcp.csproj -- --export <dir>`

## 3. Apontar para o snapshot

Precedência do diretório: `--export <dir>` → env `HANDOFF_EXPORT_DIR` → `./export/macgyver`.

## 4. Configurar no Claude Code

`.mcp.json` (na raiz do projeto) ou settings:

```json
{
  "mcpServers": {
    "handoff-macgyver": {
      "command": "ndd-handoff-mcp",
      "args": ["--export", "C:\\Users\\elson.lopes\\source\\repos\\HandoffExporter\\export\\macgyver"]
    }
  }
}
```

## 5. Configurar no VS Code (GitHub Copilot)

`.vscode/mcp.json`:

```json
{
  "servers": {
    "handoff-macgyver": {
      "type": "stdio",
      "command": "ndd-handoff-mcp",
      "args": ["--export", "${workspaceFolder}/export/macgyver"]
    }
  }
}
```

## 6. Tools expostas

| Tool | O que faz |
|------|-----------|
| `list_items` | catálogo (index.json): counts por tipo, roots, items |
| `get_item(id)` | work item completo de **qualquer tipo** (PBI/US/Sprint Task/Spike/...), resolve via index |
| `search(query)` | busca em title/description/acceptanceCriteria |
| `list_repos` | repositórios (repos/index.json) |
| `get_repo(name)` | repo (metadata + branches) |
| `get_links(id)` | vínculos work item↔repo via PR `[{workItemId,repo,prId,prTitle}]` |

## 7. Verificação

Smoke test (stdio) confirmou `initialize` + `tools/list` (server registra os 6 tools).
Logs vão para **stderr** (stdout é o canal JSON-RPC). A validação ponta-a-ponta é
conectar no Claude Code / VS Code e chamar `list_pbis`.

## 8. Próximo passo (Fase 4d) — extensão VS Code "1-clique"

Para instalação fácil do time: uma extensão VS Code fina que **auto-registra** este server
(via `contributes.mcpServerDefinitions`) e embute/baixa o `ndd-handoff-mcp`. Assim o pessoal
só instala a extensão (Copilot e Claude Code passam a ver os tools sem editar JSON).

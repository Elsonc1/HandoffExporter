# Split segmentado por WorkItemType (correção)

> Corrige o bug: itens raiz eram sempre rotulados `pbi/PBI-<id>` mesmo quando o
> `WorkItemType` era outro (ex.: `Sprint Task` 205826 caía em `pbi/`). Agora cada item
> vai para a pasta do seu **tipo real**.

## Layout novo (`index.json` versão 3.0)
```
export/macgyver/
  index.json                 # counts por tipo + roots(árvore) + items(lookup id→path)
  pbi/PBI-<id>.json
  us/US-<id>.json
  st/ST-<id>.json            # Sprint Task
  spike/SPIKE-<id>.json
  <slug-do-tipo>/<SLUG>-<id>.json   # tipos não mapeados (ex.: request-produto/REQUEST-PRODUTO-<id>.json)
  assets/ , raw/ , repos/
```

Mapa conhecido (`Classify`): `Product Backlog Item*`→pbi/PBI · `User Story`→us/US · `Sprint Task`→st/ST ·
`Spike`→spike/SPIKE · `Bug`→bug/BUG · `Task`→task/TASK · `Issue`→issue/ISSUE · `Feature`→feature/FEATURE ·
`Epic`→epic/EPIC · `Test Case`→testcase/TC · `Impediment`→impediment/IMP. Demais → slug do tipo.

## Cada arquivo de item
```json
{ "id":.., "workItemType":"..", "title":"..", "state":"..", "description":"..", "acceptanceCriteria":..,
  "parentId":.., "parentPath":"<tipo>/<PREFIX>-<id>.json",
  "childIds":[..], "children":[{"id":..,"workItemType":"..","path":"<tipo>/<PREFIX>-<id>.json"}],
  "attachments":[..], "assets":[..], "rawPath":".." }
```
- `parentId`/`parentPath` (genérico — pai pode ser qualquer tipo).
- `children` referencia cada filho pelo **tipo+path corretos** (um filho `Task` aponta p/ `task/TASK-<id>.json`).
- Itens "avulsos" (sem pai/links) viram **roots** normalmente — ok.

## index.json
- `counts`: por pasta/tipo (`{ "pbi": 831, "us": 700, "st": 100, ... }`).
- `roots`: itens sem pai, com refs de filhos (id/tipo/path).
- `items`: lookup achatado `[{id, workItemType, path}]` — usado pelo `HandoffStore`/MCP p/ resolver id→arquivo.

## MCP — tools renomeadas
Como agora um id pode ser de qualquer tipo, as tools por-tipo saíram:
- ❌ `list_pbis`/`get_pbi`/`get_us`  →  ✅ **`list_items`** (catálogo) e **`get_item(id)`** (qualquer tipo, resolve via index).
- Mantidas: `search`, `list_repos`, `get_repo`, `get_links`.

## ⚠️ Ação necessária (re-run)
O export em disco foi gerado com o código antigo (tudo em `pbi/`). **Re-rode** para regenerar por tipo e **atualize o tool MCP**:
```powershell
dotnet run --project HandoffExporter.csproj -- --team macgyver `
  --collection NDD-DECollection --project "Central de Soluções" `
  --mode all-artifacts --output output.json --includeRepos true --reposProject "Integrações"

dotnet pack Mcp/HandoffExporter.Mcp.csproj -c Release -o ./nupkg
dotnet tool update --global --add-source ./nupkg Ndd.HandoffExporter.Mcp
```

## Testes
55 verdes, incluindo: `Split_SprintTaskRoot_GoesToStFolder_NotPbi`, `Split_Spike_GoesToSpikeFolder`,
`Split_UnknownType_SlugFolder`, `Split_MixedChildTypes_GoToOwnFolders_WithCorrectRefs`,
`Split_Index_HasItemsLookup`, `GetItem_SprintTask_ResolvedFromStFolder`.

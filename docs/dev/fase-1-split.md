# Fase 1 — Sanitização + Split do JSON (entrega)

> Implementa a Camada 2 da `docs/architecture/mcp-server-and-tfs-evolution-spec.md`.
> Status: **implementado e validado offline**. Pronto para QA (`/handoffexporter:review`).

## O que mudou

| Arquivo | Mudança |
|---------|---------|
| `Services/HandoffSplitter.cs` | **novo** — quebra um `HandoffJson` em `index.json` + `pbi/PBI-<id>.json` + `us/US-<id>.json` + `assets/` + `raw/` |
| `Program.cs` | flags `--split <dir>` e `--splitFrom <file>`; short-circuit offline (splitFrom não chama o TFS); split pós-export quando `--split` é usado junto com um export normal |

## Como usar

```powershell
# 1) Export normal + split na mesma execução (vai ao TFS)
dotnet run --project "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -- `
  --collection NDD-DECollection --project "Central de Solucoes" --areaPath MacGyver `
  --mode all-artifacts --output output.json --split export/macgyver

# 2) Split OFFLINE de um output.json já existente (NÃO vai ao TFS)
dotnet run --project "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -- `
  --splitFrom output.json --split export/macgyver
```

## Layout gerado (paths nos JSONs são relativos à raiz do split)

```
export/macgyver/
  index.json                 # area, source, request, counts, pbis[]{id,title,path,childUsIds[]}
  pbi/PBI-<id>.json          # raiz: description, childUsIds[], children[]{id,path}, attachments, assets, rawPath
  us/US-<id>.json            # filha: parentPbiId, description, childIds[], attachments, assets, rawPath
  assets/<id>-asset-<n>.<ext># data-URIs base64 decodificados para arquivos reais
  raw/<id>.html              # RawHtml original (fora do JSON do agent)
```

## Garantias verificadas (fixture offline: 1 PBI + 2 US)

- ✅ Build `dotnet build` — 0 erros (warnings são nullable pré-existentes do código legado).
- ✅ Árvore gerada conforme o layout; `exit 0`.
- ✅ **Nenhum data-URI base64 vaza** para `index.json`/`pbi`/`us` (objetivo principal da sanitização).
- ✅ data-URI decodificado para PNG válido em `assets/` (`PNG image data, 1 x 1`).
- ✅ Cross-ref bidirecional: PBI→`children[].path`; US→`parentPbiId`.
- ✅ Determinístico: `childUsIds`/listas ordenadas por `Id` (fixture invertida → saída ordenada).
- ✅ `RawHtml == null` → `rawPath: null` e nenhum arquivo em `raw/`.

## Self-critique / bugs previstos (e tratamento)

1. **WIQL paginada > 100 ids** — não afeta o split (consome o `HandoffJson` já montado). OK.
2. **base64 malformado** — `Convert.FromBase64String` em try/catch; cai para referência sem inline (não re-infla). OK.
3. **Item raiz que não é PBI** (ex.: Issue em `all-artifacts`) — escrito em `pbi/PBI-<id>.json` mesmo assim; `workItemType` real fica dentro do arquivo. Convenção documentada.

## Follow-ups conhecidos (fora do escopo da Fase 1)

- `acceptanceCriteria` e `state` saem **`null`/ausentes**: o exportador atual (`ResolveContent`/`CreateItem`) não captura `ndd.DefinicoesTecnicas` nem `System.State` como campos próprios do `Item`. Enriquecer isso é Fase 1b/2 (tarefa do `@handoffexporter-transformer` + `dev`).
- Avaliar perda de conteúdo em `ExtractTextFromHtml` (tabelas/listas) — `audit-sanitization`.

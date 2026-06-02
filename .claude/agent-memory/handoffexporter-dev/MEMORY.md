# HandoffExporter Dev — Memory

> Atualizar após cada entrega. **NÃO há auto-load** — só entra em contexto quando o
> agent o lê na FIRST ACTION.

## Active Tasks

### Fase 1 — Split do JSON ✅ ENTREGUE (pendente QA)
- Novo `Services/HandoffSplitter.cs` + flags `--split <dir>` / `--splitFrom <file>` em `Program.cs`.
- Validado offline (fixture 1 PBI + 2 US): build 0 erros, árvore correta, sem vazar data-URI, PNG extraído, determinístico.
- Doc: `docs/dev/fase-1-split.md`. Handoff `ENTREGA_PRONTA` em `.claude/handoffs.json`.
- Follow-up: `acceptanceCriteria`/`state` saem null — exportador não captura `ndd.DefinicoesTecnicas`/`System.State` (Fase 2).

## Build / Run

- Build: `dotnet build "...\HandoffExporter.csproj" -c Debug`
- Run MacGyver: `dotnet run --project "...\HandoffExporter.csproj" -- --collection NDD-DECollection --project "Central de Solucoes" --areaPath MacGyver --mode all-artifacts --output output.json`
- Validar JSON: `Get-Content output.json -Raw | ConvertFrom-Json | Out-Null`

## Patterns aplicados

- _(vazio)_

## Quirks recorrentes

- `Nullable enable` (net10): trate `Fields?.ContainsKey(...)` antes de indexar.
- Determinismo: ordene coleções por `Id` antes de serializar.
- Nunca serialize `config.Key` (PAT) no output.

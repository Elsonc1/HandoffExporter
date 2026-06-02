# HandoffExporter Dev — Memory

> Atualizar após cada entrega. **NÃO há auto-load** — só entra em contexto quando o
> agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

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

# Fase 2b (escopo MacGyver) + Fase 5 START (multi-project repos) — entrega

> Status: **implementado e testado** (30/30 testes verdes). Fase 5 = START (lista repos + branches);
> falta o join work item↔repo (§12.6 da spec).

## Fase 2b — escopo MacGyver
- `Program.cs`: flag **`--team <name>`**. Quando presente: define `areaPath = team` (se não dado) e
  `splitDir = export/<team-minúsculo>` (se `--split` não dado).
- Ex.: `--team macgyver` → area `MacGyver` + split em `export/macgyver`.

## Fase 5 START — repositórios (multi-project, single collection)
Confirmado: **multi-project**, não multi-collection. Collection única `NDD-DECollection`;
repos no Project `Integrações` (`https://tfs.ndd.tech/NDD-DECollection/Integrações`).

| Arquivo | Mudança |
|---------|---------|
| `Models/RepoVO.cs` | **novo** — DTOs Git (`GitRepoResult`/`GitRepo`/`GitRef...`) + projeção `RepoInfo` |
| `Services/GitQueryService.cs` | **novo** — lista repos + branches; `BuildBaseUrl` codifica o project (`Integrações`→`Integra%C3%A7%C3%B5es`); reusa o `HttpClient` autenticado do `TFSAplicationProcess` |
| `Services/RepoWriter.cs` | **novo** — escreve `repos/index.json` + `repos/<name>/repo.json` (puro, determinístico) |
| `Config/ConfigVO.cs` | `ReposProject` (XML) |
| `Program.cs` | flags `--includeRepos <bool>` / `--reposProject <name>`; busca repos após o split |

### Uso
```powershell
# Export MacGyver + split + repos da Integrações
dotnet run --project "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -- `
  --team macgyver --collection NDD-DECollection --project "Central de Soluções" `
  --mode all-artifacts --output output.json --includeRepos true --reposProject "Integrações"
```

### Saída adicional
```
export/macgyver/
  repos/
    index.json            {count, repos[]{name,path,defaultBranch}}
    <repo>/repo.json      {id,name,project,defaultBranch,size,remoteUrl,webUrl,branches[]}
```

## Testes (Tests/ReposTests.cs — 9 casos; total do projeto: 30)
- `BuildBaseUrl_EncodesAccentedProject` → `Integrações` ⇒ `.../Integra%C3%A7%C3%B5es`.
- RepoWriter: index+per-repo, campos, ordenação determinística, branches ordenadas, null sem throw, char inválido de pasta (invariante index→arquivo).
- Parse dos DTOs Git a partir de fixtures JSON.

## Limites / próximos passos
- `GitQueryService` faz HTTP real → validado com **dados reais** quando você rodar `--includeRepos true` (offline aqui foram testadas as partes puras + parsing).
- Falta o **join work item ↔ repo** (PR/commit/branch) — §12.6 (Fase 5 continuação).
- Commits/PRs ainda não exportados (só repos+branches nesta START).

# QA Gate — Fase 1 (Split do JSON)

> Consome o handoff `ENTREGA_PRONTA` (taskId `fase-1-split`).
> **Gate: ✅ APROVADO** · QA Loop: iteração 1.

## Escopo validado
`Services/HandoffSplitter.cs` + flags `--split`/`--splitFrom` em `Program.cs`;
logging via `ILogHelper`; enriquecimento `State`/`AcceptanceCriteria` (Fase 2a).

## Checklist

| # | Item | Resultado |
|---|------|-----------|
| 1 | `dotnet build` (Debug) | ✅ 0 erros (warnings = nullable pré-existentes do código legado) |
| 2 | Build inclui exclusão de `Tests/**` no projeto principal | ✅ corrigido (glob do SDK não compila mais o test project) |
| 3 | Testes automatizados | ✅ **21/21 passados** (`dotnet test`, 584 ms) |
| 4 | Contrato dos sub-arquivos (index/pbi/us) | ✅ counts, cross-refs pai↔filho, paths root-relativos |
| 5 | Sanitização — sem data-URI base64 nos JSONs do agent | ✅ coberto por teste dedicado |
| 6 | Extração de asset → arquivo válido | ✅ PNG real (magic bytes) em `assets/` |
| 7 | RawHtml preservado em `raw/`; `null` tratado | ✅ |
| 8 | Determinismo (mesmo input → mesmos bytes) | ✅ teste de duas execuções |
| 9 | Robustez (base64 malformado, chaves no título, coleções null) | ✅ não lança; sem leak |
| 10 | Segredos no output | ✅ splitter só toca campos do `Item`; PAT nunca serializado; `**/config.xml` no `.gitignore` |
| 11 | Logging JSON padrão | ✅ splitter loga início/resumo/warn via `ILogHelper` → `logs.json` |

## Cobertura de testes (21 casos)
Guard (null/args), estrutura/index, contagens, itens vazios, ordenação determinística,
duas execuções idênticas, sem-base64, extração PNG, URL externa como referência,
base64 malformado, raw/ presente e null, parentPbiId, paths root-relativos, PBI sem filhas,
nesting profundo, State+AcceptanceCriteria, attachments, título com chaves, coleções null.

## Ressalvas (não-bloqueantes — Fase 2)
- `acceptanceCriteria`/`state` agora são **capturados** no export (Fase 2a) e emitidos pelo split.
  Validar com dados reais do MacGyver no próximo export (esta rodada foi validada offline + unit).
- Avaliar perda de conteúdo do `ExtractTextFromHtml` (tabelas/listas) — `audit-sanitization`.

## Decisão
**APROVADO.** Base estável para a Fase 2 (escopo MacGyver + multi-collection) e Fase 3 (builds/logs).

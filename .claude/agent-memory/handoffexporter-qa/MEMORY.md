# HandoffExporter QA — Memory

> Atualizar após cada gate. **NÃO há auto-load** — só entra em contexto quando o agent
> o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Recurring Failure Patterns

- _(vazio)_

## Known False Positives

- _(vazio)_

## Checagens-chave

- Build limpo (atenção a warnings nullable/net10).
- JSON válido (`ConvertFrom-Json`) + contrato `HandoffJson`.
- Campo ausente = `null` (não string vazia).
- Sem segredos no output (`Select-String -Pattern '"Key"|PAT|Bearer'`).
- Determinismo: rodar 2× → mesma estrutura.

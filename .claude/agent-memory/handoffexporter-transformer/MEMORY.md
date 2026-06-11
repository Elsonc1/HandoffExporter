# HandoffExporter Transformer — Memory

> Atualizar após cada auditoria/desenho. **NÃO há auto-load** — só entra em contexto
> quando o agent o lê na FIRST ACTION.

## Active Tasks

_(nenhuma ainda)_

## Pipeline Map (refs rápidas)

- Seleção WIQL: `Services/WorkItemQueryService.cs`
- Detalhe + sanitização: `Services/TFSAplicationProcess.cs` (`GetWorkItemAsync`, `ExtractTextFromHtml`)
- Conteúdo/mapeamento de campos: `Program.ResolveContent`
- Hierarquia PBI→US: `Program.GetPBIWithChildren` / `BuildItemWithChildren` (relação `Hierarchy-Forward`)
- Montagem do Item + assets: `Program.CreateItem`

## Field Mapping Status

| Campo | Origem | Status |
|-------|--------|--------|
| US Description | `ndd.DefinicoesDeNegocio` → fallback `System.Description` | ok |
| US AcceptanceCriteria | `ndd.DefinicoesTecnicas` → fallback `Microsoft.VSTS.Common.AcceptanceCriteria` | ok (Fase 2a) |
| **VOs por tipo** | `Services/ContentResolver.cs` coleta TODOS os campos `ndd.*`/`NDD.*`/`nddd.*` (+ exatos VSTS) em `contentFields`; compõe description quando primária vazia | ok (2026-06-09) |
| PBI Compliance | SEM `System.Description`! Conteúdo em `ndd.PropostaFuncional` (~16k chars), `NDD.Objetivo`, `NDD.BeneficiosCliente`, `NDD.Discovery*`/`Sorting*` | descoberto via `--inspect 204055` |
| Issue (Suporte) | `System.Description` + `ndd.ModeloDescricao`/`1` + `Microsoft.VSTS.Common.ProductName/ModuleName/...` | visto no inspect 206366 |
| Prefixo `NDDigital.*` | (SLAPause, Priority) — metadado, NÃO é conteúdo | excluído do resolver |

## Quirks

- `ExtractTextFromHtml` usa `InnerText` — pode achatar tabelas/listas (avaliar perda em `audit-sanitization`).
- WIQL pagina em lotes de 100 ids.

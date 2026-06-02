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
| US Description | `ndd.DefinicoesDeNegocio` → fallback `System.Description` | ok (código atual) |
| US AcceptanceCriteria | `ndd.DefinicoesTecnicas` → fallback `Microsoft.VSTS.Common.AcceptanceCriteria` | **conferir** (dev-guide pede, código atual foca em Description) |

## Quirks

- `ExtractTextFromHtml` usa `InnerText` — pode achatar tabelas/listas (avaliar perda em `audit-sanitization`).
- WIQL pagina em lotes de 100 ids.

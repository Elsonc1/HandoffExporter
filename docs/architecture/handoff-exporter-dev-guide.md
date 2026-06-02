# HandoffExporter - Dev Guide

## Purpose

This document is the working guide for using HandoffExporter as an Azure DevOps export tool.

The tool is a context exporter only:

- It exports PBIs and linked User Stories.
- It does not create User Stories.
- It does not use local AI in this delivery.
- It is intended to provide structured context for future analysis, review, and handoff work.

## When to use it

Use HandoffExporter when the architect receives an Azure DevOps handoff and needs a deterministic export of:

- one specific PBI and its linked User Stories, or
- all PBIs for an area/team, with their linked User Stories.

For surgical changes, prefer a focused export by PBI id.
For broader context, use the area-based export.

## Current runtime flow

1. Load configuration from `bin/Debug/config/config.xml`.
2. Read CLI arguments when present.
3. Build the Azure DevOps connection using collection, project, and PAT.
4. Query PBIs by area or by a specific PBI id.
5. For each PBI, fetch linked User Stories through `System.LinkTypes.Hierarchy-Forward` relations.
6. Normalize the work item data into a JSON bundle.
7. Write the final export file to the configured output path.

## Supported inputs

### CLI

The current CLI accepts these arguments:

```bash
dotnet run -- --collection <collection> --project <project> --areaPath <areaPath> [--pbiId <id>] [--includeIssues <true/false>] --output <outputFile>
```

Notes:

- `--pbiId` is optional.
- `--includeIssues` is optional and only makes sense for area-based exports.
- `areaPath` is still required by the current validation.
- CLI arguments take precedence over the config file.

### Config file

When no CLI args are provided, the tool falls back to `bin/Debug/config/config.xml`.

Relevant config values:

- `Organization`
- `Project`
- `AreaOrId`
- `OutputFile`
- `Key` for PAT

## Output contract

The current source contract is a `HandoffJson` envelope with source data, request data, items, and handoff metadata.

```json
{
  "Source": {
    "Type": "azure-devops",
    "Collection": "NDD-DECollection",
    "Project": "Central de Solucoes"
  },
  "Request": {
    "AreaPath": "MacGyver",
    "PbiId": 193404,
    "IncludeIssues": false
  },
  "ExportedAtUtc": "2026-05-06T14:30:21.7261964Z",
  "Items": [],
  "Handoff": {
    "Version": "1.0",
    "Generator": "HandoffExporter"
  }
}
```

### Source

- `Type`: source system identifier, currently `azure-devops`.
- `Collection`: Azure DevOps collection.
- `Project`: Azure DevOps project.

### Request

- `AreaPath`: selected area when exporting by area.
- `PbiId`: selected PBI when exporting a focused tree.
- `IncludeIssues`: optional flag for area exports.

### Item

Each exported item should carry the following data:

- `Id`
- `WorkItemType`
- `Title`
- `RawHtml`
- `SanitizedText`
- `Assets`
- `Attachments`
- `Children`

For linked User Stories, `Children` must contain the nested work items under the PBI.

### Asset / Attachment

- `Asset` is used for embedded content or derived resources.
- `Attachment` is used for linked files or binary artifacts.

### Handoff

- `Version`: current export version.
- `Generator`: tool identifier.

## Field mapping rules

The export must reflect the real Nexus/Azure DevOps contract, not assumptions from the board UI.

Recommended mapping for User Stories:

- `Description` <- `ndd.DefinicoesDeNegocio`
- fallback `Description` <- `System.Description`
- `AcceptanceCriteria` <- `ndd.DefinicoesTecnicas`
- fallback `AcceptanceCriteria` <- `Microsoft.VSTS.Common.AcceptanceCriteria`

This matters because the Nexus creation flow stores the main content in custom fields.

For PBIs:

- keep the standard `System.*` fields;
- preserve linked children;
- keep custom content when present.

## Data quality rules

- If a field is missing in Azure DevOps, serialize it as `null`.
- Do not replace missing fields with empty text unless the contract explicitly requires it.
- Preserve the PBI even when it has no User Stories.
- Preserve all linked User Stories when the PBI has multiple children.
- Keep the export deterministic: same input should produce the same structure.

## What the architect should check before handing off

1. Decide whether the request is area-based or PBI-specific.
2. Validate the target PBI or area in Azure DevOps.
3. Confirm whether the request needs only PBIs and linked USs.
4. Avoid adding AI enrichment in this phase.
5. Make sure the output file is usable as context for future work.

## Recommended usage pattern

For a specific change:

1. Export a single PBI using `--pbiId`.
2. Inspect the linked User Stories in the JSON.
3. Hand the export to the dev as the context bundle.

For a broader review:

1. Export by `--areaPath`.
2. Review all PBIs in the area.
3. Use the output to compare scope, dependencies, and impacted stories.

## Troubleshooting

If a User Story comes with empty `Description`:

- check whether the value is stored in `ndd.DefinicoesDeNegocio` instead of `System.Description`;
- check the raw fields returned by Azure DevOps;
- confirm that the export query asks for the custom fields explicitly.

If a User Story comes with empty `AcceptanceCriteria`:

- check whether the value is stored in `ndd.DefinicoesTecnicas`;
- validate the raw payload from Azure DevOps;
- confirm the mapping used by the exporter.

## Scope of this version

This version only exports PBIs and linked User Stories.

It does not:

- generate User Stories;
- call local AI;
- create review summaries;
- perform semantic expansion of the export.

Those ideas can be documented later as a separate evolution.

## Suggested next step for the dev

Use this guide together with the current JSON output contract to keep the exporter focused on context export only.
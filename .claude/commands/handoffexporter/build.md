---
description: Compila o HandoffExporter (dotnet build) e reporta erros/warnings.
---

Execute o build do HandoffExporter:

```powershell
dotnet build "C:\Users\elson.lopes\source\repos\HandoffExporter\HandoffExporter.csproj" -c Debug
```

Reportar:
- Status (success / failed)
- Erros (com arquivo:linha)
- Warnings novos (atenção a nullable/net10; comparar com baseline se possível)
- Tempo total

NÃO tente corrigir erros — apenas reporte. Se for chamado dentro do fluxo dev,
o @handoffexporter-dev decide o que fazer.

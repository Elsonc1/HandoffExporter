---
description: Pergunta ao @handoffexporter-orchestrator quem deve assumir a próxima ação.
argument-hint: <descrição do que você quer fazer / estado atual>
---

Use o agent **@handoffexporter-orchestrator**.

Contexto:
$ARGUMENTS

O orchestrator deve:
1. Verificar handoffs pendentes (architect, dev, qa, transformer) no ledger `.claude/handoffs.json` (ou MCP)
2. Identificar a intenção do usuário
3. Selecionar agent + mission keyword
4. Retornar o bloco `[ROUTING]` conforme o template da seção 4 de `handoffexporter-orchestrator.md`
5. `<promise>ROUTED</promise>` + uma linha indicando o próximo responsável

Use isto quando estiver inseguro qual agent acionar OU ao retomar trabalho após pausa
(o orchestrator faz a triagem do estado).

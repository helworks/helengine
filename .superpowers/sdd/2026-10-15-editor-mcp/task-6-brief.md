### Tarefa 06 — Identidade de operação e resposta perdida

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationLedger.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorOperationLedgerTests.cs`. **Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationDispatcher.cs`.

**Interfaces:** `Task<EditorOperationResult> ExecuteOnceAsync(EditorOperationRequest request, Func<Task<EditorOperationResult>> execute)`, `EditorOperationResult Get(string bindingId, string operationId)`, `void ReleaseBinding(string bindingId)`; expor ferramenta `operation_get`. Unbind impede chamadas novas; trabalho já iniciado conserva seu registro até terminar, sem ser cancelado ou descartado por liberação prematura.

- [ ] Escrever `Duplicate_inflight_requests_share_one_execution`, `Lost_response_can_be_queried`, `Changed_payload_is_rejected`, `Capacity_does_not_evict_old_ids`; `Assert.Equal(1, effects); Assert.Equal(first.OperationId, second.OperationId); Assert.Equal("operation_id_conflict", changed.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorOperationLedgerTests`.
- [ ] Canonicalizar JSON por nomes de propriedades, preservando ordem de arrays; identidade inclui nome da ferramenta, alvo, geração, revisão e payload. Deduplicar antes da rechecagem de revisão para devolver resultado de chamada já aplicada. Consultas não consomem ledger. Limite de 10.000 IDs por vínculo; desconhecido após crash não vira autorização de replay.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: track editor mutation outcomes without duplicate effects`.



## Contexto de execução
Worktree C:\dev\helworks\helengine\.worktrees\editor-mcp, branch feature/editor-mcp. Substituir raiz original dos paths pela raiz deste worktree. Contrato compartilhado implementado na tarefa01; ver global-context.md para APIs.
Baseline: 25/25 testes construction/dispose passaram em código original. Para aproveitar cache do grafo do editor deste mesmo checkout, usar --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\baseline-20261015; preservar baseline.log/TRX, usar nome de log e diretório de fixtures específicos da tarefa. Cache NuGet original pode ser reutilizado com argumento quoted '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages'. Submódulos locais já inicializados em commits versionados.

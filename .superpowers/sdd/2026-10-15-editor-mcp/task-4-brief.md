### Tarefa 04 — Despacho na thread do frame e encerramento da fila

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\IEditorOperationDispatcher.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\IEditorOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationRegistry.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorOperationDispatcher.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorOperationDispatcherTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`. **Reutilizar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorMainThreadActionQueue.cs`.

**Interfaces:** produz os contratos de despacho da seção 2 e `void EditorOperationDispatcher.BeginShutdown()`. `TaskCompletionSource` usa continuações assíncronas; o handler não escapa com exceção para o frame.

- [ ] Escrever `Calls_run_on_frame_thread`, `Throwing_call_does_not_block_next`, `Cancellation_before_start_has_no_effect`, `Shutdown_completes_pending_calls`; `Assert.Equal(frameThreadId, handlerThreadId); Assert.True(next.Succeeded); Assert.True(allPendingCompleted)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorOperationDispatcherTests`.
- [ ] Enfileirar execução e snapshots destacados, limitar pendências, cancelar chamadas ainda não iniciadas e completar todas no shutdown. Nunca aguardar HTTP bloqueando a thread do frame.
- [ ] Verde com o mesmo filtro e testes existentes de frame após Dispose.
- [ ] Commit: `feat: dispatch editor automation safely through frame lifecycle`.



## Contexto de execução
Worktree C:\dev\helworks\helengine\.worktrees\editor-mcp, branch feature/editor-mcp. Substituir raiz original dos paths pela raiz deste worktree. Contrato compartilhado implementado na tarefa01; ver global-context.md para APIs.
Baseline: 25/25 testes construction/dispose passaram em código original. Para aproveitar cache do grafo do editor deste mesmo checkout, usar --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\baseline-20261015; preservar baseline.log/TRX, usar nome de log e diretório de fixtures específicos da tarefa. Cache NuGet original pode ser reutilizado com argumento quoted '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages'. Submódulos locais já inicializados em commits versionados.

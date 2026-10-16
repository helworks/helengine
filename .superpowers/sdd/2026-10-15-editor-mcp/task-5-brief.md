### Tarefa 05 — Revisões, gerações e histórico compartilhado

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorRevisionTracker.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorHistoryOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorRevisionTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\history\EditorUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\history\IEditorUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSceneMutationService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\testing\RecordingUndoRedoService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\hosting\EditorSceneWorkspaceCoordinatorTests.cs` para atualizar as duas implementações de teste da interface.

**Interfaces:** `string EditorRevisionTracker.Current`, `string Advance()`, `string ReplaceScene()`; adicionar `string Cursor` e descrição da operação disponível ao histórico, emitindo mudança para Record/Undo/Redo/Reset. Handler produz `history_get`, `history_undo`, `history_redo`.

- [ ] Escrever `Ui_edit_invalidates_mcp_revision`, `Undo_redo_advances_revision`, `Reload_rejects_reused_entity_id`, `Undo_rejects_changed_cursor`; asserções `Assert.NotEqual(before, after); Assert.Equal("revision_conflict", result.ErrorCode); Assert.Equal(userValue, valueAfterRejectedWrite)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorRevisionTests`.
- [ ] Validar revisão na mesma execução serializada da mutação. Integrar notificações existentes; as tarefas de domínio acrescentam avanço nos caminhos de UI ainda não cobertos. Manter IsAtSavedState separado de revisão de automação.
- [ ] Verde com o mesmo filtro e `FullyQualifiedName~EditorUndoRedoServiceTests|FullyQualifiedName~EditorSessionUndoRedoIntegrationTests`.
- [ ] Commit: `feat: reject stale editor operations and expose shared history`.



## Contexto de execução
Worktree C:\dev\helworks\helengine\.worktrees\editor-mcp, branch feature/editor-mcp. Substituir raiz original dos paths pela raiz deste worktree. Contrato compartilhado implementado na tarefa01; ver global-context.md para APIs.
Baseline: 25/25 testes construction/dispose passaram em código original. Para aproveitar cache do grafo do editor deste mesmo checkout, usar --artifacts-path C:\dev\helworks\builds\helengine\editor-mcp\baseline-20261015; preservar baseline.log/TRX, usar nome de log e diretório de fixtures específicos da tarefa. Cache NuGet original pode ser reutilizado com argumento quoted '-p:RestorePackagesPath=C:\dev\helworks\helengine\.nuget\packages'. Submódulos locais já inicializados em commits versionados.

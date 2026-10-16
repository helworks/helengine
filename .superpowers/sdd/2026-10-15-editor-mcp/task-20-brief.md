### Tarefa 20 — Play/Stop e fonte salva

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorPlayOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlayOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorPlayOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\play\EditorPlaySessionService.cs`.

**Interfaces:** `EditorJobSnapshot Start(string platformId, bool waitForDebugger, string unsavedPolicy)`, `void Stop()`, `JsonElement GetState()`, `void ApplySettings(string platformId, JsonElement patch)`; API MCP padrão `fail`, UI conserva a política atual de salvar antes de Play passando `save` explicitamente.

- [ ] Escrever `Dirty_scene_mcp_play_fails_without_save_policy`, `Ui_play_keeps_existing_save_behavior`, `Play_returns_real_preparation_and_launch_outcome`, `Stop_does_not_dispose_editor`; `Assert.Equal("unsaved_changes", result.ErrorCode); Assert.True(uiSaved); Assert.False(editorDisposed)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorPlayOperationTests`.
- [ ] Compartilhar operação de Play sem chamar diretamente o handler de UI que salva. Capturar configurações, fonte salva e revisão usada; respeitar bloqueios durante preparação e capacidades de plataforma. Não inventar pause/step quando indisponíveis.
- [ ] Verde com o mesmo filtro e testes existentes de PlaySession/process/shutdown.
- [ ] Commit: `feat: automate play lifecycle with explicit save policy`.



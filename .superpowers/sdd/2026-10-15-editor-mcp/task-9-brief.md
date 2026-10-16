### Tarefa 09 — Composição no app, status e shutdown

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAutomationSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp\EditorMcpLifetime.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorMcpConnectionsDialog.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.mcp.tests\EditorMcpLifetimeTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj`; `C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\MainForm.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\EditorTitleBar.cs`.

**Interfaces:** `EditorSession.Automation` fornece objeto explícito da sessão; `Task EditorMcpLifetime.StartAsync(CancellationToken token)`, `Task StopAsync(CancellationToken token)`. `EditorAutomationSession` implementa `IEditorOperationHandler.Execute` para `editor_get_state`, `editor_get_capabilities` e `editor_close`, delegando o fechamento ao fluxo existente e à política da tarefa 10. Status alimenta diálogo por snapshot, sem atualizar controles pela thread HTTP.

- [ ] Escrever `Startup_failure_is_visible_and_unpublished`, `Shutdown_rejects_calls_before_dispose`, `Unbind_does_not_close_editor`, `Command_line_build_does_not_start_mcp`; `Assert.Empty(registryAfterFailure); Assert.True(allRequestsCompleted); Assert.False(editorDisposedAfterUnbind)`.
- [ ] Vermelho: `$Mcp`, `FullyQualifiedName~EditorMcpLifetimeTests`.
- [ ] Compor host após sessão pronta; mostrar endereço, projeto, estado, erro e vínculos. Encerrar aceitação, invalidar/despublicar, completar chamadas, parar host e só então liberar recursos. Não alterar modos CLI de Program nem encerrar o editor ao falhar MCP. Preservar o guard de cena suja.
- [ ] Verde com o mesmo filtro e testes existentes de construção/dispose da sessão.
- [ ] Commit: `feat: integrate MCP startup status and shutdown with editor lifetime`.



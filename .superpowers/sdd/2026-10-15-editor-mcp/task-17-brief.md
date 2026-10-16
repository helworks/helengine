### Tarefa 17 — Jobs, cancelamento e logs

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorJobService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorLogBuffer.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorJobOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorJobServiceTests.cs`.

**Interfaces:** `EditorJobSnapshot Start(string kind, Func<CancellationToken, Task<JsonElement>> work)`, `EditorJobSnapshot Get(string jobId)`, `EditorJobSnapshot Cancel(string jobId)`, `JsonElement List(string cursor, int limit)`; logs `void Append(string level, string text, string jobId)` e `JsonElement Read(string cursor, int limit, string level, string jobId)`. Jobs pertencem à instância/projeto e são consultáveis por novo vínculo válido.

- [ ] Escrever `Request_disconnect_does_not_cancel_job`, `Cancellation_requested_is_not_cancelled`, `Concurrent_status_remains_responsive`, `Log_retention_reports_gap`, `Shutdown_observes_worker_failures`; `Assert.Equal("cancel_requested", pending.State); Assert.True(cursorGapReported); Assert.False(secretAppearsInLog)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorJobServiceTests`.
- [ ] Implementar máquina de estados e buffer com sequência/cursor e limite de 10.000 registros. Separar CancellationToken de request e de job. Não usar logger estático como identidade de projeto; filtrar/atribuir origem na captura da sessão. Documentar efeitos já aplicados quando cancelamento não os reverte.
- [ ] Verde com o mesmo filtro.
- [ ] Commit: `feat: track editor jobs and diagnostics independently of requests`.



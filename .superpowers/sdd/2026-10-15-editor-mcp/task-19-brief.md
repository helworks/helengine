### Tarefa 19 — Builds, fila e artefatos

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorBuildOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildJobRunner.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorBuildOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorBuildQueueService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorPlatformBuildExecutor.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\BuildDialog.cs`.

**Interfaces:** `EditorJobSnapshot Start(string queueItemId, string unsavedPolicy)`, `JsonElement GetQueue()`, `void ApplyQueue(JsonElement change)`; resultado de job declara entradas salvas, plataformas, perfis, output absoluto e artefatos.

- [ ] Escrever `Build_does_not_implicitly_save_live_scene`, `Queue_snapshot_is_stable_during_execution`, `Unsupported_cancel_is_not_reported_as_cancelled`, `Play_preparation_excludes_conflicting_build`; `Assert.Equal(beforeDisk, afterRejectedBuild); Assert.Equal("operation_busy", conflicting.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorBuildOperationTests`.
- [ ] Extrair execução síncrona da fila para coordenação com snapshots/jobs, preservando exclusões atuais de caches. Cobrir adicionar/remover item, copiar settings entre plataformas, resetar cenas para projeto e acompanhar resultados. Manter dependências de frame no frame; cancelar processos filhos apenas por mecanismo suportado.
- [ ] Verde com o mesmo filtro e testes existentes de build queue/config/copy settings; executores de teste, sem compilar todas as plataformas.
- [ ] Commit: `feat: expose responsive build queue jobs and explicit input policy`.



### Tarefa 18 — Imports, reimport e caches

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorImportOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorImportJobRunner.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorImportOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\AssetImportExecutionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AssetImportSettingsView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`.

**Interfaces:** `EditorJobSnapshot Start(string assetId, JsonElement scope, bool force)`; settings via `JsonElement ReadSettings(string assetId, JsonElement scope)` e `void ApplySettings(string assetId, JsonElement scope, JsonElement patch)`. `scope` usa o objeto discriminado da matriz.

- [ ] Escrever `Import_settings_roundtrip_scopes`, `Reimport_refreshes_loaded_asset`, `Cancel_before_publish_keeps_previous_asset`, `Render_resource_commit_returns_to_frame_thread`; `Assert.Equal(previousBytes, cancelledBytes); Assert.Equal(frameThreadId, commitThreadId)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorImportOperationTests`.
- [ ] Integrar imports com jobs e autoria existente, capturar entradas antes de processamento e publicar recursos na thread correta. Reusar exclusão de escrita do projeto; preservar import anterior quando falhar antes do commit. Mostrar etapa não cancelável explicitamente.
- [ ] Verde com o mesmo filtro e testes existentes de import settings/asset invalidation.
- [ ] Commit: `feat: expose tracked import jobs and scoped import settings`.



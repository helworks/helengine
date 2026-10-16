### Tarefa 10 — Cenas, salvamento e exportação

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorSceneOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorSceneDocumentOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorSceneOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorSceneLifecycleService.cs`. Reutilizar SceneSaveService, SceneFileLoadService e EditorSceneWorkspaceCoordinator existentes.

**Interfaces:** handler `Execute` da seção 2; serviço compartilhado `void Save(string absolutePath)`, `void Open(string absolutePath, string unsavedPolicy, string savePath)`, `void Create(string unsavedPolicy, string savePath)`, `void Close(string unsavedPolicy, string savePath)`, `void Reload(string unsavedPolicy, string savePath)`, `void ApplySettings(JsonElement patch)`, `void Export(string absolutePath)`. Políticas aceitas: `fail`, `save`, `discard`; `savePath` pode ser vazio somente quando não é preciso salvar uma cena sem destino. Operações e payloads na matriz.

- [ ] Escrever `Dirty_scene_requires_explicit_transition_policy`, `Mcp_edit_does_not_save_until_requested`, `Reload_changes_handle`, `Export_uses_existing_scene_export_path`; comparar bytes de disco antes/depois, `Assert.Equal("unsaved_changes", result.ErrorCode); Assert.NotEqual(oldHandle, newHandle)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorSceneOperationTests`.
- [ ] Extrair os caminhos dos handlers de save/open/export sem criar diálogo na chamada MCP. Salvar cena nova exige caminho; salvar/falhar mantém dirty correto. Substituir callbacks da UI pelos mesmos serviços e avançar revisão.
- [ ] Verde com o mesmo filtro e testes existentes de scene open/save/startup.
- [ ] Commit: `feat: expose scene document operations with explicit save policy`.



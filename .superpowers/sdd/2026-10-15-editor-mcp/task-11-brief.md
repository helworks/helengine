### Tarefa 11 — Hierarquia e criação de entidades

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorEntityOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\EditorEntityAuthoringOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorEntityOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\SceneHierarchyPanel.cs`. Reutilizar EditorSceneEntityCreationService, EditorEntityReparentService, BlueprintEditorReadOnlyService e histórico existentes.

**Interfaces:** `EditorEntity Create(string kind, string name, uint parentId)`, `EditorEntity InstantiateAsset(string assetId, uint parentId)`, `void Delete(uint entityId)`, `EditorEntity Duplicate(uint entityId)`, `void Reparent(uint entityId, uint parentId)`, `void Apply(uint entityId, JsonElement scope, JsonElement patch)`; criação aceita tipos catalogados, incluindo Empty/Cube/Plane/Camera e quatro luzes. O adaptador converte somente a raiz explicitamente validada para o sentinela interno `0`.

- [ ] Escrever `Create_types_match_add_menu`, `Explicit_target_ignores_selection`, `Reparent_rejects_cycle`, `Inherited_blueprint_entity_remains_protected`, `Duplicate_and_delete_roundtrip_history`; `Assert.Equal(expectedKinds, actualKinds); Assert.Equal(beforeHierarchy, afterUndo)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorEntityOperationTests`.
- [ ] Expor hierarquia paginada, nomes, ativação, transforms e existência por escopo; validar handles antes de resolver IDs. Integrar atalhos/menu aos serviços, inclusive Add to scene de modelos/blueprints.
- [ ] Verde com o mesmo filtro e testes existentes de reparent/blueprint/read-only.
- [ ] Commit: `feat: automate scene hierarchy through shared authoring operations`.



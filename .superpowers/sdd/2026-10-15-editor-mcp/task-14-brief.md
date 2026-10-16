### Tarefa 14 — Editores especializados, blueprints e modificadores

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAssetDocumentOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetDocumentOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAssetDocumentOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\MaterialAssetView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\AnimationClipAssetView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\MeshComponentModifierStackService.cs`. Reutilizar MaterialAssetSettingsService, AnimationClipPlatformResolutionService e BlueprintSaveService existentes.

**Interfaces:** `JsonElement GetSchema(string assetId, JsonElement scope)`, `JsonElement Read(string assetId, JsonElement scope)`, `void Apply(string assetId, JsonElement scope, JsonElement patch)`. `scope` usa o objeto discriminado da matriz. Modificadores são operações de componente com índice/ID da pilha e ordem explícitos, não edição genérica de bytes.

- [ ] Escrever `Material_and_animation_edits_roundtrip_existing_serializers`, `Asset_scope_does_not_leak`, `Modifier_reorder_updates_preview_and_history`, `Blueprint_authored_edits_preserve_inherited_protection`; `Assert.Equal(expectedFields, reopenedFields); Assert.Equal(beforeCommon, afterCommon)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorAssetDocumentOperationTests`.
- [ ] Extrair commits dos controles para serviços; schemas refletem propriedades já editáveis e suas validações. Incluir salvar/autoria de blueprint e sua instanciação com os serviços existentes. Nenhum editor especializado pode ser omitido por ausência de um formulário genérico.
- [ ] Verde com o mesmo filtro e testes existentes de materiais/animações/blueprint/modifiers.
- [ ] Commit: `feat: expose specialized asset authoring and modifier operations`.



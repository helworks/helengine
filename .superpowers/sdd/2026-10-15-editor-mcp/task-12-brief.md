### Tarefa 12 — Componentes, propriedades e overrides

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorComponentOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\inspection\EditorComponentPropertySchemaService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorComponentOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\inspection\ComponentPropertyEditController.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ComponentPropertiesView.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorGameScriptHotReloadService.cs`.

**Interfaces:** `JsonElement Describe(string componentTypeId, string schemaGeneration)` e `object DecodeValue(string componentTypeId, string propertyPath, JsonElement value)` produzem somente propriedades editáveis já descritas pelo inspector; handler usa ComponentPropertyEditRequest e escopo explícito.

- [ ] Escrever `Custom_component_schema_is_editable`, `Platform_edit_preserves_common_value`, `Environment_override_is_isolated`, `Readonly_and_stale_schema_are_rejected`, `Nested_arrays_enums_and_references_validate`; `Assert.Equal(commonBefore, commonAfter); Assert.Equal("stale_schema", stale.ErrorCode)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorComponentOperationTests`.
- [ ] Implementar descoberta, add/remove, get/set de propriedade e limpar override. Converter JSON pelo schema; rejeitar NaN, infinito, enum inválido e referência incompatível. Reusar validação/histórico do controller e atualizar geração após hot reload.
- [ ] Verde com o mesmo filtro e testes existentes de propriedades/history/override.
- [ ] Commit: `feat: automate typed component editing and scoped overrides`.



### Tarefa 13 — Biblioteca, arquivos de assets e referências

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorAssetOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetLibraryOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorAssetOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetFileOperationService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserPanel.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserView.cs`.

**Interfaces:** handler `Execute`; serviços `void CreateFolder(string absolutePath)`, `void CreateFromTemplate(string templateId, string absolutePath)`, `void Move(string sourcePath, string destinationPath)`, `void Duplicate(string sourcePath, string destinationPath)`, `void Delete(string absolutePath)`. Autoria/referências passam pela sessão existente.

- [ ] Escrever `Move_preserves_identity_and_references`, `Duplicate_gets_distinct_identity`, `Folder_operations_stay_under_project_assets`, `Unicode_spaces_case_and_junctions_are_validated`, `External_changes_advance_revision`; `Assert.Equal(oldId, movedId); Assert.NotEqual(oldId, duplicateId); Assert.False(outsidePathChanged)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorAssetOperationTests`.
- [ ] Consultar biblioteca com filtro/cursor, expor templates, criar/mover/duplicar/excluir com política de dependências explícita. Renomear usa Move. Atualizar índices e autoria; não sobrescrever arquivo externo nem reparar identidade silenciosamente. Retornar relatórios de reparo existentes.
- [ ] Verde com o mesmo filtro e testes existentes de asset file operations/authoring.
- [ ] Commit: `feat: automate asset library operations preserving identities`.



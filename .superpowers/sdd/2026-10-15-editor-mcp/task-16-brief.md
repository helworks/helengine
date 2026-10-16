### Tarefa 16 — Seleção, viewport, workspace e preferências

**Criar:** `C:\dev\helworks\helengine\engine\helengine.editor\automation\EditorWorkspaceOperationHandler.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\managers\workspace\EditorWorkspaceOperations.cs`; `C:\dev\helworks\helengine\engine\helengine.editor.tests\automation\EditorWorkspaceOperationTests.cs`.

**Modificar:** `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\EditorSelectionService.cs`; `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PreviewPanel.cs`.

**Interfaces:** `JsonElement GetPanel(string panelId)`, `void SetPanelState(string panelId, JsonElement state)`, `void SaveLayout(int slot)`, `void LoadLayout(int slot)`; handler registra seleção, frame/navigation/projection/gizmo/snap, preview e preferências, delegando a serviços existentes.

- [ ] Escrever `Viewport_target_is_explicit_with_multiple_panels`, `Layout_slots_are_1_through_5`, `Preview_lock_survives_selection_change`, `Preferences_persist_without_dirtying_scene`; `Assert.Equal(beforeOtherViewport, afterOtherViewport); Assert.False(sceneBecameDirty)`.
- [ ] Vermelho: `$Editor`, `FullyQualifiedName~EditorWorkspaceOperationTests`.
- [ ] Extrair comandos de painel/layout de EditorSession; preservar múltiplos painéis e IDs por instância. Aplicar framing/navigation por controller, não input simulado. Editar tema/UI scale/snap com seus serviços. Mouse hover e pintura não são comandos persistentes.
- [ ] Verde com o mesmo filtro e testes existentes de workspace, framing, preview e preferences.
- [ ] Commit: `feat: automate editor workspace selection and viewport state`.



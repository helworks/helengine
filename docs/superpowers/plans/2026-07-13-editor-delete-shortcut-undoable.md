# Editor Delete Shortcut Undoable Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Delete` support for removing the currently selected authored scene entity through the editor-global shortcut path and make that deletion fully undoable and redoable.

**Architecture:** Reuse the existing global shortcut routing in `EditorKeyboardFocusUpdateComponent` and `EditorSession`, add one explicit entity-deletion history operation, and record deletion through `EditorMutationService` before disposing the live entity. Keep selection behavior simple: delete clears selection, undo restores and reselects the entity, redo deletes and clears selection again.

**Tech Stack:** C#, xUnit, existing editor undo/redo history services, editor keyboard focus system

---

### Task 1: Restore the test harness so delete TDD can run

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionUndoRedoIntegrationTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionHistoryBridgeLifecycleTests.cs`
- Modify: `engine/helengine.editor.tests/EditorComponentHistoryMutationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/DemodiscTiltTrialEditorSessionCloseTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Fix the stale `OpenFileDialog.Show()` call in the undo/redo integration tests**

```csharp
openFileDialog.Show(string.Empty);
```

- [ ] **Step 2: Fix missing `Project` type resolution in editor-session test files by importing the projectfile namespace**

```csharp
using helengine.projectfile;
```

- [ ] **Step 3: Replace the removed `SerializedEditorEntityState` constructor calls with explicit object initializers**

```csharp
SerializedEditorEntityState expectedState = new SerializedEditorEntityState {
    EntityId = 7u,
    ParentEntityId = 0u,
    EntityAsset = new SceneEntityAsset {
        Id = 7u,
        Name = "Sprite Entity"
    },
    AssetReferences = Array.Empty<SceneAssetReference>()
};
```

- [ ] **Step 4: Replace the unavailable `GDIFontProcessor` dependency in the close test with a deterministic local font factory already valid for test use**

```csharp
return new FontAsset(
    new FontInfo("Test", 16, 4f),
    new TestRuntimeTexture {
        Width = 64,
        Height = 64
    },
    characters,
    16f,
    64,
    64);
```

- [ ] **Step 5: Run the test project build to verify the harness is unblocked**

Run: `rtk dotnet build engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --verbosity minimal`

Expected: build reaches the current test assembly without the prior constructor/import/signature errors.

### Task 2: Write failing delete-shortcut integration tests

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionUndoRedoIntegrationTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Add a direct session test that deletes the selected authored entity and records the dirty state**

```csharp
[Fact]
public void Handle_global_delete_shortcut_deletes_the_selected_scene_entity() {
    EditorSession session = CreateSessionForUndoRedo();
    EditorEntity selectedEntity = CreateUserSceneEntity(300u, "Delete Me");
    EditorSelectionService.SetSelectedEntity(selectedEntity);

    InvokePrivate(session, "HandleGlobalDeleteShortcut");

    Assert.Equal(0, CountUserSceneEntities());
    Assert.Null(EditorSelectionService.SelectedEntity);
    Assert.True(GetPrivateField<bool>(session, "IsSceneDirty"));
}
```

- [ ] **Step 2: Add an undo/redo round-trip test for deleted entities**

```csharp
[Fact]
public void Handle_global_delete_shortcut_can_be_undone_and_redone() {
    EditorSession session = CreateSessionForUndoRedo();
    EditorEntity selectedEntity = CreateUserSceneEntity(301u, "Delete Undo");
    EditorSelectionService.SetSelectedEntity(selectedEntity);

    InvokePrivate(session, "HandleGlobalDeleteShortcut");
    InvokePrivate(session, "HandleGlobalUndoShortcut");
    InvokePrivate(session, "HandleGlobalRedoShortcut");

    Assert.Equal(0, CountUserSceneEntities());
}
```

- [ ] **Step 3: Add a modal-blocking test for delete**

```csharp
[Fact]
public void Handle_global_delete_shortcut_when_open_map_dialog_is_visible_does_not_delete_the_selected_entity() {
    EditorSession session = CreateSessionForUndoRedo();
    EditorEntity selectedEntity = CreateUserSceneEntity(302u, "Blocked Delete");
    OpenFileDialog openFileDialog = GetPrivateField<OpenFileDialog>(session, "openFileDialog");
    EditorSelectionService.SetSelectedEntity(selectedEntity);

    openFileDialog.Show(string.Empty);
    InvokePrivate(session, "HandleGlobalDeleteShortcut");

    Assert.Equal(1, CountUserSceneEntities());
    Assert.Same(selectedEntity, EditorSelectionService.SelectedEntity);
}
```

- [ ] **Step 4: Add keyboard-focus routing coverage for `Keys.Delete`**

```csharp
[Fact]
public void Keyboard_focus_update_component_routes_delete_into_the_session_handler() {
    EditorSession session = CreateSessionForUndoRedo();
    EditorEntity selectedEntity = CreateUserSceneEntity(303u, "Delete Key");
    EditorSelectionService.SetSelectedEntity(selectedEntity);
    EditorKeyboardFocusUpdateComponent component = new EditorKeyboardFocusUpdateComponent {
        DeleteShortcutRequested = CreatePrivateActionDelegate(session, "HandleGlobalDeleteShortcut")
    };

    AdvanceToNeutralFrame();
    InputBackend.SetKeyboardState(new KeyboardState(Keys.Delete));
    InputBackend.EarlyUpdate();
    component.Update();

    Assert.Equal(0, CountUserSceneEntities());
}
```

- [ ] **Step 5: Run the focused test filter and verify the new tests fail for the missing delete feature**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~Delete_shortcut" --verbosity minimal`

Expected: FAIL with missing delete shortcut handler or missing delete history behavior.

### Task 3: Implement delete history and global shortcut routing

**Files:**
- Create: `engine/helengine.editor/history/EntityDeletionHistoryOperation.cs`
- Modify: `engine/helengine.editor/history/EditorMutationService.cs`
- Modify: `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionUndoRedoIntegrationTests.cs`

- [ ] **Step 1: Add the explicit deletion history operation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Reverses and reapplies one authored entity deletion.
    /// </summary>
    public class EntityDeletionHistoryOperation : IEditorHistoryOperation {
        readonly SerializedEditorEntityState EntityState;

        public EntityDeletionHistoryOperation(SerializedEditorEntityState entityState) {
            EntityState = entityState ?? throw new ArgumentNullException(nameof(entityState));
        }

        public string Description {
            get { return "Delete Entity"; }
        }

        public void Undo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.RestoreEntity(EntityState);
            context.RestoreSelectionByEntityId(EntityState.EntityId);
        }

        public void Redo(EditorHistoryContext context) {
            if (context == null) {
                throw new ArgumentNullException(nameof(context));
            }

            context.DeleteEntityById(EntityState.EntityId);
            context.ClearSelection();
        }
    }
}
```

- [ ] **Step 2: Add explicit deletion recording to `EditorMutationService`**

```csharp
public void RecordDeletedEntity(EditorEntity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    UndoRedoService.Record(new EntityDeletionHistoryOperation(
        HistoryCaptureService.CaptureEntity(entity)));
    MarkSceneMutated();
}
```

- [ ] **Step 3: Extend keyboard shortcut routing with one delete callback**

```csharp
/// <summary>
/// Callback invoked when the editor-global delete shortcut is pressed.
/// </summary>
public Action DeleteShortcutRequested { get; set; }

// inside Update():
} else if (input.WasKeyPressed(Keys.Delete)) {
    if (DeleteShortcutRequested != null) {
        DeleteShortcutRequested();
    }
}
```

- [ ] **Step 4: Wire the callback and implement the guarded delete handler in `EditorSession`**

```csharp
var keyboardFocusUpdateComponent = new EditorKeyboardFocusUpdateComponent {
    UpdateOrder = core.ObjectManager.GetUpdateOrderForLayer(1),
    SaveShortcutRequested = HandleGlobalSaveShortcut,
    UndoShortcutRequested = HandleGlobalUndoShortcut,
    RedoShortcutRequested = HandleGlobalRedoShortcut,
    DeleteShortcutRequested = HandleGlobalDeleteShortcut
};
```

```csharp
void HandleGlobalDeleteShortcut() {
    if (IsEditorGlobalShortcutBlocked()) {
        return;
    }

    EditorEntity entity = ResolveSelectedDeletableSceneEntity();
    if (entity == null) {
        return;
    }

    HistoryMutationService.RecordDeletedEntity(entity);
    EditorSelectionService.ClearSelection();
    DeleteSceneEntityById(GetSceneEntityId(entity));
}
```

- [ ] **Step 5: Add the helper that limits delete to authored scene entities**

```csharp
EditorEntity ResolveSelectedDeletableSceneEntity() {
    if (EditorSelectionService.SelectedEntity is not EditorEntity editorEntity) {
        return null;
    }
    if (editorEntity.IsDisposed || editorEntity.InternalEntity) {
        return null;
    }
    if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
        return null;
    }

    return editorEntity;
}
```

- [ ] **Step 6: Run the focused delete tests and verify they pass**

Run: `rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~delete" --verbosity minimal`

Expected: PASS for the newly added delete shortcut tests.

- [ ] **Step 7: Run the editor project build as the final production validation**

Run: `rtk dotnet build engine\helengine.editor\helengine.editor.csproj --no-restore --verbosity minimal`

Expected: build succeeds with no errors.

# Add Model To Scene Context Action Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `Add to scene` context-menu action for model assets. The action should create a scene entity that uses the clicked model, preserve imported materials when present, place the entity at the center of the last clicked viewport's orbit target, select the new entity, and refresh the editor scene UI.

**Architecture:** Keep the context-menu trigger in the asset browser, keep scene mutation in `EditorSession`/scene creation services, and keep viewport target resolution inside the viewport/workspace layer. The asset browser should only identify the clicked model entry and raise an intent event. The session should resolve the model asset plus imported material set, choose the last focused viewport orbit target, and ask the scene creation service to spawn the entity. The scene creation service should own the actual entity construction so the model path is reused consistently for future scene creation code.

**Tech Stack:** C#, existing editor UI components, existing asset import helpers, existing scene creation services, xUnit, existing editor test doubles.

---

### Task 1: Add the model-only context menu action to the asset browser

**Files:**
- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Test `engine/helengine.editor.tests/AssetBrowserTabVisibilityTests.cs`

- [ ] **Step 1: Write the failing browser menu tests**

Add one test that right-clicking a model row shows `Add to scene` in the asset context menu.

Add one test that the new menu item is not shown for folders or non-model files.

Add one test that choosing `Add to scene` from a model row raises the browser-to-session intent with the clicked model entry.

Example test shape:

```csharp
/// <summary>
/// Verifies that model rows expose the add-to-scene action and that non-model rows do not.
/// </summary>
[Fact]
public void AssetBrowserPanel_WhenRightClickingModelRow_ShowsAddToSceneMenuItem() {
    string projectRoot = CreateProjectRoot();
    File.WriteAllText(Path.Combine(projectRoot, "assets", "cube.obj"), "dummy");
    File.WriteAllText(Path.Combine(projectRoot, "assets", "notes.txt"), "dummy");

    AssetBrowserPanel panel = CreateBrowserPanel(projectRoot);
    AssetBrowserView view = GetPrivateField<AssetBrowserView>(panel, "BrowserView");

    AssetBrowserEntry modelEntry = FindEntry(view, "cube.obj");
    AssetBrowserEntry textEntry = FindEntry(view, "notes.txt");

    panel.HandleContextMenuRightClick(GetRowCenter(view, modelEntry));
    Assert.True(ContextMenuHasItem(panel, "Add to scene"));

    panel.HandleContextMenuRightClick(GetRowCenter(view, textEntry));
    Assert.False(ContextMenuHasItem(panel, "Add to scene"));
}
```

- [ ] **Step 2: Run the focused browser test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~AssetBrowserTabVisibilityTests -v minimal
```

Expected: the `Add to scene` assertions fail because the asset context menu does not yet expose the model-only action.

- [ ] **Step 3: Add the model-only menu item and intent event**

Update `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs` so `CreateAssetItems` appends `Add to scene` only when the current row is a model entry.

Add a new intent event on the panel, for example:

```csharp
/// <summary>
/// Raised when the user requests to place the selected model asset into the current scene.
/// </summary>
public event Action<AssetBrowserEntry> AddModelToSceneRequested;
```

Raise the event from the new context-menu item, and keep the selection-before-menu behavior from the existing right-click handling path.

If `AssetBrowserView` needs a helper to inspect the clicked row kind without activating it, add the helper there instead of duplicating hit-testing logic in the panel.

- [ ] **Step 4: Re-run the browser tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~AssetBrowserTabVisibilityTests -v minimal
```

Expected: the model-only menu item is visible and the non-model cases stay hidden.

---

### Task 2: Resolve the last clicked viewport orbit target and spawn the model entity there

**Files:**
- Modify `engine/helengine.editor/EditorSession.cs`
- Modify `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Modify `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Possibly modify `engine/helengine.editor/content/model/ImportedModelAssetSet.cs` only if the spawn path needs a helper to read generated materials more cleanly
- Test `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
- Test `engine/helengine.editor.tests/testing/TestModelImporter.cs` only if the model-binding test needs an extra helper

- [ ] **Step 1: Write the failing session placement test**

Add one test that:
- creates a scene with at least one focused viewport
- gives that viewport a known orbit target
- invokes the new `Add to scene` model action
- asserts the new entity is created at the viewport orbit target
- asserts the entity is selected after creation

Add one test that when no viewport is focused, the session falls back to the primary viewport orbit target rather than inventing a default position.

Add one test that a multi-submesh model spawned through this path keeps its imported materials, not just a single fallback material.

Example test shape:

```csharp
/// <summary>
/// Verifies that adding a model to the scene places the spawned entity at the focused viewport orbit target.
/// </summary>
[Fact]
public void AddModelToScene_WhenViewportIsFocused_SpawnsEntityAtOrbitTarget() {
    EditorSession session = CreateSessionWithViewport();
    EditorViewport viewport = GetFocusedViewport(session);
    SetViewportOrbitTarget(viewport, new float3(12f, 3f, -8f));

    AssetBrowserEntry modelEntry = CreateModelEntry("assets/models/box.obj");
    session.HandleAddModelToSceneRequested(modelEntry);

    EditorEntity created = session.SelectedSceneEntity;
    Assert.Equal(new float3(12f, 3f, -8f), created.Position);
    Assert.Equal("box", created.Name);
}
```

- [ ] **Step 2: Run the focused session test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSessionWorkspaceTests -v minimal
```

Expected: the new placement tests fail because the session does not yet route model creation from the browser context menu.

- [ ] **Step 3: Expose the viewport orbit target through the workspace layer**

Add a public accessor for the camera orbit target on `EditorViewportCameraController` or a narrow session-facing helper on `ViewportWorkspacePanelController`.

Prefer the smallest API that still lets the session resolve:
- the last focused viewport
- that viewport's orbit target

If `EditorViewport` already tracks whether its content is focused, expose that state through a read-only property so the session can identify the most recent clicked viewport without peeking into private fields.

The session should:
- prefer the most recently focused viewport with a valid orbit target
- fall back to the primary viewport if nothing is focused
- use the orbit target as the spawn position

- [ ] **Step 4: Add a model creation helper to the scene creation service**

Extend `EditorSceneCreationService` with a model-specific creation method instead of forcing the browser/session to assemble a mesh entity manually.

The helper should:
- create a normal scene entity
- add a `MeshComponent`
- assign the `RuntimeModel`
- assign all runtime materials for the model submeshes
- attach the entity save metadata so the model and materials persist correctly
- place the entity at the resolved orbit target
- leave rotation and scale at sane defaults

The helper should not guess materials. If the model came from an imported source with generated materials, use those materials so the spawned entity renders the same way as the imported preview.

- [ ] **Step 5: Wire the browser intent through the session**

In `EditorSession`, subscribe to `AddModelToSceneRequested` from the asset browser panel(s).

When the event fires:
- resolve the clicked model asset
- load or reuse the imported model metadata so generated materials can be bound
- spawn the entity at the focused viewport orbit target
- select the new entity
- refresh the hierarchy and dirty-state indicators

Keep the mutation in the session or scene creation service, not in the browser panel.

- [ ] **Step 6: Re-run the focused session tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSessionWorkspaceTests -v minimal
```

Expected: placement, selection, and material-binding tests pass.

---

### Task 3: Verify the whole feature end to end

**Files:**
- Re-run `engine/helengine.editor.tests/AssetBrowserTabVisibilityTests.cs`
- Re-run `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
- Re-run any model-material regression test in `engine/helengine.editor.tests/AssimpModelImporterTests.cs` if the spawn helper needs validation against multi-submesh imports

- [ ] **Step 1: Run the focused browser and session tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~AssetBrowserTabVisibilityTests -v minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~EditorSessionWorkspaceTests -v minimal
```

- [ ] **Step 2: Build the editor and tests**

Run:

```powershell
rtk dotnet build engine\helengine.editor\helengine.editor.csproj --no-restore
rtk dotnet build engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore
```

- [ ] **Step 3: Confirm the user-facing behavior**

Verify in-editor that:
- right-clicking a model selects it first
- `Add to scene` appears only for model assets
- the spawned entity appears at the last clicked viewport's orbit target
- the spawned entity is selected after creation
- imported materials still render correctly on multi-submesh models


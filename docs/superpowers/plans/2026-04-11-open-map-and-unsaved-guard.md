# Open Map And Unsaved Guard Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Open Map...` plus a `Save / Don't Save / Cancel` guard for `Open Map...` and `New Map`, while keeping the current scene intact when loading fails.

**Architecture:** Extend `EditorTitleBar` with an `Open Map...` command, add two editor-owned modals (`OpenFileDialog` and `UnsavedChangesDialog`), and route scene navigation through `EditorSession`. Use a small scene-mutation notification service so `Add`, gizmo drags, and property edits can mark the current scene dirty without coupling low-level editor components back to `EditorSession`.

**Tech Stack:** C#/.NET 9, Hel editor UI (`EditorEntity`, `AssetBrowserView`, `ButtonComponent`, `TextBoxComponent`), HELE asset serialization, xUnit

---

## File Structure

### New Files

- `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
  Modal dialog for selecting one `.helen` file under the project `assets` folder.
- `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
  Small editor modal that exposes `Save`, `Don't Save`, and `Cancel`.
- `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
  Reads one `.helen` file from disk, deserializes it into `SceneAsset`, and materializes editor entities through `SceneLoadService`.
- `engine/helengine.editor/EditorSceneMutationService.cs`
  Static editor-side notification service used by scene-editing flows to signal that the active scene became dirty.
- `engine/helengine.editor.tests/OpenFileDialogTests.cs`
  Verifies the open dialog filters to `.helen` entries and raises the selected file path.
- `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`
  Verifies the unsaved-changes dialog raises the correct actions.
- `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
  Verifies `.helen` files are deserialized into editor entities and load failures surface clearly.
- `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
  Verifies `Open Map...`, `New Map`, guarded transitions, pending actions, and failure-preserving scene swaps.
- `engine/helengine.editor.tests/EditorSceneMutationServiceTests.cs`
  Verifies scene-mutation notifications can be raised and reset safely across tests.

### Modified Files

- `engine/helengine.editor/components/ui/EditorTitleBar.cs`
  Add `Open Map...` to the file menu and raise a new command event.
- `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
  Keep root materialization compatible with temporary disabled loading if a helper extraction becomes necessary during the scene-file load work.
- `engine/helengine.editor/EditorSession.cs`
  Own document state (`IsSceneDirty`, pending transition, open dialog, unsaved dialog, scene loader), handle guarded scene transitions, clear user scene entities, and react to mutation notifications.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  Mark the scene dirty when transform or entity-name edits are actually applied.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Mark the scene dirty when component-backed scalar, vector, material, or model properties are actually changed.
- `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
  Mark the scene dirty once a completed translation drag has changed the selected entity.
- `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
  Mark the scene dirty once a completed rotation drag has changed the selected entity.
- `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`
  Mark the scene dirty once a completed scale drag has changed the selected entity.
- `engine/helengine.editor.tests/EditorTitleBarTests.cs`
  Extend the file-menu coverage to assert the new `Open Map...` item ordering.
- `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
  Reuse save-routing helpers if the open/new guard tests need the same partial-session setup.

## Task 1: Add `Open Map...` To The File Menu

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`

- [ ] **Step 1: Write the failing file-menu test**

```csharp
/// <summary>
/// Ensures the File menu exposes `Open Map...` between `New Map` and `Save Map`.
/// </summary>
[Fact]
public void ToggleFileMenu_ShowsOpenMapBetweenNewAndSave() {
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

    InvokePrivate(titleBar, "ToggleFileMenu");

    ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
    List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(fileMenu, "ActiveItems");

    Assert.Collection(
        activeItems,
        item => Assert.Equal("New Map", item.Label),
        item => Assert.Equal("Open Map...", item.Label),
        item => Assert.Equal("Save Map", item.Label),
        item => Assert.Equal("Save Map As...", item.Label));
}
```

- [ ] **Step 2: Run the title-bar test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorTitleBarTests`

Expected: FAIL because `Open Map...` is not in `BuildFileMenuItems()` and no open-map event exists.

- [ ] **Step 3: Add the new event and menu item**

```csharp
/// <summary>
/// Raised when the user selects the Open Map file-menu command.
/// </summary>
public event Action OpenMapRequested;
```

```csharp
IReadOnlyList<ContextMenuItem> BuildFileMenuItems() {
    return new ContextMenuItem[] {
        new ContextMenuItem("New Map", RaiseNewMapRequested),
        new ContextMenuItem("Open Map...", RaiseOpenMapRequested),
        new ContextMenuItem("Save Map", RaiseSaveMapRequested),
        new ContextMenuItem("Save Map As...", RaiseSaveMapAsRequested)
    };
}
```

```csharp
/// <summary>
/// Raises the Open Map command event.
/// </summary>
void RaiseOpenMapRequested() {
    if (OpenMapRequested != null) {
        OpenMapRequested();
    }
}
```

- [ ] **Step 4: Run the title-bar test to verify it passes**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorTitleBarTests`

Expected: PASS with the updated file-menu ordering.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarTests.cs
git commit -m "feat: add open map command to title bar"
```

## Task 2: Add The Open-Scene And Unsaved-Changes Dialogs

**Files:**
- Create: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Create: `engine/helengine.editor/components/ui/UnsavedChangesDialog.cs`
- Create: `engine/helengine.editor.tests/OpenFileDialogTests.cs`
- Create: `engine/helengine.editor.tests/UnsavedChangesDialogTests.cs`

- [ ] **Step 1: Write the failing open-dialog test**

```csharp
/// <summary>
/// Ensures the open dialog only raises `.helen` files.
/// </summary>
[Fact]
public void HandleOpenClicked_WhenSceneFileIsSelected_RaisesScenePath() {
    string projectRoot = CreateProjectRootWithScenes();
    OpenFileDialog dialog = new OpenFileDialog(CreateFont(), projectRoot);
    string raisedPath = string.Empty;
    dialog.OpenRequested += path => raisedPath = path;
    dialog.Show("Scenes");
    dialog.UpdateLayout(1280, 720);

    SetSelectedEntry(dialog, "Level01.helen");
    InvokePrivate(dialog, "HandleOpenClicked");

    Assert.EndsWith(Path.Combine("assets", "Scenes", "Level01.helen"), raisedPath, StringComparison.OrdinalIgnoreCase);
}
```

```csharp
/// <summary>
/// Ensures the open dialog hides non-scene files from selection.
/// </summary>
[Fact]
public void RefreshEntries_WhenOpenDialogIsVisible_FiltersToHelenFiles() {
    string projectRoot = CreateProjectRootWithMixedFiles();
    OpenFileDialog dialog = new OpenFileDialog(CreateFont(), projectRoot);
    dialog.Show("Scenes");
    dialog.UpdateLayout(1280, 720);

    AssetBrowserView browserView = GetPrivateField<AssetBrowserView>(dialog, "BrowserView");
    List<AssetBrowserEntry> entries = GetPrivateField<List<AssetBrowserEntry>>(browserView, "Entries");

    Assert.Contains(entries, entry => string.Equals(entry.Extension, ".helen", StringComparison.OrdinalIgnoreCase));
    Assert.DoesNotContain(entries, entry => string.Equals(entry.Extension, ".png", StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Write the failing unsaved-dialog test**

```csharp
/// <summary>
/// Ensures the unsaved-changes dialog raises the Save action.
/// </summary>
[Fact]
public void HandleSaveClicked_RaisesSaveRequested() {
    UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
    bool raised = false;
    dialog.SaveRequested += () => raised = true;
    dialog.Show();
    dialog.UpdateLayout(1280, 720);

    InvokePrivate(dialog, "HandleSaveClicked");

    Assert.True(raised);
}
```

```csharp
/// <summary>
/// Ensures the unsaved-changes dialog raises the Don't Save action.
/// </summary>
[Fact]
public void HandleDontSaveClicked_RaisesDontSaveRequested() {
    UnsavedChangesDialog dialog = new UnsavedChangesDialog(CreateFont());
    bool raised = false;
    dialog.DontSaveRequested += () => raised = true;
    dialog.Show();
    dialog.UpdateLayout(1280, 720);

    InvokePrivate(dialog, "HandleDontSaveClicked");

    Assert.True(raised);
}
```

- [ ] **Step 3: Run the new dialog tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "OpenFileDialogTests|UnsavedChangesDialogTests"`

Expected: FAIL because neither dialog class exists yet.

- [ ] **Step 4: Implement `OpenFileDialog`**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Floating modal dialog used to choose one `.helen` scene file under the project assets folder.
    /// </summary>
    public class OpenFileDialog : EditorEntity {
        readonly FontAsset Font;
        readonly SceneSavePathResolver PathResolver;
        readonly EditorEntity PanelRoot;
        readonly RoundedRectComponent PanelBackground;
        readonly EditorEntity HeaderRoot;
        readonly SpriteComponent HeaderBackground;
        readonly InteractableComponent HeaderInteractable;
        readonly EditorEntity HeaderHost;
        readonly TextComponent HeaderText;
        readonly AssetBrowserView BrowserView;
        readonly EditorEntity StatusHost;
        readonly TextComponent StatusText;
        readonly EditorEntity CancelButtonHost;
        readonly ButtonComponent CancelButton;
        readonly EditorEntity OpenButtonHost;
        readonly ButtonComponent OpenButton;
        readonly byte PanelOrder;
        readonly byte TextOrder;
        int2 PanelSize;
        int2 PanelPosition;
        int2 HostSize;
        bool IsUserPositioned;
        bool IsDragging;
        bool IsInitialized;
        AssetBrowserEntry SelectedEntry;

        public event Action<string> OpenRequested;

        public OpenFileDialog(FontAsset font, string projectPath) {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            if (string.IsNullOrWhiteSpace(projectPath)) {
                throw new ArgumentException("Project path must be provided.", nameof(projectPath));
            }

            PathResolver = new SceneSavePathResolver(projectPath);
            BrowserView = new AssetBrowserView(font, projectPath, LayerMask, PanelOrder, PanelOrder, PanelOrder, TextOrder, false);
            BrowserView.SetExtensionFilter(".helen");
            BrowserView.AssetActivated += HandleAssetActivated;
            BrowserView.SelectionCleared += HandleSelectionCleared;
            CancelButton = new ButtonComponent("Cancel", new int2(88, 22), font, Hide, 0f);
            OpenButton = new ButtonComponent("Open", new int2(88, 22), font, HandleOpenClicked, 0f);
            StatusText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.StateWarning,
                RenderOrder2D = TextOrder
            };
            Enabled = false;
            IsInitialized = true;
        }

        public bool IsVisible => Enabled;

        public void Show(string initialRelativeDirectory) {
            IsUserPositioned = false;
            IsDragging = false;
            SelectedEntry = null;
            StatusText.Text = string.Empty;
            Enabled = true;
            if (!BrowserView.TryNavigateTo(initialRelativeDirectory)) {
                BrowserView.TryNavigateTo(SceneSavePathResolver.DefaultSceneDirectory);
            }
            BrowserView.RefreshEntries();
        }

        public void Hide() {
            IsUserPositioned = false;
            IsDragging = false;
            SelectedEntry = null;
            StatusText.Text = string.Empty;
            EditorInputCaptureService.ClearBlocker(this);
            Enabled = false;
        }

        void HandleOpenClicked() {
            if (SelectedEntry == null || SelectedEntry.IsDirectory) {
                StatusText.Text = "Select a scene file to open.";
                return;
            }

            if (!string.Equals(SelectedEntry.Extension, ".helen", StringComparison.OrdinalIgnoreCase)) {
                StatusText.Text = "Selected file is not a scene.";
                return;
            }

            OpenRequested?.Invoke(SelectedEntry.FullPath);
        }

        void HandleAssetActivated(AssetBrowserEntry entry) {
            if (entry == null || entry.IsDirectory) {
                return;
            }

            SelectedEntry = entry;
            StatusText.Text = string.Empty;
        }

        void HandleSelectionCleared() {
            SelectedEntry = null;
        }
    }
}
```

- [ ] **Step 5: Implement `UnsavedChangesDialog`**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Floating confirmation dialog shown before discarding a dirty scene.
    /// </summary>
    public class UnsavedChangesDialog : EditorEntity {
        readonly FontAsset Font;
        readonly EditorEntity PanelRoot;
        readonly RoundedRectComponent PanelBackground;
        readonly EditorEntity MessageHost;
        readonly TextComponent MessageText;
        readonly EditorEntity SaveButtonHost;
        readonly ButtonComponent SaveButton;
        readonly EditorEntity DontSaveButtonHost;
        readonly ButtonComponent DontSaveButton;
        readonly EditorEntity CancelButtonHost;
        readonly ButtonComponent CancelButton;
        readonly byte PanelOrder;
        readonly byte TextOrder;
        int2 PanelSize;
        int2 PanelPosition;
        int2 HostSize;
        bool IsInitialized;

        public event Action SaveRequested;
        public event Action DontSaveRequested;
        public event Action CancelRequested;

        public UnsavedChangesDialog(FontAsset font) {
            Font = font ?? throw new ArgumentNullException(nameof(font));
            MessageText = new TextComponent {
                Font = font,
                Text = "Do you want to save changes to the current map?",
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = TextOrder
            };
            SaveButton = new ButtonComponent("Save", new int2(88, 22), font, HandleSaveClicked, 0f);
            DontSaveButton = new ButtonComponent("Don't Save", new int2(104, 22), font, HandleDontSaveClicked, 0f);
            CancelButton = new ButtonComponent("Cancel", new int2(88, 22), font, HandleCancelClicked, 0f);
            Enabled = false;
            IsInitialized = true;
        }

        public bool IsVisible => Enabled;

        public void Show() {
            Enabled = true;
        }

        public void Hide() {
            EditorInputCaptureService.ClearBlocker(this);
            Enabled = false;
        }

        void HandleSaveClicked() {
            SaveRequested?.Invoke();
        }

        void HandleDontSaveClicked() {
            DontSaveRequested?.Invoke();
        }

        void HandleCancelClicked() {
            CancelRequested?.Invoke();
        }
    }
}
```

- [ ] **Step 6: Run the dialog tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "OpenFileDialogTests|UnsavedChangesDialogTests"`

Expected: PASS with the new dialogs.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor/components/ui/UnsavedChangesDialog.cs engine/helengine.editor.tests/OpenFileDialogTests.cs engine/helengine.editor.tests/UnsavedChangesDialogTests.cs
git commit -m "feat: add scene open and unsaved change dialogs"
```

## Task 3: Add Scene Loading From `.helen`

**Files:**
- Create: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- Create: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`

- [ ] **Step 1: Write the failing scene-load test**

```csharp
/// <summary>
/// Ensures a saved `.helen` file can be materialized back into editor entities.
/// </summary>
[Fact]
public void Load_WhenSceneFileExists_ReturnsRootEntities() {
    string projectRoot = CreateProjectRoot();
    string scenePath = SaveScene(projectRoot, entity => entity.Name = "Loaded Cube");
    SceneFileLoadService loadService = CreateLoadService(projectRoot);

    IReadOnlyList<EditorEntity> loaded = loadService.Load(scenePath);

    Assert.Single(loaded);
    Assert.Equal("Loaded Cube", loaded[0].Name);
}
```

```csharp
/// <summary>
/// Ensures invalid scene files fail with a clear exception.
/// </summary>
[Fact]
public void Load_WhenSceneFileIsInvalid_ThrowsInvalidOperationException() {
    string projectRoot = CreateProjectRoot();
    string scenePath = Path.Combine(projectRoot, "assets", "Scenes", "Broken.helen");
    File.WriteAllText(scenePath, "not-a-helen");
    SceneFileLoadService loadService = CreateLoadService(projectRoot);

    InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => loadService.Load(scenePath));

    Assert.Contains("Scene load failed", exception.Message);
}
```

- [ ] **Step 2: Run the scene-load tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter SceneFileLoadServiceTests`

Expected: FAIL because `SceneFileLoadService` does not exist.

- [ ] **Step 3: Implement `SceneFileLoadService`**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Reads one `.helen` file from disk and materializes editor entities from it.
    /// </summary>
    public class SceneFileLoadService {
        readonly string ProjectRootPath;
        readonly SceneLoadService SceneLoadService;

        /// <summary>
        /// Initializes a new scene-file load service.
        /// </summary>
        /// <param name="projectRootPath">Project root that owns the assets folder.</param>
        /// <param name="persistenceRegistry">Registry used to deserialize persisted components.</param>
        /// <param name="referenceResolver">Resolver used to rebuild runtime-backed assets.</param>
        public SceneFileLoadService(
            string projectRootPath,
            ComponentPersistenceRegistry persistenceRegistry,
            ISceneAssetReferenceResolver referenceResolver) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
            SceneLoadService = new SceneLoadService(
                persistenceRegistry ?? throw new ArgumentNullException(nameof(persistenceRegistry)),
                referenceResolver ?? throw new ArgumentNullException(nameof(referenceResolver)));
        }

        /// <summary>
        /// Loads one `.helen` scene file from disk.
        /// </summary>
        /// <param name="fullPath">Absolute path to the scene file.</param>
        /// <returns>Loaded root editor entities.</returns>
        public IReadOnlyList<EditorEntity> Load(string fullPath) {
            if (string.IsNullOrWhiteSpace(fullPath)) {
                throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
            }

            string normalizedPath = Path.GetFullPath(fullPath);
            HashSet<Entity> existingEntities = new HashSet<Entity>(Core.Instance.ObjectManager.Entities);
            try {
                using FileStream stream = new FileStream(normalizedPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                SceneAsset sceneAsset = AssetSerializer.Deserialize<SceneAsset>(stream);
                IReadOnlyList<EditorEntity> loadedRoots = SceneLoadService.Load(sceneAsset);
                SetRootsEnabled(loadedRoots, false);
                return loadedRoots;
            } catch (Exception ex) {
                CleanupFailedLoad(existingEntities);
                throw new InvalidOperationException($"Scene load failed: {ex.Message}", ex);
            }
        }

        void SetRootsEnabled(IReadOnlyList<EditorEntity> roots, bool enabled) {
            if (roots == null) {
                throw new ArgumentNullException(nameof(roots));
            }

            for (int i = 0; i < roots.Count; i++) {
                if (roots[i] == null) {
                    throw new InvalidOperationException("Loaded scene contained a null root entity.");
                }

                roots[i].Enabled = enabled;
            }
        }

        void CleanupFailedLoad(HashSet<Entity> existingEntities) {
            List<Entity> liveEntities = new List<Entity>(Core.Instance.ObjectManager.Entities);
            for (int i = 0; i < liveEntities.Count; i++) {
                Entity entity = liveEntities[i];
                if (existingEntities.Contains(entity)) {
                    continue;
                }
                if (entity is not EditorEntity editorEntity) {
                    continue;
                }
                if (editorEntity.InternalEntity) {
                    continue;
                }

                editorEntity.Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(editorEntity);
            }
        }
    }
}
```

- [ ] **Step 4: Run the scene-load tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter SceneFileLoadServiceTests`

Expected: PASS with one valid-load test and one failure-path test.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs
git commit -m "feat: add scene file loading service"
```

## Task 4: Add Dirty-State Mutation Notifications

**Files:**
- Create: `engine/helengine.editor/EditorSceneMutationService.cs`
- Create: `engine/helengine.editor.tests/EditorSceneMutationServiceTests.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`

- [ ] **Step 1: Write the failing mutation-service test**

```csharp
/// <summary>
/// Ensures scene-mutation notifications raise the shared event.
/// </summary>
[Fact]
public void MarkSceneMutated_RaisesSceneMutated() {
    bool raised = false;
    EditorSceneMutationService.SceneMutated += HandleMutated;

    try {
        EditorSceneMutationService.MarkSceneMutated();
        Assert.True(raised);
    } finally {
        EditorSceneMutationService.SceneMutated -= HandleMutated;
        EditorSceneMutationService.Reset();
    }

    void HandleMutated() {
        raised = true;
    }
}
```

- [ ] **Step 2: Run the mutation-service test to verify it fails**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSceneMutationServiceTests`

Expected: FAIL because `EditorSceneMutationService` does not exist.

- [ ] **Step 3: Implement the mutation service**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Broadcasts scene-edit notifications to the active editor session.
    /// </summary>
    public static class EditorSceneMutationService {
        /// <summary>
        /// Raised when editor tools mutate the current scene.
        /// </summary>
        public static event Action SceneMutated;

        /// <summary>
        /// Raises one scene-mutated notification.
        /// </summary>
        public static void MarkSceneMutated() {
            if (SceneMutated != null) {
                SceneMutated();
            }
        }

        /// <summary>
        /// Clears all subscribers between tests or editor shutdown.
        /// </summary>
        public static void Reset() {
            SceneMutated = null;
        }
    }
}
```

- [ ] **Step 4: Hook the mutation service into actual scene-edit paths**

```csharp
// EditorSession.cs
void CreateAndSelectSceneEntity(Func<EditorEntity> createEntity) {
    Entity previousSelection = EditorSelectionService.SelectedEntity;

    try {
        EditorEntity entity = createEntity();
        sceneHierarchyPanel.RefreshHierarchy();
        EditorSelectionService.SetSelectedEntity(entity);
        EditorSceneMutationService.MarkSceneMutated();
    } catch (Exception ex) {
        Logger.WriteError($"Scene entity creation failed: {ex.Message}");
        if (previousSelection == null) {
            EditorSelectionService.ClearSelection();
        } else {
            EditorSelectionService.SetSelectedEntity(previousSelection);
        }
    }
}
```

```csharp
// PropertiesPanel.cs
if (nameChanged) {
    editorEntity.Name = text;
    NameTextCache = text;
    EditorSceneMutationService.MarkSceneMutated();
}

if (positionChanged) {
    SelectedEntity.Position = new float3((float)x, (float)y, (float)z);
    SetVectorFields(PositionFields, PositionTextCache, x, y, z);
    EditorSceneMutationService.MarkSceneMutated();
}
```

```csharp
// ComponentPropertiesView.cs
row.Property.SetValue(row.TargetComponent, value);
SetVectorFields(row, x, y, z);
EditorSceneMutationService.MarkSceneMutated();
```

```csharp
// TransformTranslationGizmoDragComponent.cs
float3 DragStartEntityPosition;
bool DragChanged;

void UpdateActiveDrag(InputManager input) {
    float3 targetPosition = DragStartEntityPosition + axisOffset;
    DraggedEntity.Position = targetPosition;
    DragChanged = DragChanged || DraggedEntity.Position != DragStartEntityPosition;
}

void EndDrag() {
    if (DragChanged) {
        EditorSceneMutationService.MarkSceneMutated();
    }
    DragChanged = false;
    EditorGizmoDragService.EndDrag(SceneCamera);
    IsDragging = false;
    DraggedEntity = null;
    DragHandleEntity = null;
    DragConstraintType = TransformGizmoHandleConstraintType.Axis;
    DragPrimaryDirection = float3.Zero;
    DragSecondaryDirection = float3.Zero;
    DragPlaneNormal = float3.Zero;
    DragStartEntityPosition = float3.Zero;
    DragStartAxisParameter = 0.0;
    DragStartPlanePoint = float3.Zero;
}
```

```csharp
// TransformRotationGizmoDragComponent.cs and TransformScaleGizmoDragComponent.cs
float4 DragStartEntityOrientation;
bool DragChanged;

void EndDrag() {
    if (DragChanged) {
        EditorSceneMutationService.MarkSceneMutated();
    }
    DragChanged = false;
    EditorGizmoDragService.EndDrag(SceneCamera);
    IsDragging = false;
    DraggedEntity = null;
    DragHandleEntity = null;
    DragRotationAxis = float3.Zero;
    DragRotationCenter = float3.Zero;
    DragStartEntityOrientation = float4.Identity;
    DragAccumulatedAngle = 0.0;
    DragPreviousVector = float3.Zero;
}

float3 DragStartEntityScale;
bool DragChanged;

void EndDrag() {
    if (DragChanged) {
        EditorSceneMutationService.MarkSceneMutated();
    }
    DragChanged = false;
    EditorGizmoDragService.EndDrag(SceneCamera);
    IsDragging = false;
    DraggedEntity = null;
    DragHandleEntity = null;
    DragConstraintType = TransformGizmoHandleConstraintType.Axis;
    DragPrimaryDirection = float3.Zero;
    DragSecondaryDirection = float3.Zero;
    DragPlaneNormal = float3.Zero;
    DragStartEntityScale = float3.Zero;
    DragStartEntityPosition = float3.Zero;
    DragStartAxisParameter = 0.0;
    DragStartPlanePoint = float3.Zero;
}
```

- [ ] **Step 5: Run focused tests for the mutation service and affected editor tests**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSceneMutationServiceTests|EditorSessionAddMenuTests|TransformTranslationGizmoFollowComponentTests"`

Expected: PASS with the new service and no regressions in add-menu or gizmo coverage.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/EditorSceneMutationService.cs engine/helengine.editor.tests/EditorSceneMutationServiceTests.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs
git commit -m "feat: track dirty scene mutations"
```

## Task 5: Wire Guarded `New Map` And `Open Map...` In `EditorSession`

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`

- [ ] **Step 1: Write the failing session tests**

```csharp
/// <summary>
/// Ensures `Open Map...` shows the open dialog immediately when the scene is clean.
/// </summary>
[Fact]
public void HandleOpenMapRequested_WhenSceneIsClean_ShowsOpenFileDialog() {
    EditorSession session = CreateSessionForSceneOpen();
    SetPrivateField(session, "IsSceneDirty", false);

    InvokePrivate(session, "HandleOpenMapRequested");

    OpenFileDialog dialog = GetPrivateField<OpenFileDialog>(session, "openFileDialog");
    Assert.True(dialog.IsVisible);
}
```

```csharp
/// <summary>
/// Ensures `New Map` shows the unsaved-changes dialog when the scene is dirty.
/// </summary>
[Fact]
public void HandleNewMapRequested_WhenSceneIsDirty_ShowsUnsavedChangesDialog() {
    EditorSession session = CreateSessionForSceneOpen();
    SetPrivateField(session, "IsSceneDirty", true);

    InvokePrivate(session, "HandleNewMapRequested");

    UnsavedChangesDialog dialog = GetPrivateField<UnsavedChangesDialog>(session, "unsavedChangesDialog");
    Assert.True(dialog.IsVisible);
}
```

```csharp
/// <summary>
/// Ensures failed scene opens keep the current live scene intact.
/// </summary>
[Fact]
public void HandleSceneOpenRequested_WhenLoadFails_PreservesCurrentScene() {
    EditorSession session = CreateSessionForSceneOpen();
    EditorEntity existing = new EditorEntity { Name = "Existing" };
    string missingPath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Missing.helen");

    InvokePrivate(session, "HandleSceneOpenRequested", missingPath);

    Assert.Contains(Core.Instance.ObjectManager.Entities, entity => ReferenceEquals(entity, existing));
}
```

```csharp
/// <summary>
/// Ensures choosing `Save` with no current scene path opens the save dialog and preserves the pending transition.
/// </summary>
[Fact]
public void HandleUnsavedSaveRequested_WhenSceneHasNoPath_ShowsSaveDialogAndKeepsPendingTransition() {
    EditorSession session = CreateSessionForSceneOpen();
    SetPrivateField(session, "IsSceneDirty", true);
    InvokePrivate(session, "HandleNewMapRequested");

    InvokePrivate(session, "HandleUnsavedChangesSaveRequested");

    SaveFileDialog saveDialog = GetPrivateField<SaveFileDialog>(session, "saveFileDialog");
    object pendingTransition = GetPrivateField<object>(session, "PendingSceneTransition");
    Assert.True(saveDialog.IsVisible);
    Assert.NotNull(pendingTransition);
}
```

- [ ] **Step 2: Run the new session tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSessionSceneOpenTests|EditorSessionSceneSaveTests"`

Expected: FAIL because the open dialog, unsaved dialog, dirty state, and guarded transition handlers do not exist.

- [ ] **Step 3: Add document-state fields and dialog wiring to `EditorSession`**

```csharp
enum SceneTransitionKind {
    None,
    NewMap,
    OpenMap
}
```

```csharp
readonly SceneFileLoadService SceneFileLoadService;
readonly OpenFileDialog openFileDialog;
readonly UnsavedChangesDialog unsavedChangesDialog;
bool IsSceneDirty;
SceneTransitionKind PendingSceneTransition;
string PendingOpenScenePath;
```

```csharp
titleBar.NewMapRequested += HandleNewMapRequested;
titleBar.OpenMapRequested += HandleOpenMapRequested;
openFileDialog.OpenRequested += HandleSceneOpenRequested;
unsavedChangesDialog.SaveRequested += HandleUnsavedChangesSaveRequested;
unsavedChangesDialog.DontSaveRequested += HandleUnsavedChangesDontSaveRequested;
unsavedChangesDialog.CancelRequested += HandleUnsavedChangesCancelRequested;
EditorSceneMutationService.SceneMutated += HandleSceneMutated;
```

```csharp
titleBar.NewMapRequested -= HandleNewMapRequested;
titleBar.OpenMapRequested -= HandleOpenMapRequested;
openFileDialog.OpenRequested -= HandleSceneOpenRequested;
unsavedChangesDialog.SaveRequested -= HandleUnsavedChangesSaveRequested;
unsavedChangesDialog.DontSaveRequested -= HandleUnsavedChangesDontSaveRequested;
unsavedChangesDialog.CancelRequested -= HandleUnsavedChangesCancelRequested;
EditorSceneMutationService.SceneMutated -= HandleSceneMutated;
```

- [ ] **Step 4: Implement guarded transitions and scene swapping**

```csharp
void HandleNewMapRequested() {
    RequestSceneTransition(SceneTransitionKind.NewMap, string.Empty);
}

void HandleOpenMapRequested() {
    RequestSceneTransition(SceneTransitionKind.OpenMap, string.Empty);
}

void RequestSceneTransition(SceneTransitionKind transitionKind, string openPath) {
    PendingSceneTransition = transitionKind;
    PendingOpenScenePath = openPath ?? string.Empty;

    if (!IsSceneDirty) {
        ContinuePendingSceneTransition();
        return;
    }

    unsavedChangesDialog.Show();
}
```

```csharp
void ContinuePendingSceneTransition() {
    SceneTransitionKind pending = PendingSceneTransition;
    string pendingPath = PendingOpenScenePath;

    PendingSceneTransition = SceneTransitionKind.None;
    PendingOpenScenePath = string.Empty;
    unsavedChangesDialog.Hide();

    if (pending == SceneTransitionKind.NewMap) {
        ResetToNewScene();
        return;
    }

    if (pending == SceneTransitionKind.OpenMap) {
        if (string.IsNullOrWhiteSpace(pendingPath)) {
            openFileDialog.Show(SceneSavePathResolver.DefaultSceneDirectory);
            return;
        }

        LoadSceneIntoSession(pendingPath);
    }
}
```

```csharp
void HandleSceneOpenRequested(string fullPath) {
    if (string.IsNullOrWhiteSpace(fullPath)) {
        throw new ArgumentException("Scene path must be provided.", nameof(fullPath));
    }

    PendingSceneTransition = SceneTransitionKind.OpenMap;
    PendingOpenScenePath = Path.GetFullPath(fullPath);
    ContinuePendingSceneTransition();
}

void LoadSceneIntoSession(string fullPath) {
    try {
        IReadOnlyList<EditorEntity> loadedRoots = SceneFileLoadService.Load(fullPath);
        ClearUserSceneEntities();
        AttachLoadedRoots(loadedRoots);
        CurrentScenePath = Path.GetFullPath(fullPath);
        MarkSceneClean();
        EditorSelectionService.ClearSelection();
        sceneHierarchyPanel.RefreshHierarchy();
        assetBrowserPanel.RefreshEntries();
        openFileDialog.Hide();
    } catch (Exception ex) {
        Logger.WriteError($"Scene open failed: {ex.Message}");
        openFileDialog.ShowError(ex.Message);
    }
}
```

```csharp
void ResetToNewScene() {
    ClearUserSceneEntities();
    CurrentScenePath = string.Empty;
    MarkSceneClean();
    EditorSelectionService.ClearSelection();
    sceneHierarchyPanel.RefreshHierarchy();
    openFileDialog.Hide();
}
```

- [ ] **Step 5: Implement save-then-continue and clean/dirty helpers**

```csharp
void HandleUnsavedChangesSaveRequested() {
    if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
        ShowSceneSaveDialog();
        return;
    }

    HandleSceneSaveRequested(CurrentScenePath);
    if (!IsSceneDirty) {
        ContinuePendingSceneTransition();
    }
}

void HandleUnsavedChangesDontSaveRequested() {
    ContinuePendingSceneTransition();
}

void HandleUnsavedChangesCancelRequested() {
    PendingSceneTransition = SceneTransitionKind.None;
    PendingOpenScenePath = string.Empty;
    unsavedChangesDialog.Hide();
}

void HandleSceneMutated() {
    IsSceneDirty = true;
}

void MarkSceneClean() {
    IsSceneDirty = false;
}
```

```csharp
void HandleSceneSaveRequested(string fullPath) {
    try {
        SceneSaveService.Save(fullPath);
        CurrentScenePath = Path.GetFullPath(fullPath);
        MarkSceneClean();
        assetBrowserPanel.RefreshEntries();
        saveFileDialog.Hide();
        if (PendingSceneTransition != SceneTransitionKind.None) {
            ContinuePendingSceneTransition();
        }
    } catch (Exception ex) {
        Logger.WriteError($"Scene save failed: {ex.Message}");
        saveFileDialog.ShowError(ex.Message);
    }
}
```

- [ ] **Step 6: Implement clearing only user-authored scene entities**

```csharp
void ClearUserSceneEntities() {
    List<Entity> entities = new List<Entity>(Core.Instance.ObjectManager.Entities);
    for (int i = 0; i < entities.Count; i++) {
        if (entities[i] is not EditorEntity editorEntity) {
            continue;
        }
        if (editorEntity.Parent != null) {
            continue;
        }
        if (editorEntity.InternalEntity) {
            continue;
        }
        if (editorEntity.LayerMask != EditorLayerMasks.SceneObjects) {
            continue;
        }

        editorEntity.Enabled = false;
        Core.Instance.ObjectManager.RemoveEntity(editorEntity);
    }
}

void AttachLoadedRoots(IReadOnlyList<EditorEntity> roots) {
    if (roots == null) {
        throw new ArgumentNullException(nameof(roots));
    }

    for (int i = 0; i < roots.Count; i++) {
        EditorEntity root = roots[i];
        if (root == null) {
            throw new InvalidOperationException("Loaded scene contained a null root entity.");
        }

        root.Enabled = true;
    }
}
```

- [ ] **Step 7: Run the session tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorSessionSceneOpenTests|EditorSessionSceneSaveTests"`

Expected: PASS with clean-open, dirty-guard, save-continue, cancel, and failure-preserving load coverage.

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs
git commit -m "feat: add guarded open and new map workflow"
```

## Task 6: Run Focused And Full Verification

**Files:**
- No code changes expected

- [ ] **Step 1: Run focused regression tests**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorTitleBarTests|OpenFileDialogTests|UnsavedChangesDialogTests|SceneFileLoadServiceTests|EditorSessionSceneSaveTests|EditorSessionSceneOpenTests|EditorSceneMutationServiceTests"`

Expected: PASS for all new and directly affected tests.

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS for the full editor suite, with only pre-existing warnings.

- [ ] **Step 3: Review spec coverage before closing**

Checklist:

- `Open Map...` exists in the `File` menu.
- `OpenFileDialog` is rooted under `assets` and filters to `.helen`.
- `UnsavedChangesDialog` exposes `Save`, `Don't Save`, and `Cancel`.
- `New Map` and `Open Map...` both route through the same guard.
- Dirty state is tracked explicitly and updated by real scene-editing flows.
- Scene load preserves the current live scene until the new scene is materialized successfully.
- Successful save/load/new transitions mark the scene clean.
- Failed open keeps the previous scene and document state.

- [ ] **Step 4: Commit the final verified state**

```bash
git status --short
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor/components/ui/UnsavedChangesDialog.cs engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor/EditorSceneMutationService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/managers/gizmo/TransformTranslationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformRotationGizmoDragComponent.cs engine/helengine.editor/managers/gizmo/TransformScaleGizmoDragComponent.cs engine/helengine.editor.tests/EditorTitleBarTests.cs engine/helengine.editor.tests/OpenFileDialogTests.cs engine/helengine.editor.tests/UnsavedChangesDialogTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs engine/helengine.editor.tests/EditorSceneMutationServiceTests.cs
git commit -m "feat: add open map workflow with unsaved change guard"
```

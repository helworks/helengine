# Add Menu Primitives Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an `Add` menu beside `File` in the editor title bar that creates `Empty`, `Cube`, and `Plane` scene entities at the origin and selects them immediately.

**Architecture:** Extend `EditorTitleBar` with a second top-level `ContextMenu`, then route its commands through a dedicated `EditorSceneCreationService` that owns entity defaults and generated-model save metadata. `EditorSession` only wires title-bar events to the service, refreshes the hierarchy, and updates selection, while primitive creation stays compatible with the existing `.helen` save flow.

**Tech Stack:** C#/.NET 9, Hel engine editor UI (`EditorEntity`, `ContextMenu`, `ButtonComponent`), generated asset pipeline, scene save metadata, xUnit

---

## File Structure

### New Files

- `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
  Creates user-authored scene entities for `Add > Empty`, `Add > Cube`, and `Add > Plane`, including generated-model save metadata.
- `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
  Verifies title-bar layout and menu interactions for the new `Add` menu.
- `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
  Verifies entity defaults, generated model references, and save compatibility for created primitives.
- `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`
  Verifies `EditorSession` wires `Add` commands to scene creation, hierarchy refresh, and selection.

### Modified Files

- `engine/helengine.editor/components/ui/EditorTitleBar.cs`
  Add the `Add` button, `Add` context menu, new events, shared menu-hiding behavior, and updated layout calculations.
- `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
  Expose stable constants for the provider id and primitive relative paths so creation code and tests do not duplicate string literals.
- `engine/helengine.editor/EditorSession.cs`
  Own the `EditorSceneCreationService`, subscribe to `Add` menu events, create entities, refresh the hierarchy, and select the new entity.
- `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
  Reuse scene-save routing coverage if a small helper extraction is needed for add-menu session tests.

## Task 1: Add The `Add` Title-Bar Menu

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Create: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`

- [ ] **Step 1: Write the failing title-bar tests**

```csharp
using System.Reflection;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor title bar exposes the `Add` menu beside `File`.
    /// </summary>
    public class EditorTitleBarAddMenuTests {
        /// <summary>
        /// Ensures the `Add` button is laid out immediately to the right of `File`.
        /// </summary>
        [Fact]
        public void UpdateLayout_PlacesAddButtonImmediatelyToTheRightOfFile() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            EditorEntity fileButton = GetPrivateField<EditorEntity>(titleBar, "FileMenuButtonEntity");
            EditorEntity addButton = GetPrivateField<EditorEntity>(titleBar, "AddMenuButtonEntity");
            int fileButtonWidth = GetPrivateField<int>(titleBar, "FileMenuButtonWidth");

            Assert.Equal(fileButton.Position.X + fileButtonWidth + 6f, addButton.Position.X);
        }

        /// <summary>
        /// Ensures the `Add` menu shows `Empty`, `Cube`, and `Plane` and hides `File` when opened.
        /// </summary>
        [Fact]
        public void ToggleAddMenu_ShowsExpectedItemsAndHidesFileMenu() {
            EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

            InvokePrivate(titleBar, "ToggleFileMenu");
            InvokePrivate(titleBar, "ToggleAddMenu");

            ContextMenu fileMenu = GetPrivateField<ContextMenu>(titleBar, "FileMenu");
            ContextMenu addMenu = GetPrivateField<ContextMenu>(titleBar, "AddMenu");
            List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(addMenu, "ActiveItems");

            Assert.False(fileMenu.IsVisible);
            Assert.True(addMenu.IsVisible);
            Assert.Collection(
                activeItems,
                item => Assert.Equal("Empty", item.Label),
                item => Assert.Equal("Cube", item.Label),
                item => Assert.Equal("Plane", item.Label));
        }

        /// <summary>
        /// Reads one non-public instance field and casts it to the requested type.
        /// </summary>
        T GetPrivateField<T>(object target, string fieldName) {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            return Assert.IsType<T>(field.GetValue(target));
        }

        /// <summary>
        /// Invokes one non-public instance method.
        /// </summary>
        void InvokePrivate(object target, string methodName) {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            method.Invoke(target, Array.Empty<object>());
        }
    }
}
```

- [ ] **Step 2: Run the title-bar tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorTitleBarAddMenuTests`

Expected: build failure because `EditorTitleBar` does not have `AddMenuButtonEntity`, `AddMenu`, `ToggleAddMenu()`, or `Add` menu items yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
/// <summary>
/// Entity that hosts the Add menu trigger button.
/// </summary>
readonly EditorEntity AddMenuButtonEntity;
/// <summary>
/// Width reserved for the Add menu trigger button.
/// </summary>
readonly int AddMenuButtonWidth;
/// <summary>
/// Context menu shown when the Add button is activated.
/// </summary>
readonly ContextMenu AddMenu;
/// <summary>
/// Items displayed by the Add context menu.
/// </summary>
readonly IReadOnlyList<ContextMenuItem> AddMenuItems;
```

```csharp
/// <summary>
/// Raised when the user selects the Add Empty command.
/// </summary>
public event Action AddEmptyRequested;
/// <summary>
/// Raised when the user selects the Add Cube command.
/// </summary>
public event Action AddCubeRequested;
/// <summary>
/// Raised when the user selects the Add Plane command.
/// </summary>
public event Action AddPlaneRequested;
```

```csharp
FileMenuButtonEntity = CreateTitleBarButton("File", ToggleFileMenu, out int fileMenuButtonWidth);
FileMenuButtonWidth = fileMenuButtonWidth;
AddMenuButtonEntity = CreateTitleBarButton("Add", ToggleAddMenu, out int addMenuButtonWidth);
AddMenuButtonWidth = addMenuButtonWidth;

FileMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
RootEntity.AddChild(FileMenu.Entity);
FileMenuItems = BuildFileMenuItems();

AddMenu = new ContextMenu(Font, TitleBarLayerMask, menuBackgroundOrder, menuTextOrder);
RootEntity.AddChild(AddMenu.Entity);
AddMenuItems = BuildAddMenuItems();
```

```csharp
IReadOnlyList<ContextMenuItem> BuildAddMenuItems() {
    return new ContextMenuItem[] {
        new ContextMenuItem("Empty", RaiseAddEmptyRequested),
        new ContextMenuItem("Cube", RaiseAddCubeRequested),
        new ContextMenuItem("Plane", RaiseAddPlaneRequested)
    };
}
```

```csharp
public void UpdateLayout(int windowWidth, int windowHeight) {
    int width = Math.Max(1, windowWidth);
    int height = Math.Max(HeightPixels, windowHeight);
    HostSize = new int2(width, height);

    float fileButtonX = EdgePadding;
    FileMenuButtonEntity.Position = new float3(fileButtonX, ButtonTop, 0f);

    float addButtonX = fileButtonX + FileMenuButtonWidth + ButtonSpacing;
    AddMenuButtonEntity.Position = new float3(addButtonX, ButtonTop, 0f);

    int totalControlsWidth = MinimizeButtonWidth + MaximizeButtonWidth + CloseButtonWidth + (ButtonSpacing * 2);
    float titleX = addButtonX + AddMenuButtonWidth + ButtonSpacing + TitleSpacing;
    float controlStartX = Math.Max(titleX + MinimumTitleWidth, width - totalControlsWidth - EdgePadding);

    TitleEntity.Position = new float3(titleX, GetTitleVerticalOffset(), 0f);
    TitleTextComponent.Size = new int2(Math.Max(1, (int)Math.Floor(controlStartX - titleX - TitleSpacing)), TitleTextComponent.Size.Y);

    LayoutWindowControls(controlStartX);
    UpdateDragRegion(controlStartX);
    FileMenu.UpdateLayout(HostSize);
    AddMenu.UpdateLayout(HostSize);
}
```

```csharp
void UpdateDragRegion(float controlStartX) {
    float dragRegionX = EdgePadding + FileMenuButtonWidth + ButtonSpacing + AddMenuButtonWidth + ButtonSpacing;
    int dragRegionWidth = Math.Max(0, (int)Math.Floor(controlStartX - dragRegionX - ButtonSpacing));

    DragRegionEntity.Position = new float3(dragRegionX, 0f, 0f);
    DragRegion.Size = new int2(dragRegionWidth, HeightPixels);
    DragRegionInputSurface.Size = new int2(dragRegionWidth, HeightPixels);
}
```

```csharp
void HideMenus() {
    FileMenu.Hide();
    AddMenu.Hide();
}

void ToggleFileMenu() {
    if (FileMenu.IsVisible) {
        FileMenu.Hide();
        return;
    }

    AddMenu.Hide();
    FileMenu.Show(FileMenuItems, GetFileMenuPosition(), HostSize);
}

void ToggleAddMenu() {
    if (AddMenu.IsVisible) {
        AddMenu.Hide();
        return;
    }

    FileMenu.Hide();
    AddMenu.Show(AddMenuItems, GetAddMenuPosition(), HostSize);
}
```

```csharp
int2 GetAddMenuPosition() {
    int x = (int)Math.Round(AddMenuButtonEntity.Position.X);
    return new int2(x, HeightPixels);
}

void HandleTitleBarCursorEvent(int2 pos, int2 delta, PointerInteraction state) {
    if (state != PointerInteraction.Press) {
        return;
    }

    if (FileMenu.IsVisible || AddMenu.IsVisible) {
        HideMenus();
        return;
    }

    // existing drag and double-click logic stays unchanged
}
```

```csharp
void HandleToggleMaximizeRequested() {
    HideMenus();
    RaiseToggleMaximizeRequested();
}

void HandleMinimizeRequested() {
    HideMenus();
    MinimizeRequested?.Invoke();
}

void HandleCloseRequested() {
    HideMenus();
    CloseRequested?.Invoke();
}
```

- [ ] **Step 4: Run the title-bar tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorTitleBarAddMenuTests`

Expected: PASS with `Add` button layout, item population, and menu exclusivity green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs
git commit -m "feat: add title bar add menu"
```

## Task 2: Create Scene Entities For `Empty`, `Cube`, And `Plane`

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorSceneCreationService.cs`
- Modify: `engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs`
- Create: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`

- [ ] **Step 1: Write the failing scene-creation tests**

```csharp
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.scene {
    /// <summary>
    /// Verifies scene entities created through the editor add flow.
    /// </summary>
    public class EditorSceneCreationServiceTests : IDisposable {
        /// <summary>
        /// Temporary project root used by scene-creation tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core services required for generated primitive creation.
        /// </summary>
        public EditorSceneCreationServiceTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-scene-creation-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(TempProjectRootPath, "assets", "Scenes"));

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EngineGeneratedModelCache.ResetForTests();
        }

        /// <summary>
        /// Ensures `Add > Empty` creates a root scene entity at the origin.
        /// </summary>
        [Fact]
        public void CreateEmpty_CreatesRootSceneEntityAtOrigin() {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = service.CreateEmpty();

            Assert.Equal("Empty", entity.Name);
            Assert.Equal(EditorLayerMasks.SceneObjects, entity.LayerMask);
            Assert.Null(entity.Parent);
            Assert.Equal(float3.Zero, entity.LocalPosition);
            Assert.Equal(float3.One, entity.LocalScale);
            Assert.Equal(float4.Identity, entity.LocalOrientation);
        }

        /// <summary>
        /// Ensures primitive creation stores the generated model reference required by scene saving.
        /// </summary>
        [Theory]
        [InlineData("Cube", EngineGeneratedModelCache.CubeAssetId, EngineGeneratedAssetProvider.CubeRelativePath)]
        [InlineData("Plane", EngineGeneratedModelCache.PlaneAssetId, EngineGeneratedAssetProvider.PlaneRelativePath)]
        public void CreatePrimitive_StoresGeneratedModelReference(string expectedName, string assetId, string relativePath) {
            EditorSceneCreationService service = new EditorSceneCreationService();

            EditorEntity entity = expectedName == "Cube" ? service.CreateCube() : service.CreatePlane();

            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(entity.Components, component => component is MeshComponent));
            EntitySaveComponent saveComponent = Assert.IsType<EntitySaveComponent>(Assert.Single(entity.Components, component => component is EntitySaveComponent));

            Assert.Equal(expectedName, entity.Name);
            Assert.NotNull(meshComponent.Model);
            Assert.True(saveComponent.TryGetComponentState(meshComponent, out EntityComponentSaveState saveState));
            Assert.True(saveState.TryGetAssetReference("Model", out SceneAssetReference modelReference));
            Assert.Equal(SceneAssetReferenceSourceKind.Generated, modelReference.SourceKind);
            Assert.Equal(EngineGeneratedAssetProvider.ProviderIdValue, modelReference.ProviderId);
            Assert.Equal(relativePath, modelReference.RelativePath);
            Assert.Equal(assetId, modelReference.AssetId);
        }

        /// <summary>
        /// Ensures a created primitive can be saved immediately through the existing scene-save flow.
        /// </summary>
        [Fact]
        public void CreateCube_WhenSaved_WritesHelenFileWithoutAdditionalPickerMetadata() {
            ComponentPersistenceRegistry registry = new ComponentPersistenceRegistry();
            registry.Register(new MeshComponentPersistenceDescriptor());
            EditorSceneCreationService service = new EditorSceneCreationService();
            SceneSaveService saveService = new SceneSaveService(TempProjectRootPath, registry);
            string scenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "CreatedFromAdd.helen");

            service.CreateCube();
            saveService.Save(scenePath);

            Assert.True(File.Exists(scenePath));
        }
    }
}
```

- [ ] **Step 2: Run the scene-creation tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSceneCreationServiceTests`

Expected: build failure because `EditorSceneCreationService` does not exist and `EngineGeneratedAssetProvider` does not expose primitive path constants.

- [ ] **Step 3: Write the minimal implementation**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Publishes built-in generated engine assets such as primitive models.
    /// </summary>
    public class EngineGeneratedAssetProvider : IGeneratedAssetProvider {
        /// <summary>
        /// Stable provider identifier used by engine-generated entries.
        /// </summary>
        public const string ProviderIdValue = "engine";
        /// <summary>
        /// Virtual entry path for the generated cube primitive.
        /// </summary>
        public const string CubeRelativePath = "Engine/Models/Cube";
        /// <summary>
        /// Virtual entry path for the generated plane primitive.
        /// </summary>
        public const string PlaneRelativePath = "Engine/Models/Plane";

        /// <summary>
        /// Gets the stable provider identifier used by engine-generated entries.
        /// </summary>
        public string ProviderId => ProviderIdValue;
    }
}
```

```csharp
if (string.Equals(relativePath, EngineModelsPath, StringComparison.Ordinal)) {
    entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Cube", CubeRelativePath, AssetEntryKind.Model, ProviderIdValue, EngineGeneratedModelCache.CubeAssetId));
    entries.Add(AssetBrowserEntry.CreateGeneratedAsset("Plane", PlaneRelativePath, AssetEntryKind.Model, ProviderIdValue, EngineGeneratedModelCache.PlaneAssetId));
}
```

```csharp
namespace helengine.editor {
    /// <summary>
    /// Creates user-authored scene entities for the editor `Add` menu.
    /// </summary>
    public class EditorSceneCreationService {
        /// <summary>
        /// Stable save-state slot name used by `MeshComponentPersistenceDescriptor`.
        /// </summary>
        const string MeshModelReferenceName = "Model";

        /// <summary>
        /// Creates a root empty entity for the scene.
        /// </summary>
        public EditorEntity CreateEmpty() {
            return CreateBaseEntity("Empty");
        }

        /// <summary>
        /// Creates a root cube primitive for the scene.
        /// </summary>
        public EditorEntity CreateCube() {
            return CreatePrimitive("Cube", EngineGeneratedModelCache.CubeAssetId, EngineGeneratedAssetProvider.CubeRelativePath);
        }

        /// <summary>
        /// Creates a root plane primitive for the scene.
        /// </summary>
        public EditorEntity CreatePlane() {
            return CreatePrimitive("Plane", EngineGeneratedModelCache.PlaneAssetId, EngineGeneratedAssetProvider.PlaneRelativePath);
        }

        /// <summary>
        /// Creates one primitive entity backed by a generated runtime model.
        /// </summary>
        EditorEntity CreatePrimitive(string name, string assetId, string relativePath) {
            RuntimeModel runtimeModel = EngineGeneratedModelCache.GetRuntimeModel(assetId);
            EditorEntity entity = CreateBaseEntity(name);

            try {
                EntitySaveComponent saveComponent = FindSaveComponent(entity);
                MeshComponent meshComponent = new MeshComponent {
                    Model = runtimeModel
                };
                entity.AddComponent(meshComponent);
                saveComponent.SetAssetReference(meshComponent, MeshModelReferenceName, BuildGeneratedModelReference(relativePath, assetId));
                return entity;
            } catch {
                entity.Enabled = false;
                Core.Instance.ObjectManager.RemoveEntity(entity);
                throw;
            }
        }

        /// <summary>
        /// Creates a root scene entity with the standard defaults used by add commands.
        /// </summary>
        EditorEntity CreateBaseEntity(string name) {
            return new EditorEntity {
                Name = name,
                LayerMask = EditorLayerMasks.SceneObjects,
                LocalPosition = float3.Zero,
                LocalScale = float3.One,
                LocalOrientation = float4.Identity
            };
        }

        /// <summary>
        /// Builds the stable generated-model reference stored for created primitives.
        /// </summary>
        SceneAssetReference BuildGeneratedModelReference(string relativePath, string assetId) {
            return new SceneAssetReference {
                SourceKind = SceneAssetReferenceSourceKind.Generated,
                RelativePath = relativePath,
                ProviderId = EngineGeneratedAssetProvider.ProviderIdValue,
                AssetId = assetId
            };
        }

        /// <summary>
        /// Resolves the hidden save component attached to one editor entity.
        /// </summary>
        EntitySaveComponent FindSaveComponent(EditorEntity entity) {
            for (int i = 0; i < entity.Components.Count; i++) {
                if (entity.Components[i] is EntitySaveComponent saveComponent) {
                    return saveComponent;
                }
            }

            throw new InvalidOperationException("Editor entities created by the add flow must include EntitySaveComponent.");
        }
    }
}
```

- [ ] **Step 4: Run the scene-creation tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSceneCreationServiceTests`

Expected: PASS with `Empty`, `Cube`, and `Plane` creation green and immediate `.helen` save compatibility confirmed.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs
git commit -m "feat: add scene creation service for add menu"
```

## Task 3: Wire `Add` Commands Through `EditorSession`

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`

- [ ] **Step 1: Write the failing session tests**

```csharp
using System.Reflection;
using System.Runtime.CompilerServices;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the editor session handles title-bar add commands.
    /// </summary>
    public class EditorSessionAddMenuTests : IDisposable {
        /// <summary>
        /// Temporary project root used by add-menu session tests.
        /// </summary>
        readonly string TempProjectRootPath;

        /// <summary>
        /// Initializes the core services required for hierarchy and selection updates.
        /// </summary>
        public EditorSessionAddMenuTests() {
            TempProjectRootPath = Path.Combine(Path.GetTempPath(), "helengine-editor-add-menu-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(TempProjectRootPath);

            Core core = new Core(new CoreInitializationOptions {
                ContentRootPath = TempProjectRootPath
            });
            core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null);
            EditorSelectionService.ClearSelection();
            EngineGeneratedModelCache.ResetForTests();
        }

        /// <summary>
        /// Ensures `Add > Empty` creates and selects a root scene entity.
        /// </summary>
        [Fact]
        public void HandleAddEmptyRequested_CreatesAndSelectsRootSceneEntity() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddEmptyRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            Assert.Equal("Empty", selectedEntity.Name);
            Assert.Equal(float3.Zero, selectedEntity.LocalPosition);
            Assert.Equal(1, GetHierarchyRowCount(session));
        }

        /// <summary>
        /// Ensures `Add > Cube` creates a mesh-backed scene entity and selects it.
        /// </summary>
        [Fact]
        public void HandleAddCubeRequested_CreatesMeshEntityAndSelectsIt() {
            EditorSession session = CreateSessionForAddCommands();

            InvokePrivate(session, "HandleAddCubeRequested");

            EditorEntity selectedEntity = Assert.IsType<EditorEntity>(EditorSelectionService.SelectedEntity);
            MeshComponent meshComponent = Assert.IsType<MeshComponent>(Assert.Single(selectedEntity.Components, component => component is MeshComponent));

            Assert.Equal("Cube", selectedEntity.Name);
            Assert.NotNull(meshComponent.Model);
            Assert.Equal(1, GetHierarchyRowCount(session));
        }

        /// <summary>
        /// Creates a partially initialized editor session containing only the collaborators used by add handlers.
        /// </summary>
        EditorSession CreateSessionForAddCommands() {
            EditorSession session = (EditorSession)RuntimeHelpers.GetUninitializedObject(typeof(EditorSession));
            SceneHierarchyPanel sceneHierarchyPanel = new SceneHierarchyPanel(CreateFont());
            EditorSceneCreationService sceneCreationService = new EditorSceneCreationService();

            SetPrivateField(session, "sceneHierarchyPanel", sceneHierarchyPanel);
            SetPrivateField(session, "SceneCreationService", sceneCreationService);

            return session;
        }
    }
}
```

- [ ] **Step 2: Run the session tests to verify they fail**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionAddMenuTests`

Expected: build failure because `EditorSession` does not own `SceneCreationService` and the add-command handlers do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

```csharp
/// <summary>
/// Creates new scene entities for title-bar add commands.
/// </summary>
readonly EditorSceneCreationService SceneCreationService;
```

```csharp
SceneCreationService = new EditorSceneCreationService();
titleBar.AddEmptyRequested += HandleAddEmptyRequested;
titleBar.AddCubeRequested += HandleAddCubeRequested;
titleBar.AddPlaneRequested += HandleAddPlaneRequested;
```

```csharp
titleBar.AddEmptyRequested -= HandleAddEmptyRequested;
titleBar.AddCubeRequested -= HandleAddCubeRequested;
titleBar.AddPlaneRequested -= HandleAddPlaneRequested;
```

```csharp
/// <summary>
/// Handles the Add Empty command from the editor title bar.
/// </summary>
void HandleAddEmptyRequested() {
    CreateAndSelectSceneEntity(SceneCreationService.CreateEmpty);
}

/// <summary>
/// Handles the Add Cube command from the editor title bar.
/// </summary>
void HandleAddCubeRequested() {
    CreateAndSelectSceneEntity(SceneCreationService.CreateCube);
}

/// <summary>
/// Handles the Add Plane command from the editor title bar.
/// </summary>
void HandleAddPlaneRequested() {
    CreateAndSelectSceneEntity(SceneCreationService.CreatePlane);
}
```

```csharp
/// <summary>
/// Creates one scene entity, refreshes the hierarchy, and selects the result.
/// </summary>
void CreateAndSelectSceneEntity(Func<EditorEntity> createEntity) {
    if (createEntity == null) {
        throw new ArgumentNullException(nameof(createEntity));
    }

    Entity previousSelection = EditorSelectionService.SelectedEntity;

    try {
        EditorEntity entity = createEntity();
        sceneHierarchyPanel.RefreshHierarchy();
        EditorSelectionService.SetSelectedEntity(entity);
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

- [ ] **Step 4: Run the session tests to verify they pass**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter EditorSessionAddMenuTests`

Expected: PASS with `Add > Empty` and `Add > Cube` creating root scene entities, refreshing the hierarchy, and updating selection.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs
git commit -m "feat: wire add menu into editor session"
```

## Task 4: Full Verification

**Files:**
- Test: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionAddMenuTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] **Step 1: Run the focused add-menu test set**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --filter "EditorTitleBarAddMenuTests|EditorSceneCreationServiceTests|EditorSessionAddMenuTests|SceneSaveServiceTests|EditorSessionSceneSaveTests"`

Expected: PASS with title-bar layout, primitive creation, session wiring, and save compatibility all green together.

- [ ] **Step 2: Run the full editor test suite**

Run: `dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj'`

Expected: PASS with the full editor suite green and no regressions introduced by the new add-menu flow.

- [ ] **Step 3: Commit the verified implementation**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor/managers/asset/EngineGeneratedAssetProvider.cs engine/helengine.editor/managers/scene/EditorSceneCreationService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/EditorSessionAddMenuTests.cs
git commit -m "feat: add title bar primitive creation menu"
```

## Self-Review

### Spec Coverage

- `Add` as a second fixed title-bar menu beside `File`: covered by Task 1.
- `Empty`, `Cube`, and `Plane` commands with explicit title-bar events: covered by Task 1.
- Dedicated scene-creation service instead of inline session construction: covered by Task 2.
- Root scene-entity defaults for all add commands: covered by Task 2.
- Generated model references written into `EntitySaveComponent`: covered by Task 2.
- Immediate selection and hierarchy refresh in `EditorSession`: covered by Task 3.
- Save compatibility for newly created primitives: covered by Task 2 and the focused verification in Task 4.

### Placeholder Scan

- No `TODO`, `TBD`, or "similar to" placeholders remain.
- Every task includes exact file paths, code snippets, test commands, and expected outcomes.
- Error-handling behavior is described concretely in the `CreateAndSelectSceneEntity(...)` and `CreatePrimitive(...)` snippets instead of as generic "add validation later" language.

### Type Consistency

- `EditorSceneCreationService`, `SceneCreationService`, `AddMenuButtonEntity`, `AddMenu`, `AddEmptyRequested`, `AddCubeRequested`, and `AddPlaneRequested` are named consistently across all tasks.
- Generated primitive references consistently use `EngineGeneratedAssetProvider.ProviderIdValue`, `EngineGeneratedAssetProvider.CubeRelativePath`, `EngineGeneratedAssetProvider.PlaneRelativePath`, and the `"Model"` save-state slot expected by `MeshComponentPersistenceDescriptor`.
- The session wiring always routes through `CreateAndSelectSceneEntity(...)`, so hierarchy refresh and selection behavior are not duplicated under slightly different method names.

# Component Platform Tab Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add always-visible platform tabs to the entity properties inspector, switch the component editing context by platform, and persist component-scoped per-platform overrides that diverge from a common baseline only when the user edits a non-common platform tab.

**Architecture:** Reuse the shared `PlatformTabStripView` under the selected entity name inside `PropertiesPanel`, but keep the inspector chrome compact with no attached lower panel. Keep the live entity component list as the common baseline. Add editor-only per-component platform override metadata to `EntityComponentSaveState`, persist that metadata by wrapping each serialized editor component payload with an editor-only override envelope, and route inspector edits through a new component-platform editing service that materializes detached override component clones on demand. The runtime scene model and player packaging remain unchanged.

**Tech Stack:** C#, helengine editor UI entities/components, existing component persistence descriptors, editor scene save/load services, xUnit editor tests

---

### Task 1: Add Failing Tests For Inspector Tabs And Component Override Persistence

**Files:**
- Modify: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- Modify: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Modify: `engine/helengine.editor.tests/EntitySaveComponentTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Add a failing inspector-shell test for the always-visible platform tab row**

```csharp
[Fact]
public void ShowEntityProperties_WhenEntityIsSelected_ShowsPlatformTabsDirectlyUnderEntityName() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity();
    entity.Name = "Test Entity";
    entity.AddComponent(new CameraComponent());

    panel.Size = new int2(320, 480);
    panel.ShowEntityProperties(entity);

    PlatformTabStripView tabStrip = GetPrivateField<PlatformTabStripView>(panel, "ComponentPlatformTabStrip");

    Assert.NotNull(tabStrip);
    Assert.True(tabStrip.Root.Enabled);
    Assert.Contains("common", tabStrip.PlatformIds, StringComparer.OrdinalIgnoreCase);
}
```

- [ ] **Step 2: Add a failing mutation test for divergence-on-edit behavior**

```csharp
[Fact]
public void HandleScalarSubmitted_WhenEditingOnWindowsTab_CreatesIndependentComponentOverride() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    EditorEntity entity = new EditorEntity();
    CameraComponent camera = new CameraComponent();
    entity.AddComponent(camera);

    panel.ShowEntityProperties(entity);
    SetSelectedInspectorPlatform(panel, "windows");

    ComponentPropertiesView view = GetPrivateField<ComponentPropertiesView>(panel, "ComponentView");
    ComponentPropertyRow row = GetSingleRow(view, "FarPlaneDistance");
    row.ScalarField.Text = "200";
    InvokePrivate(view, "HandleScalarSubmitted", row.ScalarField);

    EntitySaveComponent saveComponent = GetEntitySaveComponent(entity);

    Assert.True(saveComponent.GetOrCreateComponentState(camera).HasPlatformOverride("windows"));
    Assert.NotEqual(camera.FarPlaneDistance, GetPlatformOverrideScalar(saveComponent, camera, "windows", "FarPlaneDistance"));
}
```

- [ ] **Step 3: Add a failing persistence test for scene save/load round-tripping component overrides**

```csharp
[Fact]
public void SaveAndLoad_WhenComponentHasWindowsOverride_RoundTripsTheOverridePayload() {
    // Arrange one entity with a common camera component and a windows override.
    // Save the scene, reload it, and confirm the override metadata still exists
    // while the live entity still carries only the common component instance.
}
```

- [ ] **Step 4: Run the focused test slice to verify it fails**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PropertiesPanelComponentShellTests|FullyQualifiedName~helengine.editor.tests.PropertiesPanelMutationTests|FullyQualifiedName~helengine.editor.tests.EntitySaveComponentTests|FullyQualifiedName~helengine.editor.tests.serialization.scene.SceneSaveServiceTests"`

Expected: FAIL because the inspector has no component platform tabs yet and component save-state does not track per-platform overrides.

- [ ] **Step 5: Commit the failing tests**

```bash
git add engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/EntitySaveComponentTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "test: cover component platform override inspector flow"
```

### Task 2: Add Editor-Only Component Platform Override State And Payload Envelope

**Files:**
- Modify: `engine/helengine.editor/components/persistence/EntityComponentSaveState.cs`
- Create: `engine/helengine.editor/components/persistence/EntityComponentPlatformOverrideState.cs`
- Create: `engine/helengine.editor/serialization/scene/ComponentPlatformOverridePayloadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/EntitySaveComponentTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Extend per-component editor save-state with platform override entries**

```csharp
namespace helengine {
    /// <summary>
    /// Stores editor-only serialized component override payload for one target platform.
    /// </summary>
    public class EntityComponentPlatformOverrideState {
        /// <summary>
        /// Gets or sets the platform identifier that owns this override.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the serialized editor component payload for the override.
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}
```

```csharp
readonly Dictionary<string, EntityComponentPlatformOverrideState> PlatformOverridesById;

public void SetPlatformOverride(string platformId, EntityComponentPlatformOverrideState overrideState) { }
public bool TryGetPlatformOverride(string platformId, out EntityComponentPlatformOverrideState overrideState) { }
public IEnumerable<EntityComponentPlatformOverrideState> EnumeratePlatformOverrides() { }
public bool HasPlatformOverride(string platformId) { }
```

- [ ] **Step 2: Add one central payload wrapper that preserves existing descriptors**

```csharp
public sealed class ComponentPlatformOverridePayloadService {
    public SceneComponentAssetRecord Wrap(SceneComponentAssetRecord baseRecord, EntityComponentSaveState saveState) {
    }

    public SceneComponentAssetRecord UnwrapBaseRecord(SceneComponentAssetRecord wrappedRecord) {
    }

    public IReadOnlyList<EntityComponentPlatformOverrideState> ReadOverrideStates(SceneComponentAssetRecord wrappedRecord) {
    }
}
```

Implementation notes:
- Keep each concrete descriptor unchanged.
- Serialize the common/base component exactly as today.
- Wrap the descriptor payload in one editor-only tagged envelope containing:
  - base payload bytes
  - zero or more per-platform override payload blobs keyed by platform id
- Unwrap before descriptor deserialization in `SceneLoadService`.
- Rehydrate override entries back into `EntityComponentSaveState` after the base component is loaded.

- [ ] **Step 3: Update scene save/load services to use the wrapper centrally**

```csharp
SceneComponentAssetRecord descriptorRecord = descriptor.SerializeComponent(component, persistedComponentIndex, saveState);
SceneComponentAssetRecord persistedRecord = OverridePayloadService.Wrap(descriptorRecord, saveState);
componentRecords.Add(persistedRecord);
AppendAssetReferences(saveState, assetReferences, assetReferenceKeys);
AppendPlatformOverrideAssetReferences(component, saveState, descriptor, assetReferences, assetReferenceKeys);
```

```csharp
SceneComponentAssetRecord baseRecord = OverridePayloadService.UnwrapBaseRecord(record);
Component component = descriptor.DeserializeComponent(baseRecord, saveComponent, ReferenceResolver);
RestorePlatformOverrides(component, saveComponent, record);
```

- [ ] **Step 4: Add focused tests for the new save-state and payload envelope**

Verification goals:
- `EntityComponentSaveState` can store and retrieve multiple platform override entries.
- scene save writes wrapped payloads when overrides exist.
- scene load restores platform override metadata into the hidden save component without adding extra live runtime components to the entity.

- [ ] **Step 5: Run the focused persistence slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.EntitySaveComponentTests|FullyQualifiedName~helengine.editor.tests.serialization.scene.SceneSaveServiceTests"`

Expected: PASS

- [ ] **Step 6: Commit the persistence model**

```bash
git add engine/helengine.editor/components/persistence/EntityComponentSaveState.cs engine/helengine.editor/components/persistence/EntityComponentPlatformOverrideState.cs engine/helengine.editor/serialization/scene/ComponentPlatformOverridePayloadService.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/EntitySaveComponentTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "feat: persist editor component platform overrides"
```

### Task 3: Add A Component Platform Editing Service For Lazy Divergence

**Files:**
- Create: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`

- [ ] **Step 1: Add a service that resolves effective component state for one inspector platform**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Resolves and materializes effective component editing targets for inspector platform tabs.
    /// </summary>
    public sealed class ComponentPlatformEditingService {
        public Component GetEditableComponent(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
        }

        public EntityComponentSaveState GetEditableSaveState(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
        }

        public void EnsurePlatformOverride(Component commonComponent, EntitySaveComponent saveComponent, string platformId) {
        }
    }
}
```

Implementation notes:
- `common` returns the live component and its current save-state.
- non-common first checks for an existing override payload.
- if none exists, clone the common component by round-tripping through its persistence descriptor.
- store the override payload back into `EntityComponentSaveState` only after the first edit on that platform.
- keep override component clones detached from the live entity; do not attach duplicate runtime components to the scene.

- [ ] **Step 2: Reroute row binding and mutation through the editing service**

```csharp
public void ShowComponents(Entity entity, string platformId) {
    CurrentEntity = entity;
    CurrentPlatformId = platformId;
    ...
    Component editableComponent = EditingService.GetEditableComponent(component, saveComponent, platformId);
    BindRowsAgainstEditableComponent(section, component, editableComponent, platformId);
}
```

```csharp
void SetRowValue(ComponentPropertyRow row, object value) {
    EnsureEditableOverride(row);
    row.Property.SetValue(row.TargetComponent, value);
    PersistEditableOverride(row);
    EditorSceneMutationService.MarkSceneMutated();
}
```

Implementation notes:
- `ComponentPropertyRow` needs enough context to distinguish:
  - common live component
  - current editable component target
  - current platform id
  - editable save-state used by asset pickers
- asset picker paths must stop inferring save-state only from `row.TargetComponent.Parent`, because platform override clones are detached.

- [ ] **Step 3: Keep component add/remove as common-scene operations in this first pass**

```csharp
// Do not create platform-specific component lists yet.
// Add/remove component actions still mutate the common live entity only.
// Platform tabs in this pass override component property values, not component membership.
```

- [ ] **Step 4: Add focused mutation and asset-reference tests**

Verification goals:
- editing on `common` still mutates the live component directly
- editing on `windows` creates an override and leaves the common component unchanged
- material/model/font picks on a platform override update the override payload/save-state rather than the common component save-state

- [ ] **Step 5: Run the component-editing slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PropertiesPanelMutationTests|FullyQualifiedName~helengine.editor.tests.ComponentPropertiesViewScenePersistenceTests"`

Expected: PASS

- [ ] **Step 6: Commit the editing service**

```bash
git add engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/PropertiesPanelMutationTests.cs engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs
git commit -m "feat: add component platform editing context"
```

### Task 4: Add Inspector Platform Tabs To Properties Panel

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Add the shared tab strip directly under the selected entity name**

```csharp
readonly PlatformTabStripView ComponentPlatformTabStrip;
readonly List<string> InspectorPlatformIds;
string CurrentInspectorPlatformId;
IReadOnlyList<string> SupportedPlatforms;
```

```csharp
ComponentPlatformTabStrip = new PlatformTabStripView(font, EditorLayerMasks.PropertiesPanelContent, 96, 24, 0, 24);
ScrollContentRoot.AddChild(ComponentPlatformTabStrip.Root);
```

Implementation notes:
- tab order should be `common` first, then supported project platforms
- no attached lower panel chrome
- keep the strip compact and scrollable like the shared asset/build usage

- [ ] **Step 2: Thread supported-platform context in from `EditorSession`**

```csharp
propertiesPanel = new PropertiesPanel(
    uiFont,
    EditorContentManager,
    fileSystemModelResolver,
    titleBar.Entity,
    scriptHotReloadService,
    CurrentUiMetrics,
    fileSystemFontResolver,
    SupportedPlatforms);
```

If constructor expansion is too noisy, add one narrow setter such as `SetSupportedPlatforms(IReadOnlyList<string> platformIds, string activePlatformId)` and call it from `EditorSession` after project bootstrap and after supported-platform changes.

- [ ] **Step 3: Switch component view context when the inspector tab changes**

```csharp
void HandleInspectorPlatformTabChanged(string platformId) {
    CurrentInspectorPlatformId = platformId;
    if (SelectedEntity != null) {
        ComponentView.ShowComponents(SelectedEntity, CurrentInspectorPlatformId);
        LayoutLines();
    }
}
```

Implementation notes:
- keep the entity name row at the top
- place the tab strip immediately below the name row
- transforms remain visually below the tab row in this pass, but component overrides only apply to the component property view
- do not add lower panel chrome here

- [ ] **Step 4: Add focused shell/layout tests**

Verification goals:
- inspector tab strip is always visible for entity selection
- `common` plus project platforms appear in stable order
- switching tabs rebuilds the component editor context without breaking layout
- strip still hides for non-entity asset summary views

- [ ] **Step 5: Run the inspector shell slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PropertiesPanelComponentShellTests"`

Expected: PASS

- [ ] **Step 6: Commit the inspector integration**

```bash
git add engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "feat: add platform tabs to component inspector"
```

### Task 5: Final Verification And Handoff

**Files:**
- Modify: `docs/superpowers/plans/2026-05-08-component-platform-tab-overrides.md`

- [ ] **Step 1: Run the full targeted verification slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PropertiesPanelComponentShellTests|FullyQualifiedName~helengine.editor.tests.PropertiesPanelMutationTests|FullyQualifiedName~helengine.editor.tests.EntitySaveComponentTests|FullyQualifiedName~helengine.editor.tests.ComponentPropertiesViewScenePersistenceTests|FullyQualifiedName~helengine.editor.tests.serialization.scene.SceneSaveServiceTests|FullyQualifiedName~helengine.editor.tests.PlatformTabStripViewTests"`

Expected: PASS

- [ ] **Step 2: Perform one manual editor sanity pass**

Checklist:
- select an entity and confirm the platform strip appears under the entity name
- switch between `common` and one platform tab
- edit one scalar property on a non-common tab and confirm common stays unchanged
- save and reload the scene
- confirm the platform override still exists

- [ ] **Step 3: Update this plan checklist to reflect completed execution**

```markdown
- [x] Step completed
```

- [ ] **Step 4: Commit the final plan-status update if needed**

```bash
git add docs/superpowers/plans/2026-05-08-component-platform-tab-overrides.md
git commit -m "docs: update component platform override plan status"
```

# Per-Entity Platform Override Sidecars Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` (recommended) or `superpowers:executing-plans` to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Replace the embedded platform-override design with per-platform scene sidecars such as `scene.windows.helen`, while keeping the override system editor-only. The editor loads the base scene plus all discovered sidecars, the Properties panel exposes `Base` plus supported platform tabs, and packaging resolves `base + target sidecar` into one flattened runtime scene that omits disabled entities.

**Architecture:** `scene.helen` remains the shared scene. Each `scene.<platform>.helen` sidecar stores only entity-local overrides keyed by stable base entity id. `EditorEntity` keeps base state live, while `EntitySaveComponent` holds editor-only override metadata in memory. Save/load services split base and sidecar persistence. The Windows scene packager resolves one target platform before component rewrite so runtime players remain unaware of sidecars.

**Tech Stack:** C#/.NET 9, existing `SceneAsset` serialization pipeline, editor UI (`PropertiesPanel`, `ComponentPropertiesView`), Windows scene packaging (`EditorWindowsBuildScenePackager`), xUnit.

---

## File Structure

### New files

- `engine/helengine.core/assets/raw/scene/ScenePlatformOverrideAsset.cs`
  - Serialized sidecar asset that stores one platform id plus entity override records.
- `engine/helengine.core/assets/raw/scene/SceneEntityPlatformOverrideAsset.cs`
  - Serialized entity-local override record keyed by stable base entity id.
- `engine/helengine.core/assets/raw/scene/SceneEntityPlatformComponentOverrideAsset.cs`
  - Serialized component removal, addition, and full payload override data for one entity/platform pair.
- `engine/helengine.editor/model/EntityPlatformEditScope.cs`
  - Editor-side value object describing whether the Properties panel is editing `Base` or one specific platform.
- `engine/helengine.editor/model/ResolvedSceneEntityPlatformState.cs`
  - Editor-side resolved entity state used by copy-from-platform and packaging resolution.
- `engine/helengine.editor/serialization/scene/ScenePlatformSidecarLocator.cs`
  - Resolves sidecar file paths from a base scene path and discovers all matching platform sidecars.
- `engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs`
  - Resolves one base entity plus one platform sidecar override into a flattened entity-local result.
- `engine/helengine.editor/components/ui/EntityPlatformTabsView.cs`
  - Reusable tab strip UI for `Base` plus supported platforms inside the Properties panel.
- `engine/helengine.editor/components/ui/EntityPlatformCopyDialog.cs`
  - Modal dialog that lets users copy the selected entity state from `Base` or another platform into the current platform override.

### Modified files

- `engine/helengine.core/assets/raw/scene/SceneAsset.cs`
  - Keeps the base scene shape unchanged and does not embed platform override data.
- `engine/helengine.core/assets/raw/scene/SceneEntityAsset.cs`
  - Remains the shared base entity payload and should not carry embedded platform overrides.
- `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
  - Gains editor-only in-memory platform override storage keyed by platform id.
- `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
  - Loads the base scene, discovers all sidecars, and attaches override data back onto loaded entities.
- `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
  - Saves the base scene separately from per-platform sidecars.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  - Adds platform tabs, base-only name editing, platform-aware transform editing, and copy-from-platform flow.
- `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  - Renders and mutates component lists and property editors against `Base` or one platform override scope.
- `engine/helengine.editor/EditorSession.cs`
  - Provides supported platform ids and active platform state to the Properties panel.
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
  - Resolves base plus target sidecar and omits disabled entities before packaged runtime scene output.
- `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
  - Adds binary support for the new sidecar asset type.

### Test files

- `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- `engine/helengine.editor.tests/BinarySerializationTests.cs`

---

### Task 1: Add Serialized Sidecar Asset Types And Binary Support

**Files:**
- Create: `engine/helengine.core/assets/raw/scene/ScenePlatformOverrideAsset.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneEntityPlatformOverrideAsset.cs`
- Create: `engine/helengine.core/assets/raw/scene/SceneEntityPlatformComponentOverrideAsset.cs`
- Modify: `engine/helengine.files/assets/EditorAssetBinarySerializer.cs`
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing binary serializer test for one platform sidecar asset**

Add a new test to `engine/helengine.editor.tests/BinarySerializationTests.cs` that creates one `ScenePlatformOverrideAsset` for `windows` containing one entity override keyed by entity id `entity-root` and round-trips it through `AssetSerializer`.

Assert:

- asset type remains `ScenePlatformOverrideAsset`
- platform id round-trips as `windows`
- entity override id round-trips as `entity-root`
- component override payload bytes round-trip unchanged

- [ ] **Step 2: Run the focused binary serializer test to verify it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests.SerializeAndDeserialize_WhenAssetIsScenePlatformOverrideAsset_RoundTripsSidecarPayload" -v minimal
```

Expected: FAIL because the sidecar asset type and serializer support do not exist yet.

- [ ] **Step 3: Add the sidecar asset types**

Create:

- `ScenePlatformOverrideAsset`
  - platform id
  - entity override array
- `SceneEntityPlatformOverrideAsset`
  - entity id
  - optional enabled override
  - optional local transform override
  - component override array
- `SceneEntityPlatformComponentOverrideAsset`
  - component type id
  - removed flag
  - serialized payload bytes

Keep XML comments substantive and align member order with repo conventions.

- [ ] **Step 4: Extend `EditorAssetBinarySerializer` for the new sidecar asset kind**

Add:

- new `EditorAssetBinaryValueKind` entry
- serializer dispatch for `ScenePlatformOverrideAsset`
- reader/writer methods for:
  - sidecar asset
  - entity override record
  - transform override payload
  - component override record

Do not change the base `SceneAsset` layout for platform override storage.

- [ ] **Step 5: Run the focused binary serializer test to verify it passes**

Run the same command from Step 2.

- [ ] **Step 6: Commit the sidecar asset scaffolding**

```bash
git add engine/helengine.core/assets/raw/scene/ScenePlatformOverrideAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityPlatformOverrideAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityPlatformComponentOverrideAsset.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
git commit -m "feat: add scene platform override sidecar assets"
```

---

### Task 2: Add Live Editor Override Storage And Base/Sidecar Save-Load Flow

**Files:**
- Create: `engine/helengine.editor/serialization/scene/ScenePlatformSidecarLocator.cs`
- Modify: `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneFileLoadService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`

- [ ] **Step 1: Write the failing save tests for split base and sidecar output**

Add tests covering:

- saving one base scene with no platform overrides writes only `scene.helen`
- saving one entity with a `windows` override writes:
  - `scene.helen`
  - `scene.windows.helen`
- base file does not embed platform override payloads

- [ ] **Step 2: Write the failing load test for discovery of all sidecars**

Add a test that writes:

- `scene.helen`
- `scene.windows.helen`
- `scene.ps2.helen`

Then loads `scene.helen` and asserts the loaded editor entity has both sidecar override records attached editor-side by entity id.

- [ ] **Step 3: Run the focused save/load tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests" -v minimal
```

Expected: FAIL on the new sidecar-specific cases.

- [ ] **Step 4: Extend `EntitySaveComponent` with editor-only platform override storage**

Add editor-only storage keyed by platform id that can hold:

- entity enabled override
- local transform override
- component removals
- component additions
- full component payload overrides

This is the live authoring seam. Do not attach runtime-facing platform data to normal scene components.

- [ ] **Step 5: Add `ScenePlatformSidecarLocator`**

Implement path helpers for:

- discovering all `scene.<platform>.helen` files for a base scene
- resolving one sidecar path for a platform id
- deriving base scene path from a sidecar path when necessary

Keep the convention strict and deterministic.

- [ ] **Step 6: Split save flow between base and sidecars**

Update `SceneSaveService` so:

- base scene save serializes only shared scene state into `scene.helen`
- sidecar save serializes only non-empty platform override records into `scene.<platform>.helen`
- empty sidecars are removed or not written

Base save should not mutate unrelated sidecars except where stale-orphan cleanup is required.

- [ ] **Step 7: Load base scene plus all sidecars**

Update `SceneFileLoadService` so:

- loading `scene.helen` materializes the base scene
- all matching sidecars are discovered and loaded
- loaded sidecar entity overrides are attached to the correct `EditorEntity` by stable entity id

Switching platform tabs later must not require reopening the scene.

- [ ] **Step 8: Run the focused save/load tests to verify they pass**

Run the same command from Step 3.

- [ ] **Step 9: Commit split base/sidecar persistence**

```bash
git add engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/serialization/scene/ScenePlatformSidecarLocator.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs
git commit -m "feat: persist scene platform override sidecars"
```

---

### Task 3: Add Platform Tabs And Base-Only Name Editing To The Properties Panel

**Files:**
- Create: `engine/helengine.editor/model/EntityPlatformEditScope.cs`
- Create: `engine/helengine.editor/components/ui/EntityPlatformTabsView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Write the failing properties-panel tab tests**

Add tests for:

- `ShowEntityProperties_WhenProjectHasSupportedPlatforms_RendersBaseAndPlatformTabs`
- `ShowEntityProperties_WhenPlatformTabIsActive_DisablesNameEditing`
- `ShowEntityProperties_WhenPlatformTabIsActive_BindsTransformFieldsToPlatformOverrideScope`

- [ ] **Step 2: Run the focused properties-panel tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenProjectHasSupportedPlatforms_RendersBaseAndPlatformTabs|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenPlatformTabIsActive_DisablesNameEditing|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenPlatformTabIsActive_BindsTransformFieldsToPlatformOverrideScope" -v minimal
```

- [ ] **Step 3: Implement the tab strip and edit scope**

Add:

- `EntityPlatformEditScope`
- `EntityPlatformTabsView`

Update `PropertiesPanel` to:

- render `Base` plus supported project platforms
- lock the name field when the selected scope is not `Base`
- retarget transform rows to base or platform override storage
- keep the currently active project platform selected by default when available

Update `EditorSession` to pass supported platforms and active platform into `PropertiesPanel`.

- [ ] **Step 4: Run the focused properties-panel tests to verify they pass**

Run the same command from Step 2.

- [ ] **Step 5: Commit the platform-tab UI foundation**

```bash
git add engine/helengine.editor/model/EntityPlatformEditScope.cs engine/helengine.editor/components/ui/EntityPlatformTabsView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "feat: add entity platform tabs for scene sidecars"
```

---

### Task 4: Make Component Editing And Copy-From-Platform Sidecar-Aware

**Files:**
- Create: `engine/helengine.editor/model/ResolvedSceneEntityPlatformState.cs`
- Create: `engine/helengine.editor/components/ui/EntityPlatformCopyDialog.cs`
- Create: `engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Write the failing platform component-edit and copy tests**

Add tests for:

- component removal hidden only on one platform tab
- component addition visible only on one platform tab
- platform-specific scalar/property changes round-trip through override storage
- `Copy From Platform...` replaces the current platform override with the resolved source state

- [ ] **Step 2: Run the focused component-edit tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenPlatformRemovesComponent_HidesThatComponentOnlyOnThatTab|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenPlatformAddsComponent_ShowsItOnlyOnThatTab|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenPlatformPropertyValueDiffers_BindsEditorToSidecarOverride|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenCopyFromPlatformIsConfirmed_ReplacesTheTargetPlatformOverride" -v minimal
```

- [ ] **Step 3: Implement resolved sidecar editing flow**

Add `ResolvedSceneEntityPlatformState` and `ScenePlatformOverrideResolver`.

Update `ComponentPropertiesView` so platform-scope edits write into sidecar override storage instead of mutating base components directly:

- base scope edits the live entity
- platform scope edits the sidecar override model on `EntitySaveComponent`

Support:

- component remove for one platform
- component add for one platform
- full component payload override capture for one platform

- [ ] **Step 4: Add `Copy From Platform...`**

Add the dialog and wire `PropertiesPanel` to:

- show it only on platform tabs
- allow `Base` and other platforms as sources
- resolve the source state
- replace the current platform override for that entity

- [ ] **Step 5: Run the focused component-edit tests to verify they pass**

Run the same command from Step 2.

- [ ] **Step 6: Commit platform-aware entity editing**

```bash
git add engine/helengine.editor/model/ResolvedSceneEntityPlatformState.cs engine/helengine.editor/components/ui/EntityPlatformCopyDialog.cs engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
git commit -m "feat: add sidecar-aware entity platform editing"
```

---

### Task 5: Resolve Sidecars During Packaging And Omit Disabled Entities

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write the failing packaging tests**

Add tests for:

- disabled entity in `scene.windows.helen` is omitted from the packaged Windows scene
- platform transform override changes packaged entity transform
- platform component removal omits only that component
- platform component addition emits only for that platform

- [ ] **Step 2: Run the focused packaging tests to verify they fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenEntityIsDisabledForTargetPlatform_OmitsTheEntity|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenPlatformTransformOverrideExists_EmitsResolvedTransform|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenPlatformRemovesComponent_EmitsEntityWithoutThatComponent|FullyQualifiedName~EditorWindowsBuildScenePackagerTests.Package_WhenPlatformAddsComponent_EmitsEntityWithThatComponentOnlyForThatPlatform" -v minimal
```

- [ ] **Step 3: Resolve base plus target sidecar before component rewrite**

Update `EditorWindowsBuildScenePackager` so the scene walk:

- loads base scene
- loads only the selected target platform sidecar
- resolves every entity against that platform
- omits disabled entities
- emits resolved transforms and resolved component sets
- then rewrites component payloads through the existing packaging transform flow

Runtime scene output must contain no sidecar metadata.

- [ ] **Step 4: Run the focused packaging tests to verify they pass**

Run the same command from Step 2.

- [ ] **Step 5: Commit packaging-side sidecar resolution**

```bash
git add engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: resolve scene sidecars during packaging"
```

---

### Task 6: Run End-To-End Regression Coverage

**Files:**
- Test: `engine/helengine.editor.tests/BinarySerializationTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- Test: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Run the focused sidecar feature slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~PropertiesPanelComponentShellTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests" -v minimal
```

- [ ] **Step 2: Run one broader build/runtime regression slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal
```

- [ ] **Step 3: Commit the completed feature after green verification**

```bash
git add engine/helengine.core/assets/raw/scene/ScenePlatformOverrideAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityPlatformOverrideAsset.cs engine/helengine.core/assets/raw/scene/SceneEntityPlatformComponentOverrideAsset.cs engine/helengine.editor/components/persistence/EntitySaveComponent.cs engine/helengine.editor/model/EntityPlatformEditScope.cs engine/helengine.editor/model/ResolvedSceneEntityPlatformState.cs engine/helengine.editor/serialization/scene/ScenePlatformSidecarLocator.cs engine/helengine.editor/serialization/scene/ScenePlatformOverrideResolver.cs engine/helengine.editor/serialization/scene/SceneSaveService.cs engine/helengine.editor/serialization/scene/SceneFileLoadService.cs engine/helengine.editor/components/ui/EntityPlatformTabsView.cs engine/helengine.editor/components/ui/EntityPlatformCopyDialog.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.files/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "feat: add per-entity platform override sidecars"
```

---

## Self-Review

### Spec coverage

- Base scene plus sidecar file model: covered by Tasks 1 and 2.
- Load all sidecars on scene open: covered by Task 2.
- Base-only rename and platform tabs: covered by Task 3.
- Platform transforms, component membership, and values: covered by Task 4.
- Copy-from-platform workflow: covered by Task 4.
- Packaging-time flattening and omission of disabled entities: covered by Task 5.
- Runtime remains unaware: enforced by Task 5 plus no runtime-file changes.

### Placeholder scan

- No `TODO`, `TBD`, or vague “appropriate error handling” phrasing remains.
- Every implementation task names exact files and verification commands.

### Type consistency

- `ScenePlatformOverrideAsset` is the serialized sidecar asset throughout.
- `SceneEntityPlatformOverrideAsset` is the serialized entity override type throughout.
- `SceneEntityPlatformComponentOverrideAsset` is the serialized component override type throughout.
- `EntityPlatformEditScope` is the editor-side tab/edit context throughout.
- `ResolvedSceneEntityPlatformState` is the resolved editor/packaging entity shape throughout.

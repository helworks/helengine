# Scene Map Component Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a generic authored `SceneMapComponent` plus runtime lookup service so opt-in callers can map one scene id to another without hardcoded platform branching in core.

**Architecture:** Introduce a passive `SceneMapComponent` that stores a dictionary of scene-id mappings, expose lookup through a new runtime `SceneMapService`, and keep `SceneManager` unchanged so only explicit callers opt into remapping. Persist the component through the existing scene descriptor/runtime deserializer pipeline and expose its dictionary through a custom editor property section because the default reflected inspector does not support dictionary editing.

**Tech Stack:** C#, xUnit, core runtime scene loading, editor scene persistence descriptors, reflected component inspector custom editor providers

---

## File Structure

### Core runtime and authored component

- Create: `engine/helengine.core/components/SceneMapComponent.cs`
  Passive authored component that stores the scene-id dictionary and exposes a stable serialized type id plus cooked payload version.
- Create: `engine/helengine.core/scene/runtime/SceneMapService.cs`
  Runtime service that scans loaded scene roots, enforces the singleton rule, and maps requested ids.
- Create: `engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs`
  Player-build deserializer for cooked `SceneMapComponent` payloads.
- Modify: `engine/helengine.core/Core.cs`
  Construct and expose the runtime `SceneMapService`.
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  Register the new runtime deserializer.
- Modify: `engine/helengine.core/components/2d/menu/DemoDiscReturnToMenuRuntimeComponent.cs`
  Replace the hardcoded platform menu resolver call with `SceneMapService`.
- Delete: `engine/helengine.core/content/PlatformMenuSceneResolver.cs`
  Remove the hardcoded platform-specific scene mapping seam after the runtime caller migrates.

### Editor persistence and inspector authoring

- Create: `engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs`
  Editor descriptor that writes and reads the authored dictionary payload and accepts cooked runtime payload shape.
- Create: `engine/helengine.editor/components/ui/SceneMapPropertyEditorProvider.cs`
  Custom reflected-inspector editor descriptor for the dictionary property.
- Modify: `engine/helengine.editor/EditorSession.cs`
  Register the new persistence descriptor in the component persistence registry.
- Modify: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
  Register the custom property editor provider.
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  Render and edit the scene-map dictionary rows, including add/remove entry actions and mutation tracking.

### Tests

- Create: `engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs`
  Descriptor round-trip and cooked-runtime compatibility tests.
- Create: `engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs`
  Runtime singleton, fallback, and mapping behavior tests.
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
  Inspector rendering and editing tests for the scene-map custom section.
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Cooked runtime scene-load test for `SceneMapComponent` deserialization if needed.
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`
  Remove or replace assertions that reference the old hardcoded resolver behavior.

### Validation commands

- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapServiceTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CityRenderingSceneAuthoringTests"`
- `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests|FullyQualifiedName~SceneMapServiceTests|FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CityRenderingSceneAuthoringTests"`

## Task 1: Add the Runtime Scene Map Contract

**Files:**
- Create: `engine/helengine.core/components/SceneMapComponent.cs`
- Create: `engine/helengine.core/scene/runtime/SceneMapService.cs`
- Modify: `engine/helengine.core/Core.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs`

- [ ] **Step 1: Write the failing runtime service tests**

Add `SceneMapServiceTests` covering these exact behaviors:

1. `MapSceneId_WhenNoSceneMapComponentIsLoaded_ReturnsOriginalSceneId`
2. `MapSceneId_WhenMappingExists_ReturnsMappedSceneId`
3. `MapSceneId_WhenMappingIsMissing_ReturnsOriginalSceneId`
4. `MapSceneId_WhenMultipleSceneMapComponentsAreLoaded_ThrowsInvalidOperationException`
5. `MapSceneId_WhenSceneIdIsEmpty_ThrowsArgumentException`

Use the same temporary content root and `Core` bootstrapping patterns already used in [SceneManagerTests.cs](C:/dev/helworks/helengine/engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs).

- [ ] **Step 2: Run the new test file and verify it fails for the missing runtime contract**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapServiceTests"
```

Expected: FAIL because `SceneMapService` and `SceneMapComponent` do not exist yet.

- [ ] **Step 3: Write the minimal runtime contract**

Implement:

1. `SceneMapComponent` with:
   - `public const byte CurrentVersion`
   - `public const string SerializedComponentTypeId`
   - a public dictionary property for the mappings
   - deterministic empty-dictionary initialization
2. `SceneMapService` with:
   - constructor dependency on `SceneManager`
   - `MapSceneId(string sceneId)` behavior from the spec
   - recursive traversal over loaded scene roots and child entities
   - singleton enforcement across all loaded scenes
3. `Core` initialization that creates and exposes the service.

- [ ] **Step 4: Run the runtime service tests and verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapServiceTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/SceneMapComponent.cs engine/helengine.core/scene/runtime/SceneMapService.cs engine/helengine.core/Core.cs engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs
git commit -m "Add runtime scene map service"
```

## Task 2: Persist and Deserialize SceneMapComponent

**Files:**
- Create: `engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs`
- Create: `engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write the failing persistence tests**

Add `SceneMapComponentPersistenceDescriptorTests` covering:

1. `SerializeComponent_WhenMappingsExist_WritesDictionaryEntries`
2. `DeserializeComponent_WhenPayloadUsesTaggedEditorLayout_LoadsMappings`
3. `DeserializeComponent_WhenPayloadUsesCookedRuntimeLayout_LoadsMappings`
4. `DeserializeComponent_WhenPayloadVersionIsUnsupported_ThrowsInvalidOperationException`

If `RuntimeSceneLoadServiceTests` is the cleaner seam for the cooked path, add:

1. `Load_WhenSceneContainsSceneMapComponent_MaterializesRuntimeComponent`

- [ ] **Step 2: Run the persistence/deserializer tests and verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: FAIL because the descriptor and runtime deserializer are missing.

- [ ] **Step 3: Implement the descriptor and runtime deserializer**

Implement:

1. `SceneMapComponentPersistenceDescriptor` using the tagged editor payload pattern already used by [MenuComponentPersistenceDescriptor.cs](C:/dev/helworks/helengine/engine/helengine.editor/serialization/scene/MenuComponentPersistenceDescriptor.cs)
2. cooked runtime payload compatibility in the descriptor so authored scene loads can still accept packaged payload shape
3. `RuntimeSceneMapComponentDeserializer` that reconstructs the dictionary from the cooked payload
4. registry registration in both `EditorSession.CreateComponentPersistenceRegistry(...)` and `RuntimeComponentRegistry.CreateDefault()`

Use a deterministic serialized entry order so authored payloads and tests stay stable.

- [ ] **Step 4: Run the persistence/deserializer tests and verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/serialization/scene/SceneMapComponentPersistenceDescriptor.cs engine/helengine.core/scene/runtime/RuntimeSceneMapComponentDeserializer.cs engine/helengine.editor/EditorSession.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor.tests/serialization/scene/SceneMapComponentPersistenceDescriptorTests.cs engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs
git commit -m "Persist and deserialize scene map components"
```

## Task 3: Expose SceneMapComponent in the Editor Inspector

**Files:**
- Create: `engine/helengine.editor/components/ui/SceneMapPropertyEditorProvider.cs`
- Modify: `engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`

- [ ] **Step 1: Write the failing inspector tests**

Add targeted tests to `ComponentPropertiesViewDynamicInspectorTests` for:

1. `ShowComponents_WhenInspectingSceneMapComponent_RendersSceneMapCustomSection`
2. `ShowComponents_WhenSceneMapSectionIsExpanded_RendersExistingMappingRows`
3. `EditSceneMapEntry_WhenValueChanges_UpdatesComponentAndMarksSceneMutated`
4. `AddSceneMapEntry_WhenConfirmed_AddsDictionaryEntry`
5. `RemoveSceneMapEntry_WhenPressed_RemovesDictionaryEntry`

Keep the tests focused on rendered labels, entry counts, and mutation effects rather than pixel layout.

- [ ] **Step 2: Run the inspector tests and verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"
```

Expected: FAIL because the reflected inspector does not currently support dictionary editing.

- [ ] **Step 3: Implement the custom inspector path**

Implement:

1. `SceneMapPropertyEditorProvider` that recognizes the scene-map dictionary property and exposes a custom editor type id
2. provider registration in `ReflectedComponentPropertyDescriptorBuilder`
3. `ComponentPropertiesView` support for that custom editor type:
   - collapsed/expanded section behavior
   - one editable key/value row per dictionary entry
   - add/remove controls
   - synchronization from component state into the row UI
   - mutation tracking through `EditorSceneMutationService`

Do not add generic dictionary support to the whole inspector. Keep the implementation specific to the scene-map property editor.

- [ ] **Step 4: Run the inspector tests and verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/SceneMapPropertyEditorProvider.cs engine/helengine.editor/components/ui/ReflectedComponentPropertyDescriptorBuilder.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs
git commit -m "Add scene map inspector editing"
```

## Task 4: Migrate the Return-to-Menu Flow and Remove the Hardcoded Resolver

**Files:**
- Modify: `engine/helengine.core/components/2d/menu/DemoDiscReturnToMenuRuntimeComponent.cs`
- Delete: `engine/helengine.core/content/PlatformMenuSceneResolver.cs`
- Modify: `engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs`

- [ ] **Step 1: Write the failing integration tests**

Extend `SceneMapServiceTests` or add a small focused runtime test covering:

1. `Update_WhenReturnToMenuIsTriggeredAndMappingExists_LoadsMappedSceneId`
2. `Update_WhenReturnToMenuIsTriggeredAndNoMappingExists_LoadsOriginalSceneId`

Use a test seam that can observe which scene id `DemoDiscReturnToMenuRuntimeComponent` passes into `SceneManager.LoadScene(...)`.

- [ ] **Step 2: Run the integration tests and verify they fail**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapServiceTests|FullyQualifiedName~CityRenderingSceneAuthoringTests"
```

Expected: FAIL because the runtime component still calls `PlatformMenuSceneResolver`.

- [ ] **Step 3: Migrate the runtime caller and remove the obsolete file**

Implement:

1. `DemoDiscReturnToMenuRuntimeComponent` now starts from its logical target scene id and passes it through `Core.Instance.SceneMapService.MapSceneId(...)`
2. delete `PlatformMenuSceneResolver.cs`
3. replace any source-assert tests that hardcode the old resolver usage with assertions that match the new service-based flow

- [ ] **Step 4: Run the integration tests and verify they pass**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapServiceTests|FullyQualifiedName~CityRenderingSceneAuthoringTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.core/components/2d/menu/DemoDiscReturnToMenuRuntimeComponent.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs engine/helengine.editor.tests/serialization/scene/SceneMapServiceTests.cs
git rm engine/helengine.core/content/PlatformMenuSceneResolver.cs
git commit -m "Replace hardcoded menu scene resolver with scene map service"
```

## Task 5: Run Focused Verification and Capture Remaining Risk

**Files:**
- Verify only; no planned code changes

- [ ] **Step 1: Run the focused feature suite**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneMapComponentPersistenceDescriptorTests|FullyQualifiedName~SceneMapServiceTests|FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~RuntimeSceneLoadServiceTests|FullyQualifiedName~CityRenderingSceneAuthoringTests"
```

Expected: PASS.

- [ ] **Step 2: Run a narrow regression slice around scene persistence**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneManagerTests"
```

Expected: PASS.

- [ ] **Step 3: Review the diff for accidental scope growth**

Check:

1. `SceneManager` remains untouched for automatic remapping behavior
2. `SceneMapComponent` stays platform-agnostic
3. no generic dictionary editor support leaked into unrelated inspector properties

- [ ] **Step 4: Commit any final verification-only adjustments**

```bash
git add -A
git commit -m "Finalize scene map component implementation"
```

- [ ] **Step 5: Record residual risk in the completion summary**

Call out:

1. scene-map lookup currently traverses loaded scene roots on demand
2. duplicate singleton detection is runtime-only
3. city authoring of the persistent scene-map scene is intentionally out of scope for this implementation

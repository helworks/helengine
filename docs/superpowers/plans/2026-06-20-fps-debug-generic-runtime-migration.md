# FPS And Debug Generic Runtime Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `FPSComponent` and `DebugComponent` from bespoke packaged runtime deserializers and packaging rewrite paths to the shared automatic runtime component system.

**Architecture:** Remove the explicit runtime and packaging special-cases for `FPSComponent` and `DebugComponent`. Package them through the shared automatic runtime payload builder, let managed runtime loading use `AutomaticScriptComponentRuntimeDeserializer`, and let native player builds use generated runtime component deserializers for both built-ins.

**Tech Stack:** C#, xUnit, shared scene persistence/runtime loading, generated native C++ deserializer emission, `rtk dotnet test`

---

## File Structure

### Existing files to modify

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  Remove explicit built-in runtime registrations for `RuntimeFPSComponentDeserializer` and `RuntimeDebugComponentDeserializer`.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  Remove the FPS/debug-specific rewrite branches and helpers so both components package through the automatic runtime payload path.
- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  Flip expectations so generated runtime deserializers are emitted for `FPSComponent` and `DebugComponent`.
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Rewrite the packaged runtime tests for FPS and debug to use automatic runtime payloads and automatic type ids.
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
  Update packaged scene assertions so FPS and debug use automatic type ids and automatic runtime payload versions.

### Existing files to delete

- `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs`

## Task 1: Add failing generated-runtime coverage for FPS and Debug

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing generated-runtime registration assertions**

Update the generated-runtime overlap test so it expects generated runtime deserializer emission for:

- `GeneratedRuntimeFPSComponentDeserializer`
- `GeneratedRuntimeDebugComponentDeserializer`

Also expect the corresponding generated `.cpp` files to exist.

- [ ] **Step 2: Run the focused regeneration suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
```

Expected: FAIL because both components are still excluded as explicit built-ins.

## Task 2: Add failing managed runtime load coverage for automatic FPS and Debug payloads

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Rewrite the packaged runtime FPS and debug tests to use automatic payloads**

Convert the existing runtime-load coverage for:

- FPS overlay
- FPS overlay without font
- debug overlay
- debug overlay without font

so those `SceneComponentAssetRecord` instances use:

- `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(...))`
- `WriteAutomaticRuntimeComponentPayload(...)`

Use explicit `EntityComponentSaveState` font references for the non-null-font cases.

- [ ] **Step 2: Run the focused runtime load suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
```

Expected: FAIL because the default runtime registry still routes these type ids to the bespoke runtime deserializers that expect the old strict FPS/debug payload layouts.

## Task 3: Add failing packaging assertions for FPS and Debug

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Tighten packaged-scene assertions for FPS and debug**

Update the existing packager tests so they assert:

- packaged FPS and debug component records use automatic component type ids
- packaged payloads start with `AutomaticScriptComponentRuntimeDeserializer.CurrentVersion`
- packaged font references still rewrite to their correct runtime/cooked paths through the automatic payload path

- [ ] **Step 2: Run the focused packager tests to verify the relevant assertions fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Package_WhenSceneContainsDebugComponent_LeavesPackagedComponentLoadable -v minimal
```

Expected: FAIL because packaging still rewrites those components into bespoke runtime payload shapes.

## Task 4: Remove explicit runtime deserializers and packaging rewrite branches

**Files:**
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Delete:
  - `engine/helengine.core/scene/runtime/RuntimeFPSComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeDebugComponentDeserializer.cs`

- [ ] **Step 1: Remove the two built-in runtime registrations from the runtime registry**

After this change, managed runtime fallback should resolve these component type ids through the automatic runtime path.

- [ ] **Step 2: Remove the two packaging rewrite branches**

Delete the `TryTransform` branches that call:

- `RewriteFPSComponentRecord`
- `RewriteDebugComponentRecord`

- [ ] **Step 3: Delete the now-dead packaging rewrite helper methods**

Delete the FPS/debug rewrite helpers and any dead payload readers that only existed to support those bespoke runtime formats.

- [ ] **Step 4: Delete the two bespoke runtime deserializer files**

These files should be removed completely once nothing references them.

## Task 5: Verify green and clean up any targeted assertions

**Files:**
- Modify as needed:
  - `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  - `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  - `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Run the generated-runtime suite again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
```

Expected: PASS.

- [ ] **Step 2: Run the runtime load suite again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
```

Expected: PASS.

- [ ] **Step 3: Run the focused packager tests again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~Package_WhenSceneContainsDebugComponent_LeavesPackagedComponentLoadable -v minimal
```

Expected: PASS.

## Task 6: Final focused verification slice

**Files:**
- No code changes expected.

- [ ] **Step 1: Run the final focused regression slice**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --filter FullyQualifiedName~Package_WhenSceneContainsFpsOverlay_LeavesPackagedComponentLoadable -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-build --filter FullyQualifiedName~Package_WhenSceneContainsDebugComponent_LeavesPackagedComponentLoadable -v minimal
```

Expected: PASS for all focused checks.

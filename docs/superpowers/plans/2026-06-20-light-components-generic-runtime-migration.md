# Light Components Generic Runtime Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the built-in light components from bespoke packaged runtime deserializers and packaging rewrite paths to the shared automatic runtime component system.

**Architecture:** Remove the explicit runtime and packaging special-cases for `AmbientLightComponent`, `DirectionalLightComponent`, `PointLightComponent`, and `SpotLightComponent`. Package them through the existing automatic runtime payload builder, let managed runtime loading use `AutomaticScriptComponentRuntimeDeserializer`, and let native player builds use generated runtime component deserializers.

**Tech Stack:** C#, xUnit, shared scene persistence/runtime loading, generated native C++ deserializer emission, `rtk dotnet test`

---

## File Structure

### Existing files to modify

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  Remove explicit built-in registration for the four light runtime deserializers.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  Remove the light-specific rewrite branches and let the automatic runtime packaging path own these four components.
- `engine/helengine.editor/managers/project/EditorGeneratedCoreRegenerationService.cs`
  No direct logic change may be needed, but verify built-in overlap filtering now allows generated light deserializers.
- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  Flip expectations so generated runtime deserializers are emitted for the four light components.
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
  Add or update packaging assertions so packaged light records use automatic component type ids and automatic runtime payloads.
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Add managed runtime load coverage for packaged light records using the automatic runtime payload shape.

### Existing files to delete

- `engine/helengine.core/scene/runtime/RuntimeDirectionalLightComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimePointLightComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimeSpotLightComponentDeserializer.cs`

## Task 1: Add failing generated-runtime coverage for light deserializers

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing generated-runtime registration assertions**

Update the explicit-runtime-overlap test so it expects generated runtime deserializer emission for:

- `GeneratedRuntimeAmbientLightComponentDeserializer`
- `GeneratedRuntimeDirectionalLightComponentDeserializer`
- `GeneratedRuntimePointLightComponentDeserializer`
- `GeneratedRuntimeSpotLightComponentDeserializer`

Also expect the corresponding generated `.cpp` files to exist.

- [ ] **Step 2: Run the focused regeneration test to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
```

Expected: FAIL because the four light components are still excluded as explicit built-ins.

## Task 2: Add failing packaged-runtime managed-load tests for lights

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Write failing automatic-runtime light load tests**

Add focused tests for packaged automatic runtime payloads covering:

- one ambient light
- one directional light
- one point light
- one spot light

Each test should:

- build a `SceneComponentAssetRecord` using `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(...))`
- write the shared automatic runtime payload format
- load the scene through `RuntimeSceneLoadService`
- assert the restored authored light values

- [ ] **Step 2: Run the focused runtime load suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
```

Expected: FAIL because the runtime registry still routes these components through the bespoke light deserializers that expect the old light payload format.

## Task 3: Add failing packaging tests for automatic light payloads

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Write failing packaging assertions for one representative light**

Add or update one packaged-scene test for a light component, preferably `DirectionalLightComponent`, that asserts:

- packaged record type id equals `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(DirectionalLightComponent))`
- packaged payload starts with `AutomaticScriptComponentRuntimeDeserializer.CurrentVersion`
- the packaged record can be loaded with `AutomaticScriptComponentRuntimeDeserializer`

- [ ] **Step 2: Run the focused packager suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v minimal
```

Expected: FAIL because packaging still rewrites directional lights into the bespoke light payload format and keeps the old component type id.

## Task 4: Remove explicit light runtime deserializers and packaging special-cases

**Files:**
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Delete:
  - `engine/helengine.core/scene/runtime/RuntimeDirectionalLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeAmbientLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimePointLightComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeSpotLightComponentDeserializer.cs`

- [ ] **Step 1: Remove the four built-in light registrations from the runtime registry**

After this change, managed runtime fallback should resolve these component type ids through the automatic runtime path.

- [ ] **Step 2: Remove the four light rewrite branches from packaging**

Delete the `TryTransform` branches that call:

- `RewriteDirectionalLightComponentRecord`
- `RewriteAmbientLightComponentRecord`
- `RewritePointLightComponentRecord`
- `RewriteSpotLightComponentRecord`

- [ ] **Step 3: Delete the bespoke light rewrite helper methods**

Delete the four light rewrite methods from `SceneComponentPackagingTransformService.cs`.

- [ ] **Step 4: Delete the four bespoke light runtime deserializer files**

These files should be removed completely once nothing references them.

## Task 5: Verify green and clean up dependent expectations

**Files:**
- Modify as needed:
  - `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  - `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
  - `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Run the generated-runtime test again**

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

- [ ] **Step 3: Run the packager suite again**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v minimal
```

Expected: PASS.

## Task 6: Final focused verification slice

**Files:**
- No code changes expected.

- [ ] **Step 1: Run the three focused suites as final confirmation**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v minimal
```

Expected: PASS for all three focused suites.

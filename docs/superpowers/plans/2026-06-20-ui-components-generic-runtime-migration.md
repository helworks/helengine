# UI Components Generic Runtime Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move `RoundedRectComponent`, `SpriteComponent`, and `TextComponent` from bespoke packaged runtime deserializers and packaging rewrite paths to the shared automatic runtime component system.

**Architecture:** Remove the explicit runtime and packaging special-cases for the three UI components. Package them through the shared automatic runtime payload builder, let managed runtime loading use `AutomaticScriptComponentRuntimeDeserializer`, and let native player builds use generated runtime component deserializers for the same three built-ins.

**Tech Stack:** C#, xUnit, shared scene persistence/runtime loading, generated native C++ deserializer emission, `rtk dotnet test`

---

## File Structure

### Existing files to modify

- `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
  Remove explicit built-in runtime registrations for `RuntimeTextComponentDeserializer`, `RuntimeSpriteComponentDeserializer`, and `RuntimeRoundedRectComponentDeserializer`.
- `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
  Remove the UI-specific rewrite branches and helpers for text, sprite, and rounded rectangle so they package through the automatic runtime payload path.
- `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
  Flip expectations so generated runtime deserializers are emitted for `TextComponent`, `SpriteComponent`, and `RoundedRectComponent`.
- `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`
  Rewrite existing packaged runtime tests for text, sprite, and rounded rectangle to use automatic runtime payloads and automatic type ids.
- `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
  Update packaged scene assertions so the three UI components use automatic type ids and automatic runtime payload versions.

### Existing files to delete

- `engine/helengine.core/scene/runtime/RuntimeTextComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimeSpriteComponentDeserializer.cs`
- `engine/helengine.core/scene/runtime/RuntimeRoundedRectComponentDeserializer.cs`

## Task 1: Add failing generated-runtime coverage for the UI batch

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`

- [ ] **Step 1: Write the failing generated-runtime registration assertions**

Update the explicit-runtime-overlap test so it expects generated runtime deserializer emission for:

- `GeneratedRuntimeTextComponentDeserializer`
- `GeneratedRuntimeSpriteComponentDeserializer`
- `GeneratedRuntimeRoundedRectComponentDeserializer`

Also expect the corresponding generated `.cpp` files to exist.

- [ ] **Step 2: Run the focused regeneration suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests -v minimal
```

Expected: FAIL because the three UI components are still excluded as explicit built-ins.

## Task 2: Add failing managed runtime load coverage for automatic UI payloads

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/RuntimeSceneLoadServiceTests.cs`

- [ ] **Step 1: Rewrite the packaged runtime UI tests to use automatic payloads**

Convert existing packaged runtime tests for:

- text
- sprite
- rounded rectangle

so they build `SceneComponentAssetRecord` values using:

- `AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(...))`
- `WriteAutomaticRuntimeComponentPayload(...)`

For text and sprite, continue using the existing shared asset-reference save-state machinery rather than hand-authored runtime payload helpers.

- [ ] **Step 2: Run the focused runtime load suite to verify it fails**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~RuntimeSceneLoadServiceTests -v minimal
```

Expected: FAIL because the default runtime registry still routes these type ids to the bespoke runtime deserializers that expect the old strict UI payload formats.

## Task 3: Add failing packaging assertions for the UI batch

**Files:**
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Tighten packaged-scene assertions for text, sprite, and rounded rectangle**

Update the existing packaging tests so they assert:

- packaged text, sprite, and rounded-rectangle component records use automatic component type ids
- packaged payloads start with `AutomaticScriptComponentRuntimeDeserializer.CurrentVersion`

- [ ] **Step 2: Run the focused packager suite to verify the relevant assertions fail**

Run:

```bash
rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter FullyQualifiedName~EditorWindowsBuildScenePackagerTests -v minimal
```

Expected: FAIL because packaging still rewrites those components into bespoke runtime payload shapes.

## Task 4: Remove explicit runtime deserializers and packaging rewrite branches

**Files:**
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Delete:
  - `engine/helengine.core/scene/runtime/RuntimeTextComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeSpriteComponentDeserializer.cs`
  - `engine/helengine.core/scene/runtime/RuntimeRoundedRectComponentDeserializer.cs`

- [ ] **Step 1: Remove the three built-in runtime registrations from the runtime registry**

After this change, managed runtime fallback should resolve these component type ids through the automatic runtime path.

- [ ] **Step 2: Remove the three packaging rewrite branches**

Delete the `TryTransform` branches that call:

- `RewriteTextComponentRecord`
- `RewriteSpriteComponentRecord`
- `RewriteRoundedRectComponentRecord`

- [ ] **Step 3: Delete the now-dead packaging rewrite helper methods**

Delete the three UI rewrite methods from `SceneComponentPackagingTransformService.cs`.

- [ ] **Step 4: Delete the three bespoke runtime deserializer files**

These files should be removed completely once nothing references them.

## Task 5: Verify green and clean up test helpers

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

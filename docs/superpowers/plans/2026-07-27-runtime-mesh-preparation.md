# Runtime Mesh Preparation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Allow enabled MeshComponent tessellation and scale baking to execute either during packaging or when the owning scene loads.

**Architecture:** Persist two independent execution-time booleans in existing per-platform MeshComponent settings. Packaging creates variants only for cook-time requests; runtime scene resolution retains raw model data long enough to clone, prepare, build, own, and release a private runtime model for each load-time request.

**Tech Stack:** C#, helengine editor serializer, core scene runtime, generated C++ platform runtimes, xUnit.

---

### Task 1: Persist the execution-time settings

**Files:**
- Modify: `engine/helengine.editor/managers/scene/MeshComponentTessellationSettings.cs`
- Modify: `engine/helengine.editor/managers/scene/MeshComponentTessellationSettingsService.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/MeshComponentTessellationSettingsServiceTests.cs`

- [ ] **Step 1: Write failing persistence tests**

Add tests proving absent detached members resolve to `true`, and that an explicit false round-trips:

```csharp
MeshComponentTessellationSettings settings = new MeshComponentTessellationSettings(
    true, 0.5d, true, false, true);
service.SetForPlatform(saveState, "psp", settings);
MeshComponentTessellationSettings restored = service.GetForPlatform(saveState, "psp");
Assert.False(restored.TessellateAtCookTime);
Assert.True(restored.BakeScaleAtCookTime);
```

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~MeshComponentTessellationSettingsServiceTests`

Expected: compile failure because the settings constructor and properties do not exist.

- [ ] **Step 3: Add the immutable settings fields and detached members**

Add `TessellateAtCookTime` and `BakeScaleAtCookTime` to `MeshComponentTessellationSettings`; preserve old constructor overloads by forwarding them with both values true. Add stable member names `MeshTessellateAtCookTime` and `MeshBakeScaleAtCookTime`; write both and read missing values as true. Include both flags in `BuildVariantIdentity`.

- [ ] **Step 4: Run the focused tests and confirm they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~MeshComponentTessellationSettingsServiceTests`

Expected: all targeted tests pass.

- [ ] **Step 5: Commit the settings change**

```bash
rtk git add engine/helengine.editor/managers/scene/MeshComponentTessellationSettings.cs engine/helengine.editor/managers/scene/MeshComponentTessellationSettingsService.cs engine/helengine.editor.tests/managers/scene/MeshComponentTessellationSettingsServiceTests.cs
rtk git commit -m "feat: persist mesh preparation timing"
```

### Task 2: Gate package-time variants and retain load-time source data

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`
- Modify: `engine/helengine.core/assets/RuntimeModel.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs`
- Test: `engine/helengine.core.tests/scene/runtime/RuntimeSceneAssetReferenceResolverTests.cs`

- [ ] **Step 1: Write failing packaging and resolver tests**

Cover a tessellated-at-load component retaining its original model reference, a bake-at-load component retaining its original model reference, and a resolved runtime model exposing an independent raw `ModelAsset` copy suitable for preparation.

- [ ] **Step 2: Run the targeted tests and confirm they fail**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~SceneComponentPackagingTransformServiceTests; rtk dotnet test engine/helengine.core.tests/helengine.core.tests.csproj --no-restore --filter FullyQualifiedName~RuntimeSceneAssetReferenceResolverTests`

Expected: assertions fail because packaging always creates a variant and runtime models do not retain preparation data.

- [ ] **Step 3: Implement timing-aware packaging and raw-model retention**

In `ApplyMeshComponentTessellationVariant`, return unless at least one enabled operation has its matching `AtCookTime` flag true. Apply bake and tessellation independently according to those flags. Keep the original reference for exclusively load-time requests. Extend `RuntimeModel` with owned raw-model preparation data and arrange `RuntimeSceneAssetReferenceResolver.ResolveModel` to deserialize a private `ModelAsset` copy before calling `BuildModelFromRaw`.

- [ ] **Step 4: Run the targeted tests and confirm they pass**

Run the commands from Step 2.

Expected: targeted editor and core tests pass.

- [ ] **Step 5: Commit the packaging foundation**

```bash
rtk git add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs engine/helengine.core/assets/RuntimeModel.cs engine/helengine.core/scene/runtime/RuntimeSceneAssetReferenceResolver.cs engine/helengine.core.tests/scene/runtime/RuntimeSceneAssetReferenceResolverTests.cs
rtk git commit -m "feat: retain load-time mesh preparation inputs"
```

### Task 3: Prepare private mesh models during scene loading

**Files:**
- Create: `engine/helengine.core/scene/runtime/RuntimeMeshPreparationService.cs`
- Modify: `engine/helengine.core/scene/runtime/SceneManager.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeSceneOwnedAssetSet.cs`
- Modify: `engine/helengine.core/components/3d/MeshComponent.cs`
- Test: `engine/helengine.core.tests/scene/runtime/RuntimeMeshPreparationServiceTests.cs`

- [ ] **Step 1: Write failing runtime-preparation tests**

Test two MeshComponents resolved from one source model with different scale. Assert their prepared models are distinct, the source raw model remains unchanged, bake runs before tessellation, and the created runtime models are listed in the scene-owned asset set.

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run: `rtk dotnet test engine/helengine.core.tests/helengine.core.tests.csproj --no-restore --filter FullyQualifiedName~RuntimeMeshPreparationServiceTests`

Expected: compile failure because the service is absent.

- [ ] **Step 3: Implement the service and scene-load integration**

Implement `RuntimeMeshPreparationService.Prepare(Entity root, RuntimeSceneOwnedAssetSet ownedAssets)`. For each MeshComponent with enabled load-time flags, clone its retained raw model, validate finite non-zero world scale, apply bake before tessellation, build a replacement through `Core.Instance.RenderManager3D.BuildModelFromRaw`, assign it to the component before render registration, and append it to owned models for release on scene unload. Clear consumed raw preparation data after successful replacement.

- [ ] **Step 4: Run the focused tests and confirm they pass**

Run the command from Step 2.

Expected: all runtime-preparation tests pass.

- [ ] **Step 5: Commit scene-load preparation**

```bash
rtk git add engine/helengine.core/scene/runtime/RuntimeMeshPreparationService.cs engine/helengine.core/scene/runtime/SceneManager.cs engine/helengine.core/scene/runtime/RuntimeSceneOwnedAssetSet.cs engine/helengine.core/components/3d/MeshComponent.cs engine/helengine.core.tests/scene/runtime/RuntimeMeshPreparationServiceTests.cs
rtk git commit -m "feat: prepare meshes during scene loading"
```

### Task 4: Carry flags through code generation and verify PSP scale behavior

**Files:**
- Modify: `builder/PspPlatformDefinitionFactory.cs`
- Modify: `src/platform/psp/rendering/PspRenderManager3D.cpp`
- Modify: `builder.tests/PspRenderManager3DSourceTests.cs`
- Modify: `engine/helengine.editor.tests/ModelTessellationProcessorTests.cs`

- [ ] **Step 1: Write failing source and geometry tests**

Assert PSP declares both timing members and that a load-time bake produces a no-scale world matrix exactly like a package-time baked model. Add geometry tests for bake-then-tessellate ordering with non-uniform scale.

- [ ] **Step 2: Run the focused tests and confirm they fail**

Run: `rtk dotnet test builder.tests/helengine.psp.builder.tests.csproj --no-restore --filter FullyQualifiedName~PspRenderManager3DSourceTests; rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~ModelTessellationProcessorTests`

Expected: source assertions fail until the two serialized members are declared and consumed.

- [ ] **Step 3: Implement platform declarations and no-double-scale behavior**

Declare the two booleans on PSP MeshComponent metadata. Make the PSP renderer use the prepared/baked marker after runtime preparation exactly as it does for a cook-time baked model, so it never applies non-uniform scale a second time.

- [ ] **Step 4: Run focused tests and a PSP build**

Run: `rtk dotnet test builder.tests/helengine.psp.builder.tests.csproj --no-restore --filter FullyQualifiedName~PspRenderManager3DSourceTests; rtk dotnet build builder/helengine.psp.builder.csproj --no-restore -c Debug`

Then run: `rtk powershell -ExecutionPolicy Bypass -File C:\dev\helworks\helengine\scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform psp -Output C:\dev\helprojs\output\psp-runtime-mesh-preparation`

Expected: tests pass and `PSP/GAME/HELENGINE/EBOOT.PBP` exists.

- [ ] **Step 5: Commit platform support**

```bash
rtk git add builder/PspPlatformDefinitionFactory.cs src/platform/psp/rendering/PspRenderManager3D.cpp builder.tests/PspRenderManager3DSourceTests.cs C:\dev\helworks\helengine\engine\helengine.editor.tests\ModelTessellationProcessorTests.cs
rtk git commit -m "feat: support load-time PSP mesh preparation"
```

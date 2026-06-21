# Camera Component Generic Persistence Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `CameraComponent` use the generic reflected scene persistence and packaged runtime loading pipeline without regressing editor suppression or packaged scene behavior.

**Architecture:** The refactor keeps authored camera state on `CameraComponent`, removes runtime-only camera members from the persistence surface, and moves editor suppression away from destructive mutation of persisted fields. Once camera becomes a normal authored component, the editor save/load path, packaging path, and runtime load path can all fall through the existing automatic component persistence infrastructure.

**Tech Stack:** C# / .NET 9, xUnit, existing helengine reflected scene persistence, generated runtime deserializer codegen, editor scene save/load services.

---

## Task 1: Lock down the current camera persistence and suppression behavior with tests

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`

- [ ] **Step 1: Add a failing save test that proves authored camera values must survive editor suppression**

Write a test in `SceneSaveServiceTests.cs` that:

- creates an `EditorEntity`
- attaches a `CameraComponent`
- assigns non-default authored values for `LayerMask`, `Viewport`, `NearPlaneDistance`, `FarPlaneDistance`, `ClearSettings`, and `RenderSettings`
- calls `EditorSceneCameraSuppressionService.AttachAndSuppress(entity)`
- saves the scene
- asserts that the serialized camera payload contains the authored values rather than the suppressed runtime values

Use a value set where `LayerMask` is non-zero and `ClearSettings.ClearColorEnabled` is `true` so the test fails if suppression still writes inert values.

- [ ] **Step 2: Add a failing save test that proves `RenderTarget` must not be serialized**

Write a test in `SceneSaveServiceTests.cs` that:

- creates a `CameraComponent`
- assigns a test `RenderTarget`
- saves the scene through the camera component persistence path
- asserts that save succeeds without requiring a `RenderTarget` asset reference

The test should fail if the automatic serializer tries to persist `RenderTarget`.

- [ ] **Step 3: Add a failing load test for generic camera scene payloads**

Write a test in `SceneFileLoadServiceTests.cs` that:

- builds a camera `SceneComponentAssetRecord` using the generic reflected payload shape
- loads the scene through the normal editor scene load service
- asserts that the loaded `CameraComponent` restores all authored values

The test should not register `CameraComponentPersistenceDescriptor`.

- [ ] **Step 4: Add a failing codegen/runtime registration test for camera generic deserializers**

Write or extend a test in `EditorGeneratedCoreRegenerationServiceTests.cs` that:

- builds generated runtime component deserializer output for a project containing a camera component
- asserts that generated registration contains camera deserializer registration through the generic/component schema path once the explicit runtime camera deserializer is removed

- [ ] **Step 5: Add a failing editor-scene-creation test that cameras remain suppressed without destructive persistence hacks**

Write or update a test in `EditorSceneCreationServiceTests.cs` that:

- creates a scene camera through the editor scene creation flow
- verifies suppression is attached
- verifies authored camera values remain on the `CameraComponent`
- verifies the editor still treats the camera as suppressed

- [ ] **Step 6: Run the focused camera tests to capture the baseline failures**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~EditorSceneCreationServiceTests" 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- new camera tests fail
- existing unrelated tests remain unaffected

- [ ] **Step 7: Commit the test-only baseline**

```bash
git add engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs
git commit -m "test: lock down camera generic persistence behavior"
```

## Task 2: Remove runtime-only camera members from the generic persistence surface

**Files:**
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Modify: `engine/helengine.core/scene/runtime/AutomaticComponentAssetReferenceSupport.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Mark `RenderTarget` as scene-persistence ignored**

Add `[ScenePersistenceIgnore]` to `CameraComponent.RenderTarget` in `CameraComponent.cs`.

- [ ] **Step 2: Verify no other writable camera members leak runtime-only state**

Inspect `CameraComponent` public writable members and confirm the remaining persisted surface is limited to authored state:

- `CameraDrawOrder`
- `LayerMask`
- `Viewport`
- `NearPlaneDistance`
- `FarPlaneDistance`
- `ClearSettings`
- `RenderSettings`

Do not expand `AutomaticComponentAssetReferenceSupport` to include `RenderTarget`.

- [ ] **Step 3: Run the focused save test**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests" 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- `RenderTarget` persistence test passes
- suppression/authored-value tests still fail until the next task

- [ ] **Step 4: Commit the persistence-surface change**

```bash
git add engine/helengine.core/components/CameraComponent.cs
git commit -m "refactor: exclude camera render target from scene persistence"
```

## Task 3: Make editor camera suppression non-destructive and stop proxying authored values

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorSceneCameraSuppressionService.cs`
- Modify: `engine/helengine.editor/components/EditorSceneCameraSuppressionComponent.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Test: `engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`

- [ ] **Step 1: Remove authored-value storage responsibility from suppression metadata**

Refactor `EditorSceneCameraSuppressionComponent` so it no longer stores authored camera values used for save/load and property editing.

If the component is only needed as a suppression marker after this refactor, shrink it to that role instead of keeping dead fields.

- [ ] **Step 2: Remove suppression property proxying from the properties panel**

Delete or bypass camera-specific proxy reads/writes in `ComponentPropertiesView.cs` so camera edits go directly to `CameraComponent`.

Specifically remove the path that calls `EditorSceneCameraSuppressionService.TryGetAuthoredPropertyValue` and `TrySetAuthoredPropertyValue` for normal camera property persistence.

- [ ] **Step 3: Stop mutating persisted camera fields during suppression**

Update `EditorSceneCameraSuppressionService.AttachAndSuppress` and related helpers so suppression no longer overwrites:

- `LayerMask`
- `ClearSettings`

Keep editor suppression behavior, but enforce it through editor runtime behavior rather than persistence-visible field mutation.

- [ ] **Step 4: Update scene-load suppression attachment to preserve authored values**

Ensure `SceneLoadService` can still attach camera suppression metadata after a scene loads without replacing the just-loaded camera values.

- [ ] **Step 5: Run the editor creation and save/load camera tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSceneCreationServiceTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests" 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- authored-value save tests pass
- editor scene creation keeps cameras suppressed
- generic load test may still fail until explicit camera descriptor registration is removed

- [ ] **Step 6: Commit the suppression refactor**

```bash
git add engine/helengine.editor/managers/scene/EditorSceneCameraSuppressionService.cs engine/helengine.editor/components/EditorSceneCameraSuppressionComponent.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor/serialization/scene/SceneLoadService.cs engine/helengine.editor.tests/managers/scene/EditorSceneCreationServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs
git commit -m "refactor: make editor camera suppression non-destructive"
```

## Task 4: Remove the explicit editor camera persistence descriptor

**Files:**
- Delete: `engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Test: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`

- [ ] **Step 1: Remove camera descriptor registration from editor registries**

Delete `persistenceRegistry.Register(new CameraComponentPersistenceDescriptor())` from:

- `EditorSession.CreateComponentPersistenceRegistry`
- `ComponentPlatformEditingService.CreatePersistenceRegistry`
- any other editor-only registry construction path that still registers the descriptor

- [ ] **Step 2: Delete the dedicated camera descriptor file**

Delete `CameraComponentPersistenceDescriptor.cs` once no registration sites remain.

- [ ] **Step 3: Make sure camera now resolves through `AutomaticScriptComponentPersistenceDescriptor`**

Use the existing reflected schema path and confirm no camera-specific branch remains in the editor scene persistence registry for normal save/load.

- [ ] **Step 4: Run the focused scene save/load tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests" 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- camera save/load tests pass without `CameraComponentPersistenceDescriptor`

- [ ] **Step 5: Commit the editor generic-persistence cut**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs
git rm engine/helengine.editor/serialization/scene/CameraComponentPersistenceDescriptor.cs
git commit -m "refactor: route camera through generic scene persistence"
```

## Task 5: Remove the camera-specific packaging transform and runtime deserializer path

**Files:**
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs`
- Delete: `engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] **Step 1: Remove the camera-specific packaging transform branch**

In `SceneComponentPackagingTransformService.cs`:

- remove `CameraComponentTypeId` special handling from `CanTransform`
- remove `RewriteCameraComponentRecord`
- remove camera-specific tagged/binary camera payload readers that are only used by the deleted branch if nothing else references them

Let camera pass through `TryRewriteAutomaticComponentRecord`.

- [ ] **Step 2: Remove the explicit runtime camera deserializer registration**

Delete `registry.Register(new RuntimeCameraComponentDeserializer())` from `RuntimeComponentRegistry.RegisterBuiltInComponentDeserializers`.

- [ ] **Step 3: Delete the dedicated runtime camera deserializer**

Delete `RuntimeCameraComponentDeserializer.cs` once registration and references are removed.

- [ ] **Step 4: Verify generated runtime deserializer output still covers camera**

Run or update the codegen tests so the generated runtime registration includes camera deserialization through the generic/generated path on targets with `HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION`.

- [ ] **Step 5: Run the packaging/runtime-focused tests**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedCoreRegenerationServiceTests|FullyQualifiedName~SceneManagerTests" 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- generated core/runtime registration tests pass
- packaged scene camera runtime load tests pass without `RuntimeCameraComponentDeserializer`

- [ ] **Step 6: Commit the packaging/runtime cleanup**

```bash
git add engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.core/scene/runtime/RuntimeComponentRegistry.cs engine/helengine.editor.tests/managers/project/EditorGeneratedCoreRegenerationServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs
git rm engine/helengine.core/scene/runtime/RuntimeCameraComponentDeserializer.cs
git commit -m "refactor: remove camera-specific packaged runtime loading"
```

## Task 6: Run the smallest full verification set and document follow-up work

**Files:**
- Modify: `docs/superpowers/specs/2026-06-21-camera-component-generic-persistence-design.md`
- Modify: `docs/superpowers/plans/2026-06-21-camera-component-generic-persistence.md`

- [ ] **Step 1: Run the focused editor test project sweep**

Run:

```powershell
dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- the editor test project passes, or
- any remaining failures are pre-existing and called out explicitly in the docs/hand-off

- [ ] **Step 2: Run the smallest build verification for the affected engine projects**

Run:

```powershell
dotnet build .\engine\helengine.core\helengine.core.csproj 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
dotnet build .\engine\helengine.editor\helengine.editor.csproj 2>&1 | Out-String | % { $_.Substring(0,[Math]::Min($_.Length,4000)) }
```

Expected:

- both projects build successfully after camera path removal

- [ ] **Step 3: Update the design and plan docs with any migration notes discovered during implementation**

If camera legacy payload compatibility or reflection-disabled generation required an extra nuance, record it in both docs before closing the work.

- [ ] **Step 4: Commit the final verification/doc notes**

```bash
git add docs/superpowers/specs/2026-06-21-camera-component-generic-persistence-design.md docs/superpowers/plans/2026-06-21-camera-component-generic-persistence.md
git commit -m "docs: finalize camera generic persistence plan"
```

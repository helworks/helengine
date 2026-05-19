# Exact Editor 2D World Preview Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add exact editor-only world-space previews for authored `TextComponent` and `RoundedRectComponent` so they render in the 3D scene view at their real transform, while preserving the existing sprite path and keeping internal editor UI on the screen-space path.

**Architecture:** Extend the existing editor world-space 2D preview system instead of replacing it. `SpriteComponent` remains a direct textured-quad proxy, while `TextComponent` and `RoundedRectComponent` gain exact per-component render-target-backed preview proxies with dirty-driven refresh. Keep all routing, capture, and lifetime logic in `helengine.editor`; do not touch `helengine.core`.

**Tech Stack:** C#/.NET 9, HelEngine editor runtime, DirectX11 editor renderer, xUnit.

---

## File Structure

### Existing preview routing files

- Modify: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs`
- Modify: `engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`

### Existing preview proxy files

- Modify: `engine/helengine.editor/components/preview2d/EditorWorldSpace2DPreviewComponentBase.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorSpriteWorldPreviewComponent.cs` only if needed for shared base cleanup

### New exact preview support files

- Create: `engine/helengine.editor/components/preview2d/EditorExact2DWorldPreviewComponentBase.cs`
- Create: `engine/helengine.editor/model/EditorExact2DPreviewRenderState.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewDirtyStateComparer.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewCaptureService.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewSceneFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewMaterialFactory.cs`
- Create: `engine/helengine.editor/shaders/builtin/EditorExact2DPreview.hlsl`

### Tests

- Modify: `engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs`
- Modify: `engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`
- Create: `engine/helengine.editor.tests/EditorExact2DPreviewDirtyStateComparerTests.cs`
- Create: `engine/helengine.editor.tests/EditorExact2DPreviewCaptureServiceTests.cs`

---

### Task 1: Expand Preview Routing To Include Exact Text And Rounded-Rect Proxies

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs`
- Modify: `engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs`
- Modify: `engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs`

- [ ] **Step 1: Write the failing sync tests**

Add focused tests proving:

```csharp
[Fact]
public void Update_WhenSourceEntityUsesTextComponent_CreatesPreviewProxy() {
    // create an authored text entity
    // run the sync component
    // assert one preview entity is registered
}

[Fact]
public void Update_WhenSourceEntityUsesRoundedRectComponent_CreatesPreviewProxy() {
    // create an authored rounded-rect entity
    // run the sync component
    // assert one preview entity is registered
}
```

- [ ] **Step 2: Run the sync tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: FAIL because the mapper still only supports `SpriteComponent`.

- [ ] **Step 3: Update the mapper and sync component minimally**

Implement:

- `EditorWorldSpace2DPreviewMapper.TryResolveSupportedSourceComponent(...)` must recognize:
  - `SpriteComponent`
  - `TextComponent`
  - `RoundedRectComponent`
- `EditorWorldSpace2DPreviewSyncComponent.CreatePreviewComponent(...)` must construct the correct preview component type for each supported source component.

Do not add exact capture logic yet. This task only opens the routing path.

- [ ] **Step 4: Run the sync tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorWorldSpace2DPreviewMapper.cs engine/helengine.editor/components/EditorWorldSpace2DPreviewSyncComponent.cs engine/helengine.editor.tests/EditorWorldSpace2DPreviewSyncComponentTests.cs
git commit -m "Expand editor 2d preview routing"
```

### Task 2: Add Dirty-State Modeling For Exact 2D Previews

**Files:**
- Create: `engine/helengine.editor/model/EditorExact2DPreviewRenderState.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewDirtyStateComparer.cs`
- Create: `engine/helengine.editor.tests/EditorExact2DPreviewDirtyStateComparerTests.cs`

- [ ] **Step 1: Write the failing dirty-state tests**

Add tests that prove:

```csharp
[Fact]
public void IsTextPreviewDirty_WhenOnlyTransformChanges_ReturnsFalse() {
    // same visible text state, different transform
    // assert no recapture required
}

[Fact]
public void IsTextPreviewDirty_WhenVisibleTextDataChanges_ReturnsTrue() {
    // change text, font, size, color, or alignment
    // assert recapture required
}

[Fact]
public void IsRoundedRectPreviewDirty_WhenVisibleShapeDataChanges_ReturnsTrue() {
    // change fill, border, radius, or size
    // assert recapture required
}
```

- [ ] **Step 2: Run the dirty-state tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorExact2DPreviewDirtyStateComparerTests"
```

Expected: FAIL because no explicit exact-preview dirty-state model exists.

- [ ] **Step 3: Implement the render-state snapshot model**

Create:

- `EditorExact2DPreviewRenderState`
  - stores only texture-affecting data
  - must not include transform-only fields
- `EditorExact2DPreviewDirtyStateComparer`
  - compares text render state
  - compares rounded-rect render state
  - returns whether recapture is required

Use explicit fields instead of loose dictionaries so state stays predictable.

- [ ] **Step 4: Run the dirty-state tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorExact2DPreviewDirtyStateComparerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/model/EditorExact2DPreviewRenderState.cs engine/helengine.editor/managers/scene/EditorExact2DPreviewDirtyStateComparer.cs engine/helengine.editor.tests/EditorExact2DPreviewDirtyStateComparerTests.cs
git commit -m "Add exact 2d preview dirty state tracking"
```

### Task 3: Add The Exact Preview Capture Service And Material Path

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewCaptureService.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewSceneFactory.cs`
- Create: `engine/helengine.editor/managers/scene/EditorExact2DPreviewMaterialFactory.cs`
- Create: `engine/helengine.editor/shaders/builtin/EditorExact2DPreview.hlsl`
- Create: `engine/helengine.editor.tests/EditorExact2DPreviewCaptureServiceTests.cs`

- [ ] **Step 1: Write the failing capture-service tests**

Add tests that prove:

```csharp
[Fact]
public void CaptureTextPreview_WhenRequested_CreatesOrResizesRenderTargetToRequestedSize() {
    // request a text preview at one size
    // assert a render target exists with matching dimensions
}

[Fact]
public void CaptureRoundedRectPreview_WhenRequested_BindsPreviewTextureOnReturnedMaterial() {
    // capture a rounded-rect preview
    // assert the returned material samples the preview render target
}

[Fact]
public void Dispose_WhenCalled_ReleasesOwnedRenderTargetResources() {
    // create capture resources, dispose, assert release path runs
}
```

- [ ] **Step 2: Run the capture-service tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorExact2DPreviewCaptureServiceTests"
```

Expected: FAIL because no exact preview capture service exists.

- [ ] **Step 3: Implement the exact capture service and support classes**

Build an editor-only capture stack that:

- creates a render target using the existing editor render-target pattern
- creates an isolated capture scene/camera for one source component instance
- renders the component into its own texture
- returns or updates a runtime material that samples that texture
- releases the owned resources deterministically

Reuse the patterns already present in:

- `EditorViewportCanvasPlanePreviewComponent`
- `CameraPreviewSource`
- `ModelPreviewSource`

Do not push any of this into `helengine.core`.

- [ ] **Step 4: Run the capture-service tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorExact2DPreviewCaptureServiceTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorExact2DPreviewCaptureService.cs engine/helengine.editor/managers/scene/EditorExact2DPreviewSceneFactory.cs engine/helengine.editor/managers/scene/EditorExact2DPreviewMaterialFactory.cs engine/helengine.editor/shaders/builtin/EditorExact2DPreview.hlsl engine/helengine.editor.tests/EditorExact2DPreviewCaptureServiceTests.cs
git commit -m "Add exact 2d preview capture service"
```

### Task 4: Refactor Text And Rounded-Rect Preview Components Onto The Exact Capture Base

**Files:**
- Create: `engine/helengine.editor/components/preview2d/EditorExact2DWorldPreviewComponentBase.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs`
- Modify: `engine/helengine.editor/components/preview2d/EditorWorldSpace2DPreviewComponentBase.cs`
- Modify: `engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs`

- [ ] **Step 1: Write the failing preview-proxy tests**

Add tests that prove:

```csharp
[Fact]
public void Update_WhenTextPreviewSynchronizes_UsesPositiveXyPlaneAndSourceOrientation() {
    // assert exact preview quad uses the same XY contract as sprites
}

[Fact]
public void Update_WhenOnlyTextTransformChanges_DoesNotRebuildPreviewTexture() {
    // move/rotate source only
    // assert capture count does not increase
}

[Fact]
public void Update_WhenRoundedRectVisibleDataChanges_RebuildsPreviewTexture() {
    // change a visible rounded-rect property
    // assert capture count increases
}
```

- [ ] **Step 2: Run the preview-proxy tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: FAIL because text and rounded-rect previews still use the old placeholder textured-quad logic.

- [ ] **Step 3: Implement the exact preview base and migrate the two component types**

Create a shared base for exact previews that:

- owns the capture service and render state snapshot
- tracks dirty state
- rebuilds preview textures only when required
- updates the shared positive-XY world-space quad transform every sync
- releases owned render targets and materials cleanly

Then migrate:

- `EditorTextWorldPreviewComponent`
- `EditorRoundedRectWorldPreviewComponent`

to inherit from that exact-preview base.

Keep `EditorSpriteWorldPreviewComponent` on the simpler direct path.

- [ ] **Step 4: Run the preview-proxy tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/preview2d/EditorExact2DWorldPreviewComponentBase.cs engine/helengine.editor/components/preview2d/EditorTextWorldPreviewComponent.cs engine/helengine.editor/components/preview2d/EditorRoundedRectWorldPreviewComponent.cs engine/helengine.editor/components/preview2d/EditorWorldSpace2DPreviewComponentBase.cs engine/helengine.editor.tests/Editor2DWorldPreviewProxyTests.cs
git commit -m "Add exact text and rounded rect world previews"
```

### Task 5: Extend Scene-Camera 2D Queue Filtering For Exact World Previews

**Files:**
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs`

- [ ] **Step 1: Write the failing queue-filter tests**

Add tests that prove:

```csharp
[Fact]
public void Update_WhenTextHasWorldPreview_RemovesItFromSceneCameraQueue() {
    // authored text now has a world-preview path
    // assert it leaves RenderQueue2D
}

[Fact]
public void Update_WhenRoundedRectHasWorldPreview_RemovesItFromSceneCameraQueue() {
    // authored rounded rect now has a world-preview path
    // assert it leaves RenderQueue2D
}
```

- [ ] **Step 2: Run the queue-filter tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests"
```

Expected: FAIL because the scene-camera queue still keeps text and rounded rect on the 2D path.

- [ ] **Step 3: Update direct 2D presentation filtering**

Change `EditorViewportDirect2DPresentationService` so authored `TextComponent` and `RoundedRectComponent` that now have world-preview support are removed from the scene camera queue exactly like sprites.

Do not change the rule for internal editor viewport UI.

- [ ] **Step 4: Run the queue-filter tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs
git commit -m "Route exact 2d previews off the scene camera queue"
```

### Task 6: Verify Selection Still Resolves To Authored Sources

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs` only if test evidence requires it

- [ ] **Step 1: Write the failing selection tests**

Add tests that prove:

```csharp
[Fact]
public void ResolveSelection_WhenTextPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
    // register a text preview proxy
    // assert selection resolves back to the source entity
}

[Fact]
public void ResolveSelection_WhenRoundedRectPreviewProxyIsClicked_SelectsTheUnderlyingSourceEntity() {
    // register a rounded-rect preview proxy
    // assert selection resolves back to the source entity
}
```

- [ ] **Step 2: Run the selection tests to verify they fail if needed**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: either FAIL because selection is sprite-specific, or PASS and confirm no code change is necessary.

- [ ] **Step 3: Make the minimal selection change if the tests require it**

If selection is already proxy-generic through the registry, keep the production code unchanged and document the proof through the tests. If not, make the smallest editor-only fix needed so all exact preview proxies resolve back to their authored source entity.

- [ ] **Step 4: Run the selection tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs
git commit -m "Verify exact 2d preview selection routing"
```

### Task 7: Run The Focused Regression Slice And Rebuild The Editor App

**Files:**
- No code changes required unless regressions appear

- [ ] **Step 1: Run the full focused world-preview regression slice**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~Editor2DWorldPreviewProxyTests|FullyQualifiedName~EditorWorldSpace2DPreviewSyncComponentTests|FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests|FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: PASS for the full exact-preview regression slice.

- [ ] **Step 2: Build the editor app**

Run:

```bash
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -c Debug --no-restore
```

Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
git add .
git commit -m "Complete exact editor 2d world previews"
```

## Self-Review

- Spec coverage:
  - exact world-space text preview: covered by Tasks 1, 3, 4, 5, 6
  - exact world-space rounded-rect preview: covered by Tasks 1, 3, 4, 5, 6
  - dirty-driven refresh: covered by Tasks 2 and 4
  - editor-only scope/no `helengine.core` changes: enforced in header, file structure, and task descriptions
  - scene-camera queue filtering: covered by Task 5
  - selection mapping: covered by Task 6
- Placeholder scan:
  - no `TODO`/`TBD`
  - no “similar to previous task” shortcuts
  - each task names exact files, exact commands, and expected outcomes
- Type consistency:
  - exact preview state is modeled through `EditorExact2DPreviewRenderState`
  - dirty evaluation is centralized in `EditorExact2DPreviewDirtyStateComparer`
  - capture ownership is centralized in `EditorExact2DPreviewCaptureService`


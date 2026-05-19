# World-Space 2D In 3D Scene View Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render scene 2D components in the 3D scene view at their real world transform, keep `ViewportComponent` as the only viewport-lock/layout exception, and preserve editor 2D-priority picking over overlapping 3D content.

**Architecture:** First extend the 2D rendering contract so backends can render either screen-space 2D or world-space 2D, because the current DirectX11 2D path is pixel-space only. Then update editor viewport scene rendering to use world-space 2D by default while detecting `ViewportComponent`-owned subtrees as the one layout exception. Finally, update picker behavior so editor selection still resolves 2D first over overlapping 3D content.

**Tech Stack:** C#/.NET 9, HelEngine core runtime, DirectX11 renderer, editor viewport runtime, xUnit.

---

## File Structure

### Core rendering contract files

- Create: `engine/helengine.core/rendering/WorldSpace2DDrawContext.cs`
- Modify: `engine/helengine.core/model/interfaces/IDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/ISpriteDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/ITextDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/IRoundedRectDrawable2D.cs`
- Modify: `engine/helengine.core/managers/rendering/RenderManager2D.cs`
- Modify: `engine/helengine.core/components/2d/SpriteComponent.cs`
- Modify: `engine/helengine.core/components/2d/TextComponent.cs`
- Modify: `engine/helengine.core/components/2d/RoundedRectComponent.cs`

### DirectX11 backend files

- Modify: `engine/helengine.directx11/DirectX11Renderer2D.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.directx11/shaders/SpriteShader.fx`
- Modify: `engine/helengine.directx11/shaders/UIShapeShader.fx`

### Viewport/layout files

- Modify: `engine/helengine.core/components/ViewportComponent.cs`
- Modify: `engine/helengine.core/components/ICameraBoundViewportOwner.cs`
- Modify: `engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`

### Picking files

- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`

### Test files

- Create: `engine/helengine.editor.tests/rendering/WorldSpace2DRenderingTests.cs`
- Create: `engine/helengine.editor.tests/EditorViewportWorldSpace2DScenePresenterTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager2D.cs`

---

### Task 1: Add A World-Space 2D Rendering Contract

**Files:**
- Create: `engine/helengine.core/rendering/WorldSpace2DDrawContext.cs`
- Modify: `engine/helengine.core/model/interfaces/IDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/ISpriteDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/ITextDrawable2D.cs`
- Modify: `engine/helengine.core/model/interfaces/IRoundedRectDrawable2D.cs`
- Modify: `engine/helengine.core/managers/rendering/RenderManager2D.cs`
- Modify: `engine/helengine.core/components/2d/SpriteComponent.cs`
- Modify: `engine/helengine.core/components/2d/TextComponent.cs`
- Modify: `engine/helengine.core/components/2d/RoundedRectComponent.cs`
- Modify: `engine/helengine.editor.tests/testing/TestRenderManager2D.cs`
- Create: `engine/helengine.editor.tests/rendering/WorldSpace2DRenderingTests.cs`

- [ ] **Step 1: Write the failing world-space 2D contract tests**

Add tests that verify:

```csharp
[Fact]
public void DrawSprite_WhenWorldSpaceDrawContextIsProvided_UsesEntityWorldTransformInsteadOfPixelDestRect() {
    // arrange one sprite entity in world space
    // call the 2D draw path through a test render manager
    // assert the draw context is marked world-space and carries the entity transform
}

[Fact]
public void DrawText_WhenNoViewportComponentOwnsTheSubtree_DefaultsToWorldSpace2D() {
    // arrange one text entity without viewport ownership
    // verify the draw contract resolves world-space mode
}
```

- [ ] **Step 2: Run the world-space 2D contract tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~WorldSpace2DRenderingTests"
```

Expected: FAIL because the current 2D draw contract is screen-space only and does not expose world-space draw information.

- [ ] **Step 3: Add a focused world-space 2D draw context**

Create `WorldSpace2DDrawContext.cs` to hold the minimal data the backend needs for world-space 2D rendering:

- world transform
- draw mode flag
- any per-draw presentation override needed for viewport-owned subtrees later

Keep this type small and backend-facing.

- [ ] **Step 4: Extend the 2D drawable/render-manager contract minimally**

Update the 2D drawable and render-manager interfaces so components can issue either:

- screen-space 2D draws
- world-space 2D draws

Do not duplicate component types. Extend the existing contract.

- [ ] **Step 5: Update `SpriteComponent`, `TextComponent`, and `RoundedRectComponent` to use the new contract**

Default behavior should become:

- if no viewport-owned layout exception applies, emit world-space 2D
- do not change author-facing component schemas yet

- [ ] **Step 6: Update `TestRenderManager2D` to capture the new draw mode**

The test renderer should record whether each draw was issued as world-space or screen-space so the new tests can assert the contract directly.

- [ ] **Step 7: Run the world-space 2D contract tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~WorldSpace2DRenderingTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.core/rendering/WorldSpace2DDrawContext.cs engine/helengine.core/model/interfaces/IDrawable2D.cs engine/helengine.core/model/interfaces/ISpriteDrawable2D.cs engine/helengine.core/model/interfaces/ITextDrawable2D.cs engine/helengine.core/model/interfaces/IRoundedRectDrawable2D.cs engine/helengine.core/managers/rendering/RenderManager2D.cs engine/helengine.core/components/2d/SpriteComponent.cs engine/helengine.core/components/2d/TextComponent.cs engine/helengine.core/components/2d/RoundedRectComponent.cs engine/helengine.editor.tests/testing/TestRenderManager2D.cs engine/helengine.editor.tests/rendering/WorldSpace2DRenderingTests.cs
git commit -m "Add world-space 2d rendering contract"
```

### Task 2: Implement World-Space 2D Rendering In The DirectX11 Backend

**Files:**
- Modify: `engine/helengine.directx11/DirectX11Renderer2D.cs`
- Modify: `engine/helengine.directx11/DirectX11Renderer3D.cs`
- Modify: `engine/helengine.directx11/shaders/SpriteShader.fx`
- Modify: `engine/helengine.directx11/shaders/UIShapeShader.fx`
- Modify: `engine/helengine.editor.tests/rendering/WorldSpace2DRenderingTests.cs`

- [ ] **Step 1: Write the failing backend test for world-space sprite rendering**

Add or extend tests so they verify the DirectX11 2D backend can consume world-space draw context without falling back to pixel-space `destRect` semantics.

- [ ] **Step 2: Run the backend tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~WorldSpace2DRenderingTests"
```

Expected: FAIL because `DirectX11Renderer2D` currently uses `projectionMatrix2D` and pixel-space rectangles only.

- [ ] **Step 3: Add a world-space path to `DirectX11Renderer2D`**

Implement a backend path that:

- builds a world transform from the entity transform
- uses scene-camera view/projection instead of only `projectionMatrix2D`
- keeps sprites/text/rounded rectangles visually flat while existing in world space

Do not remove the existing screen-space path; viewport-owned layout still needs it.

- [ ] **Step 4: Update the required shaders**

Adjust shader constants/inputs so sprite and 2D shape rendering can use world-space transforms where needed.

- [ ] **Step 5: Verify the backend tests pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~WorldSpace2DRenderingTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.directx11/DirectX11Renderer2D.cs engine/helengine.directx11/DirectX11Renderer3D.cs engine/helengine.directx11/shaders/SpriteShader.fx engine/helengine.directx11/shaders/UIShapeShader.fx engine/helengine.editor.tests/rendering/WorldSpace2DRenderingTests.cs
git commit -m "Render world-space 2d through directx11"
```

### Task 3: Keep `ViewportComponent` As The Only Layout Exception

**Files:**
- Modify: `engine/helengine.core/components/ViewportComponent.cs`
- Modify: `engine/helengine.core/components/ICameraBoundViewportOwner.cs`
- Modify: `engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs`
- Create: `engine/helengine.editor.tests/EditorViewportWorldSpace2DScenePresenterTests.cs`

- [ ] **Step 1: Write the failing viewport-exception tests**

Add tests that verify:

```csharp
[Fact]
public void Update_When2DSubtreeHasNoViewportComponent_UsesWorldSpace2DPresentation() {
    // assert direct scene presenter reports world-space mode
}

[Fact]
public void Update_When2DSubtreeIsOwnedByViewportComponent_KeepsViewportLockBehavior() {
    // assert viewport-owned subtree still uses viewport-driven layout sizing
}
```

- [ ] **Step 2: Run the viewport-exception tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportWorldSpace2DScenePresenterTests|FullyQualifiedName~ViewportAndAnchorLayoutTests"
```

Expected: FAIL because the current presenter still assumes viewport-sized presentation rather than default world-space behavior.

- [ ] **Step 3: Narrow the role of `ViewportComponent`**

Refine the viewport logic so it is only consulted as an explicit layout exception for its owned subtree. It should not redefine the default rendering model for all 2D scene content.

- [ ] **Step 4: Update the direct scene presenter and presentation service**

Change the presenter/service so:

- default scene-view 2D is world-space
- viewport-owned subtrees remain viewport-locked/resized
- no screen-sized proxy rectangle is assumed

- [ ] **Step 5: Run the viewport-exception tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportWorldSpace2DScenePresenterTests|FullyQualifiedName~ViewportAndAnchorLayoutTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.core/components/ViewportComponent.cs engine/helengine.core/components/ICameraBoundViewportOwner.cs engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs engine/helengine.editor.tests/ViewportAndAnchorLayoutTests.cs engine/helengine.editor.tests/EditorViewportWorldSpace2DScenePresenterTests.cs
git commit -m "Keep viewport component as the world-space 2d exception"
```

### Task 4: Make Editor Picking Prefer 2D Over 3D In The World-Space Model

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`

- [ ] **Step 1: Write the failing world-space 2D priority picking tests**

Add tests that verify:

```csharp
[Fact]
public void ResolveSelection_WhenWorldSpace2DAnd3DOverlap_Prefers2DEntity() {
    // same pointer overlaps a world-space 2D entity and a 3D entity
    // assert the 2D entity wins
}

[Fact]
public void ResolveSelection_WhenNo2DHitExists_FallsBackTo3D() {
    // assert no regression in normal 3D selection
}
```

- [ ] **Step 2: Run the picking tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: FAIL if the picker still assumes the earlier direct-screen-space path rather than actual world-space 2D hit resolution.

- [ ] **Step 3: Update picker hit resolution for world-space 2D**

Adjust picker logic so it resolves selectable world-space 2D hits first, then falls back to 3D picking. Keep this as an editor-only priority rule.

- [ ] **Step 4: Run the picking tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportPicker.cs engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs
git commit -m "Prefer world-space 2d entities in viewport picking"
```

### Task 5: Remove The Proxy Plane As The Primary Model And Verify End To End

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`

- [ ] **Step 1: Write or extend a regression test that proves world-space 2D scene view does not depend on the canvas proxy plane**

Use the existing viewport/session test seams to verify viewport runtime stacks do not require the proxy plane for normal world-space 2D scene rendering.

- [ ] **Step 2: Run the regression test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorSession|FullyQualifiedName~EditorViewportWorldSpace2DScenePresenterTests"
```

Expected: FAIL until the remaining proxy-plane dependencies are removed or reduced to compatibility-only status.

- [ ] **Step 3: Remove primary runtime dependencies on the proxy plane**

Update workspace/session plumbing so the proxy plane is no longer the primary model for scene-view 2D. Keep only compatibility code that is still genuinely needed.

- [ ] **Step 4: Run the focused and broader verification slices**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~WorldSpace2DRenderingTests|FullyQualifiedName~EditorViewportWorldSpace2DScenePresenterTests|FullyQualifiedName~EditorViewportPicker2DSelectionTests|FullyQualifiedName~ViewportAndAnchorLayoutTests"
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorSession|FullyQualifiedName~SceneFileLoadServiceTests"
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -c Debug --no-restore
```

Expected: PASS.

- [ ] **Step 5: Manual validation in the editor**

Validate:

- a 2D scene object outside any `ViewportComponent` subtree appears in the 3D scene view at its actual world transform
- the scene camera perspective and distance affect 2D content visually
- a 2D scene object inside a `ViewportComponent` subtree still follows viewport-lock/resizing behavior
- when 2D overlaps 3D, selecting in the scene view chooses the 2D entity
- the proxy plane is no longer the primary scene-view model

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs engine/helengine.editor/EditorSession.cs
git commit -m "Finish world-space 2d scene view integration"
```

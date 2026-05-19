# Direct 2D In 3D Viewport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Render scene 2D content directly in every 3D scene viewport, move scene viewport resolution ownership into `ViewportComponent`, and make viewport picking resolve underlying 2D entities before 3D hits.

**Architecture:** Extend `ViewportComponent` so it can resolve and expose the scene viewport rectangle that direct 2D-in-3D presentation needs, then replace the editor’s canvas-plane proxy path with direct 2D queue submission through the scene viewport cameras. After rendering is direct, update picking so viewport clicks test 2D scene entities first and only fall back to 3D selection when no selectable 2D entity is hit.

**Tech Stack:** C#/.NET 9, HelEngine core runtime, editor viewport runtime, DirectX11 editor renderer, xUnit.

---

## File Structure

### Core runtime files

- Modify: `engine/helengine.core/components/ViewportComponent.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Modify: `engine/helengine.core/model/interfaces/ICameraBoundViewportOwner.cs`
- Modify: `engine/helengine.core/managers/ObjectManager.cs`

### Editor viewport rendering files

- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Create: `engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`

### Editor picking files

- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`

### Test files

- Create: `engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs`
- Create: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`
- Modify: `engine/helengine.editor.tests/ViewportComponentTests.cs`
- Modify: `engine/helengine.editor.tests/managers/workspace/ViewportWorkspacePanelControllerTests.cs`

---

### Task 1: Move Scene Viewport Resolution Ownership Into `ViewportComponent`

**Files:**
- Modify: `engine/helengine.core/components/ViewportComponent.cs`
- Modify: `engine/helengine.core/components/CameraComponent.cs`
- Modify: `engine/helengine.core/model/interfaces/ICameraBoundViewportOwner.cs`
- Modify: `engine/helengine.editor.tests/ViewportComponentTests.cs`

- [ ] **Step 1: Write the failing viewport-resolution tests**

Add focused tests to `ViewportComponentTests.cs` that verify:

```csharp
[Fact]
public void Update_WhenViewportIsBoundToSceneCamera_ExposesResolvedViewportSizeForWorldPresented2D() {
    ViewportComponent viewport = new ViewportComponent();
    CameraComponent camera = new CameraComponent {
        Viewport = new float4(0f, 0f, 1280f, 720f)
    };

    // attach camera and viewport under one test entity tree
    // drive update

    Assert.Equal(new int2(1280, 720), viewport.ResolvedViewportSize);
}

[Fact]
public void Update_WhenViewportChanges_RaisesAnchorBoundsChangedForDirectSceneViewportConsumers() {
    // bind viewport to camera, change camera viewport, verify event fired once
}
```

- [ ] **Step 2: Run the viewport-resolution tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportComponentTests"
```

Expected: FAIL because `ViewportComponent` does not yet expose the resolved scene viewport contract needed by the editor viewport path.

- [ ] **Step 3: Add the minimal resolved-viewport API to `ViewportComponent`**

Implement:

- one explicit resolved viewport rectangle/property on `ViewportComponent`
- one resolved pixel size/property derived from the active binding
- one stable update path that recomputes and publishes the resolved viewport when the bound camera viewport changes

Keep the implementation in `ViewportComponent` only. Do not add editor-specific knowledge here.

- [ ] **Step 4: Update any camera-bound viewport owner interfaces required by the new contract**

If `ICameraBoundViewportOwner` or related camera-facing contracts need to expose the resolved viewport more explicitly, update them now so editor viewport systems can consume the data without reflecting private `ViewportComponent` internals.

- [ ] **Step 5: Run the viewport-resolution tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportComponentTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add engine/helengine.core/components/ViewportComponent.cs engine/helengine.core/components/CameraComponent.cs engine/helengine.core/model/interfaces/ICameraBoundViewportOwner.cs engine/helengine.editor.tests/ViewportComponentTests.cs
git commit -m "Move scene viewport resolution into viewport component"
```

### Task 2: Add Direct 2D Scene Presentation To Every 3D Viewport

**Files:**
- Create: `engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs`
- Create: `engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs`
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor.tests/managers/workspace/ViewportWorkspacePanelControllerTests.cs`
- Create: `engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs`

- [ ] **Step 1: Write the failing direct-presentation tests**

Add tests that verify:

```csharp
[Fact]
public void Update_WhenViewportIs1280By720_Presents2DInMatchingWorldSpaceRectangle() {
    // create scene camera + viewport component + direct presenter
    // verify presenter computes a 1280 x 720 world presentation area
}

[Fact]
public void Update_WhenPresenterSynchronizesQueues_RoutesScene2DDrawablesDirectlyThroughTheSceneViewport() {
    // verify scene camera receives scene 2D drawables directly
    // verify the old preview-camera-only queue path is not the active path
}
```

- [ ] **Step 2: Run the direct-presentation tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests|FullyQualifiedName~ViewportWorkspacePanelControllerTests"
```

Expected: FAIL because the workspace still creates and depends on the canvas-plane proxy path.

- [ ] **Step 3: Implement `EditorViewportDirect2DPresentationService`**

Add one focused service that:

- consumes the resolved viewport rectangle from `ViewportComponent`
- computes the world-presented 2D rectangle using `1 pixel = 1 world unit`
- exposes the transform or presentation data needed by the scene viewport presenter

Keep transform math and viewport-to-world presentation logic in this service instead of spreading it across controller code.

- [ ] **Step 4: Implement `EditorViewportDirect2DScenePresenterComponent`**

Add one component that:

- lives beside the scene camera in each viewport runtime stack
- uses the presentation service and `ViewportComponent` data
- synchronizes the scene camera’s 2D presentation path directly
- makes every scene viewport render scene 2D content without relying on the proxy plane

- [ ] **Step 5: Update viewport workspace construction to use the direct presenter**

Modify `ViewportWorkspacePanelController.cs`, `EditorViewportWorkspaceState.cs`, and `EditorSession.cs` so each viewport runtime stack owns the direct presenter component and no longer depends on the canvas-plane preview component as the primary scene 2D presentation path.

- [ ] **Step 6: Reduce `EditorViewportCanvasPlanePreviewComponent` to transitional compatibility only**

Keep only the minimum code still required while the rest of the editor migrates, or stub the component out if nothing needs it anymore after direct presentation lands. Do not leave it as the authoritative path.

- [ ] **Step 7: Run the direct-presentation tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests|FullyQualifiedName~ViewportWorkspacePanelControllerTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportDirect2DScenePresenterComponent.cs engine/helengine.editor/managers/scene/EditorViewportDirect2DPresentationService.cs engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor.tests/EditorViewportDirect2DScenePresenterComponentTests.cs engine/helengine.editor.tests/managers/workspace/ViewportWorkspacePanelControllerTests.cs
git commit -m "Render scene 2d directly in 3d viewports"
```

### Task 3: Make Viewport Picking Resolve 2D Entities Before 3D

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportPicker.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionService.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs`
- Create: `engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs`

- [ ] **Step 1: Write the failing 2D-first picking tests**

Add tests that verify:

```csharp
[Fact]
public void ResolveSelection_WhenSelectable2DAnd3DOverlap_Selects2DEntity() {
    // same pointer position intersects projected 2D entity and 3D entity
    // picker should resolve the 2D entity
}

[Fact]
public void ResolveSelection_WhenNo2DHitExists_FallsBackTo3DSelection() {
    // no selectable 2D hit, 3D hit exists
    // picker should resolve the 3D entity
}
```

- [ ] **Step 2: Run the picking tests to verify they fail**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: FAIL because the picker currently resolves 3D proxy-plane hits and only later bridges into 2D.

- [ ] **Step 3: Change `EditorViewportPicker` to try direct 2D selection first**

Refactor the selection flow so scene viewport clicks:

1. resolve selectable 2D entities using the direct viewport-presentation mapping
2. fall back to 3D selection only if no selectable 2D entity is hit

Do not keep the proxy plane as the primary selection trigger.

- [ ] **Step 4: Adjust the canvas selection service to become a general viewport-space 2D hit resolver**

If `EditorViewportCanvasPlaneSelectionService` still contains useful pointer-to-2D hit-test math, adapt it to the direct-presentation model and rename/split later only if needed. The important part is that it resolves actual 2D entities, not plane entities.

- [ ] **Step 5: Keep selection filtering aligned with direct 2D entity hits**

Update `EditorViewportSceneSelectionFilter.cs` so internally generated editor infrastructure stays non-selectable while actual scene-owned 2D entities remain selectable through the new direct path.

- [ ] **Step 6: Run the picking tests to verify they pass**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorViewportPicker2DSelectionTests"
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportPicker.cs engine/helengine.editor/managers/scene/EditorViewportCanvasPlaneSelectionService.cs engine/helengine.editor/managers/scene/EditorViewportSceneSelectionFilter.cs engine/helengine.editor.tests/EditorViewportPicker2DSelectionTests.cs
git commit -m "Prefer 2d entities in viewport picking"
```

### Task 4: Remove The Canvas-Plane Proxy As The Primary Scene Viewport Path

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs`
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/managers/workspace/ViewportWorkspacePanelControllerTests.cs`

- [ ] **Step 1: Write the failing regression test for workspace construction**

Add or extend a workspace-level test that verifies newly created viewport runtime stacks no longer require the canvas-plane preview component to show/select scene 2D content.

- [ ] **Step 2: Run the workspace regression test to verify it fails**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportWorkspacePanelControllerTests"
```

Expected: FAIL because the workspace still treats the canvas-plane preview component as required state.

- [ ] **Step 3: Remove primary runtime dependencies on the canvas-plane preview component**

Update workspace/runtime state so:

- direct 2D presenter is the authoritative scene viewport path
- workspace state does not require a preview-plane component for normal scene 2D rendering
- editor session wiring no longer assumes the plane path is always present

- [ ] **Step 4: Run the workspace regression test to verify it passes**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportWorkspacePanelControllerTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/EditorViewportCanvasPlanePreviewComponent.cs engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor/managers/workspace/EditorViewportWorkspaceState.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/managers/workspace/ViewportWorkspacePanelControllerTests.cs
git commit -m "Retire canvas plane as primary viewport path"
```

### Task 5: Final Verification

**Files:**
- No code changes required unless verification exposes gaps.

- [ ] **Step 1: Run the focused viewport and picking test slices**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~ViewportComponentTests|FullyQualifiedName~EditorViewportDirect2DScenePresenterComponentTests|FullyQualifiedName~EditorViewportPicker2DSelectionTests|FullyQualifiedName~ViewportWorkspacePanelControllerTests"
```

Expected: PASS.

- [ ] **Step 2: Run one broader editor scene interaction slice**

Run:

```bash
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --filter "FullyQualifiedName~EditorSession|FullyQualifiedName~SceneFileLoadServiceTests"
```

Expected: PASS or only unrelated known failures.

- [ ] **Step 3: Build the editor host**

Run:

```bash
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -c Debug --no-restore
```

Expected: PASS.

- [ ] **Step 4: Manual validation in the editor**

Validate:

- every scene viewport shows scene 2D content directly
- a `1280x720` scene viewport presents 2D content at `1280 x 720` world units
- clicking a visible 2D entity in the 3D view selects that 2D entity
- when 2D overlaps 3D, the 2D entity wins
- no viewport depends on the proxy plane to present/select 2D scene content

- [ ] **Step 5: Commit any final follow-up fixes**

```bash
git add .
git commit -m "Finish direct 2d in 3d viewport integration"
```

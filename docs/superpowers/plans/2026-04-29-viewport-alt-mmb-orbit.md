# Viewport Alt-MMB Orbit Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add 3ds Max-style `Alt + middle mouse button` orbit navigation to the editor viewport, orbiting around the current selection when present and a stable virtual target otherwise.

**Architecture:** Keep orbit state inside [`engine/helengine.editor/components/EditorViewportCameraController.cs`](../../../engine/helengine.editor/components/EditorViewportCameraController.cs), alongside the existing freelook, pan, and wheel-zoom state. Extend the current controller tests in [`engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`](../../../engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs) so the feature is driven by focused input regressions instead of broad session tests.

**Tech Stack:** C#, xUnit, helengine editor runtime components, existing `TestInputManager`-based input harness

---

## File Structure

- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
  - Add persistent orbit target and orbit distance state.
  - Distinguish plain middle-mouse pan from `Alt + middle mouse` orbit.
  - Keep target coherence across freelook, pan, and wheel zoom.
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`
  - Add focused regressions for selected-target orbit, no-selection orbit, pan/zoom target synchronization, and drag continuation outside the viewport.

## Task 1: Add Orbit-Target Regression Coverage

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Write the failing selected-target orbit test**

Add a test that:
- creates a camera entity at a known offset from the origin
- creates a selected entity at the origin with `EditorSelectionService.SetSelectedEntity(...)`
- presses `Alt + middle mouse` inside the viewport
- moves the mouse horizontally on the next frame
- asserts the camera position changed while the distance from the camera to the selected entity stayed constant

```csharp
[Fact]
public void Update_WhenAltMiddleMouseOrbitsSelectedEntity_PreservesSelectedPivotDistance() {
    TestInputManager input = InitializeCore();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    cameraEntity.Position = new float3(0f, 0f, 10f);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);
    EditorEntity selectedEntity = new EditorEntity();
    EditorSelectionService.SetSelectedEntity(selectedEntity);

    CompleteInputFrame(input, CreateMouseState(150, 150, 0, false, ButtonState.Released));
    AdvanceInput(input, CreateMouseState(150, 150, 0, true, ButtonState.Pressed));
    controller.Update();
    AdvanceInput(input, CreateMouseState(190, 150, 0, true, ButtonState.Pressed));

    controller.Update();

    Assert.NotEqual(new float3(0f, 0f, 10f), cameraEntity.Position);
    Assert.Equal(10d, Distance(cameraEntity.Position, selectedEntity.Position), 3);
}
```

- [ ] **Step 2: Run the selected-target test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenAltMiddleMouseOrbitsSelectedEntity_PreservesSelectedPivotDistance" -v minimal`

Expected: FAIL because `EditorViewportCameraController` does not start an orbit on `Alt + middle mouse`.

- [ ] **Step 3: Write the failing no-selection fallback orbit test**

Add a second test that:
- starts with no selection
- performs an `Alt + middle mouse` orbit
- asserts the camera moves and still faces a stable virtual target instead of doing nothing

```csharp
[Fact]
public void Update_WhenAltMiddleMouseOrbitsWithoutSelection_UsesStoredVirtualTarget() {
    TestInputManager input = InitializeCore();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    cameraEntity.Position = new float3(0f, 0f, 8f);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);

    CompleteInputFrame(input, CreateMouseState(150, 150, 0, false, ButtonState.Released));
    AdvanceInput(input, CreateMouseState(150, 150, 0, true, ButtonState.Pressed));
    controller.Update();
    AdvanceInput(input, CreateMouseState(180, 135, 0, true, ButtonState.Pressed));

    controller.Update();

    Assert.NotEqual(new float3(0f, 0f, 8f), cameraEntity.Position);
}
```

- [ ] **Step 4: Run the no-selection test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenAltMiddleMouseOrbitsWithoutSelection_UsesStoredVirtualTarget" -v minimal`

Expected: FAIL because the controller has no virtual target state yet.

- [ ] **Step 5: Commit the red tests**

```bash
git add engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
git commit -m "test: add viewport orbit camera regressions"
```

## Task 2: Implement Alt-MMB Orbit State In The Controller

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Verify against: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Add orbit state fields and properties**

Add controller state for:
- `isOrbiting`
- `ignoreNextOrbitDelta`
- `lastOrbitPosition`
- `virtualTarget`
- `orbitDistance`

Add constants for:
- minimum orbit distance

Document every new field and property with XML comments to match repository rules.

- [ ] **Step 2: Add failing helper-driven math assertions if needed**

If the implementation needs explicit coverage for target synchronization, add one focused test before writing the helper logic rather than debugging it inside the production file.

Possible test shape:

```csharp
[Fact]
public void Update_WhenWheelZoomMovesCamera_UpdatesLaterOrbitRadius() {
    // Zoom first, then orbit, then assert the new orbit still preserves the post-zoom radius.
}
```

- [ ] **Step 3: Implement virtual-target synchronization helpers**

Inside `EditorViewportCameraController`, add focused private methods for:
- initializing yaw/pitch and the initial virtual target from the current camera state
- recomputing `virtualTarget` from `Parent.Position + forward * orbitDistance`
- recomputing `orbitDistance` from `Parent.Position` and the current target
- resolving the orbit target from `EditorSelectionService.SelectedEntity` or `virtualTarget`
- applying orbit deltas around a target while keeping the camera facing the target

Do not add local helper functions. Keep the math in controller methods unless a reusable editor utility becomes clearly necessary.

- [ ] **Step 4: Wire `Alt + middle mouse` orbit activation**

Update the middle-mouse handling so:
- `Alt + middle mouse` starts orbit only when the press begins inside the viewport and input is not blocked
- plain middle mouse still starts pan
- orbit and pan never start simultaneously
- release of the middle button clears both drag states

- [ ] **Step 5: Run the focused orbit tests to verify they pass**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenAltMiddleMouseOrbitsSelectedEntity_PreservesSelectedPivotDistance|FullyQualifiedName~Update_WhenAltMiddleMouseOrbitsWithoutSelection_UsesStoredVirtualTarget" -v minimal`

Expected: PASS

- [ ] **Step 6: Commit the controller orbit implementation**

```bash
git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
git commit -m "feat: add alt-mmb viewport orbit"
```

## Task 3: Keep Orbit Coherent Across Pan, Zoom, And Freemode Look

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Write the failing pan synchronization test**

Add a test that:
- performs a plain middle-mouse pan
- then starts an `Alt + middle mouse` orbit with no selection
- asserts the orbit uses the panned target instead of snapping back to the original view center

```csharp
[Fact]
public void Update_WhenPanMovesCamera_FollowingOrbitUsesMovedVirtualTarget() {
    // Pan first, orbit second, then assert the later orbit reflects the translated target.
}
```

- [ ] **Step 2: Run the pan synchronization test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenPanMovesCamera_FollowingOrbitUsesMovedVirtualTarget" -v minimal`

Expected: FAIL because pan currently only moves `Parent.Position`.

- [ ] **Step 3: Write the failing wheel-zoom synchronization test**

Add a test that:
- scrolls inside the viewport to zoom
- starts a no-selection orbit
- asserts the orbit radius matches the zoomed camera distance, not the pre-zoom distance

```csharp
[Fact]
public void Update_WhenWheelZoomChangesDistance_FollowingOrbitUsesUpdatedOrbitRadius() {
    // Zoom first, orbit second, assert the preserved radius equals the post-zoom camera distance.
}
```

- [ ] **Step 4: Run the wheel-zoom test to verify it fails**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenWheelZoomChangesDistance_FollowingOrbitUsesUpdatedOrbitRadius" -v minimal`

Expected: FAIL because wheel zoom currently moves the camera without updating orbit state.

- [ ] **Step 5: Implement target/radius synchronization in production code**

Update the controller so:
- plain pan moves both `Parent.Position` and `virtualTarget`
- wheel zoom updates `orbitDistance` after moving the camera
- RMB freelook refreshes `virtualTarget` from the camera’s new forward direction while preserving `orbitDistance`

- [ ] **Step 6: Run the synchronization slice**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenPanMovesCamera_FollowingOrbitUsesMovedVirtualTarget|FullyQualifiedName~Update_WhenWheelZoomChangesDistance_FollowingOrbitUsesUpdatedOrbitRadius|FullyQualifiedName~Update_WhenWheelScrollsUpInsideViewport_MovesCameraForward|FullyQualifiedName~Update_WhenWheelScrollsDownInsideViewport_MovesCameraBackward" -v minimal`

Expected: PASS

- [ ] **Step 7: Commit the coherence updates**

```bash
git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
git commit -m "fix: keep viewport orbit target synchronized"
```

## Task 4: Preserve Orbit Drag After Leaving The Viewport

**Files:**
- Modify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`
- Verify against: `engine/helengine.editor/components/EditorViewportCameraController.cs`

- [ ] **Step 1: Write the failing viewport-leave orbit test**

Add a test that:
- starts `Alt + middle mouse` inside the viewport
- moves the cursor outside the viewport while still holding the middle button
- asserts the orbit continues updating camera position until the release frame

```csharp
[Fact]
public void Update_WhenOrbitStartsInsideViewport_ContinuesAfterPointerLeavesViewportUntilRelease() {
    // Start orbit inside viewport, move outside while still pressed, assert camera keeps moving.
}
```

- [ ] **Step 2: Run the viewport-leave test to verify current behavior**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~Update_WhenOrbitStartsInsideViewport_ContinuesAfterPointerLeavesViewportUntilRelease" -v minimal`

Expected: FAIL if orbit state is still tied to current pointer-inside-viewport checks instead of drag-start checks.

- [ ] **Step 3: Finalize controller release behavior if needed**

Ensure the controller:
- starts orbit only from an in-viewport press
- does not require the pointer to remain inside the viewport after the drag begins
- stops orbit immediately on middle-button release

- [ ] **Step 4: Run the full controller test slice**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCameraControllerTests" -v minimal`

Expected: PASS

- [ ] **Step 5: Commit the orbit drag completion behavior**

```bash
git add engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs engine/helengine.editor/components/EditorViewportCameraController.cs
git commit -m "test: cover viewport orbit drag lifetime"
```

## Task 5: Final Verification

**Files:**
- Verify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Verify: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Run focused editor verification**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCameraControllerTests" -v minimal`

Expected: PASS

- [ ] **Step 2: Run broader navigation regression coverage**

Run: `rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorViewportCameraControllerTests|FullyQualifiedName~EditorViewportPointerRayBuilderTests|FullyQualifiedName~TransformTranslationGizmoFollowComponentTests|FullyQualifiedName~TransformRotationGizmoFollowComponentTests|FullyQualifiedName~TransformScaleGizmoFollowComponentTests" -v minimal`

Expected: PASS

- [ ] **Step 3: Build the editor assembly**

Run: `rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal`

Expected: `0 errors`

- [ ] **Step 4: Commit the final verified state**

```bash
git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
git commit -m "feat: add 3ds max style viewport orbit"
```

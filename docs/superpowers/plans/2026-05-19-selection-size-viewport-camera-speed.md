# Selection-Size Viewport Camera Speed Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make editor viewport camera navigation adapt to the currently selected object size by default, while allowing a per-viewport manual speed override in the viewport settings overlay.

**Architecture:** Keep the behavior entirely editor-side by extending the viewport camera controller and reusing the editor-only selection-bounds seam already introduced for viewport framing. Expose viewport-local speed mode and manual override state through the viewport settings overlay and persist both through workspace viewport save/load.

**Tech Stack:** C#/.NET 9, xUnit, HelEngine editor viewport/workspace systems, editor-only scene services

---

## File Structure

### Existing Files To Modify

- `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs`
  - Extend the existing editor-only selection-bounds seam so camera speed and framing share the same bounds resolution logic.
- `engine/helengine.editor/components/EditorViewportCameraController.cs`
  - Add viewport-local speed mode, manual speed value, effective speed derivation, and selection-size adaptation.
- `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
  - Add UI controls for camera speed mode and manual override value.
- `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
  - Persist new viewport camera speed state through workspace runtime/controller setup.
- `engine/helengine.editor.tests/EditorViewportSelectionFramingServiceTests.cs`
  - Add coverage for shared selection-extent resolution.
- `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`
  - Add coverage for adaptive speed behavior and manual override behavior.
- `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`
  - Add overlay interaction coverage for the new speed controls.
- `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`
  - Add workspace save/load round-trip coverage for speed mode and manual speed.

### New Files To Create

- `engine/helengine.editor/model/EditorViewportCameraSpeedMode.cs`
  - Small editor-only mode enum/constant holder for `AutoFromSelection` and `ManualOverride`.

---

### Task 1: Shared Selection Extent Resolution

**Files:**
- Create: `engine/helengine.editor/model/EditorViewportCameraSpeedMode.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSelectionFramingServiceTests.cs`

- [ ] **Step 1: Write the failing shared-bounds tests**

Add tests to `engine/helengine.editor.tests/EditorViewportSelectionFramingServiceTests.cs` that verify shared selection extent resolution for viewport, mesh, sprite, and fallback cases.

```csharp
[Fact]
public void ResolveSelectionExtent_WhenViewportEntityIsSelected_UsesResolvedViewportSize() {
    Entity viewportEntity = CreateViewportEntity(new int2(1280, 720));
    EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

    double selectionExtent = service.ResolveSelectionExtentForTest(viewportEntity);

    Assert.Equal(1280.0, selectionExtent);
}

[Fact]
public void ResolveSelectionExtent_WhenMeshEntityIsSelected_UsesLargestScaledModelDimension() {
    TestRuntimeModel runtimeModel = new TestRuntimeModel();
    runtimeModel.SetBounds(new float3(-1f, -2f, -3f), new float3(3f, 4f, 5f));
    Entity meshEntity = new Entity();
    meshEntity.InitComponents();
    meshEntity.InitChildren();
    meshEntity.LocalScale = new float3(2f, 3f, 4f);
    meshEntity.AddComponent(new MeshComponent {
        Model = runtimeModel
    });
    EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

    double selectionExtent = service.ResolveSelectionExtentForTest(meshEntity);

    Assert.Equal(32.0, selectionExtent);
}

[Fact]
public void ResolveSelectionExtent_WhenSpriteEntityIsSelected_UsesLargestSpriteDimension() {
    Entity spriteEntity = new Entity();
    spriteEntity.InitComponents();
    spriteEntity.InitChildren();
    spriteEntity.AddComponent(new SpriteComponent {
        Size = new int2(64, 96)
    });
    EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

    double selectionExtent = service.ResolveSelectionExtentForTest(spriteEntity);

    Assert.Equal(96.0, selectionExtent);
}

[Fact]
public void ResolveSelectionExtent_WhenEntityHasNoSupportedBounds_ReturnsZero() {
    Entity entity = new Entity();
    entity.InitComponents();
    entity.InitChildren();
    EditorViewportSelectionFramingService service = new EditorViewportSelectionFramingService();

    double selectionExtent = service.ResolveSelectionExtentForTest(entity);

    Assert.Equal(0.0, selectionExtent);
}
```

- [ ] **Step 2: Run the shared-bounds tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportSelectionFramingServiceTests"
```

Expected: FAIL because `ResolveSelectionExtentForTest(...)` does not exist yet.

- [ ] **Step 3: Add the editor-only speed mode type**

Create `engine/helengine.editor/model/EditorViewportCameraSpeedMode.cs`.

```csharp
namespace helengine.editor {
    /// <summary>
    /// Defines how one editor viewport derives effective camera navigation speed.
    /// </summary>
    public static class EditorViewportCameraSpeedMode {
        /// <summary>
        /// Derives effective speed from the current selected entity bounds extent.
        /// </summary>
        public const byte AutoFromSelection = 0;

        /// <summary>
        /// Uses one viewport-local authored manual speed override.
        /// </summary>
        public const byte ManualOverride = 1;
    }
}
```

- [ ] **Step 4: Extend the framing service with shared extent resolution**

Modify `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs` so it exposes a reusable selection-extent method and uses the same bounds seam as framing.

```csharp
/// <summary>
/// Resolves one scalar selection extent for editor-only camera behavior tests and adaptive speed.
/// </summary>
/// <param name="selectedEntity">Selected entity whose bounds should be measured.</param>
/// <returns>Largest supported selection dimension, or zero when no supported bounds exist.</returns>
public double ResolveSelectionExtentForTest(Entity selectedEntity) {
    return ResolveSelectionExtent(selectedEntity);
}

double ResolveSelectionExtent(Entity selectedEntity) {
    if (selectedEntity == null) {
        return 0.0;
    }

    if (TryResolveViewportBounds(selectedEntity, out _, out _, out double viewportExtent)) {
        return viewportExtent;
    }
    if (TryResolveMeshBounds(selectedEntity, out _, out _, out double meshExtent)) {
        return meshExtent;
    }
    if (TryResolveSpriteBounds(selectedEntity, out _, out _, out double spriteExtent)) {
        return spriteExtent;
    }

    return 0.0;
}
```

Update the existing helper signatures so each supported bounds path also returns its largest extent.

- [ ] **Step 5: Run the shared-bounds tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportSelectionFramingServiceTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the shared-bounds seam**

```powershell
rtk git add engine/helengine.editor/model/EditorViewportCameraSpeedMode.cs engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs engine/helengine.editor.tests/EditorViewportSelectionFramingServiceTests.cs
rtk git commit -m "Share editor selection extent resolution"
```

---

### Task 2: Adaptive Camera Speed in the Viewport Controller

**Files:**
- Modify: `engine/helengine.editor/components/EditorViewportCameraController.cs`
- Modify: `engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs`
- Test: `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`

- [ ] **Step 1: Write the failing controller tests**

Add tests to `engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs`.

```csharp
[Fact]
public void UpdateEffectiveSpeeds_WhenAutoModeAndLargeSelection_IncreasesMovementPanAndZoom() {
    EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);
    Entity selectedViewportEntity = CreateFixedViewportEntity(new int2(2000, 1200));
    EditorSelectionService.SetSelectedEntity(selectedViewportEntity);

    controller.UpdateEffectiveSpeedsForTest(selectionBounds);

    Assert.True(controller.MoveSpeed > EditorViewportCameraController.DefaultMoveSpeed);
    Assert.True(controller.PanSpeed > EditorViewportCameraController.DefaultPanSpeed);
    Assert.True(controller.WheelZoomSpeed > EditorViewportCameraController.DefaultWheelZoomSpeed);
}

[Fact]
public void UpdateEffectiveSpeeds_WhenAutoModeAndTinySelection_DecreasesMovementPanAndZoom() {
    EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);
    Entity spriteEntity = CreateSpriteEntity(new int2(8, 8));
    EditorSelectionService.SetSelectedEntity(spriteEntity);

    controller.UpdateEffectiveSpeedsForTest(selectionBounds);

    Assert.True(controller.MoveSpeed < EditorViewportCameraController.DefaultMoveSpeed);
    Assert.True(controller.PanSpeed < EditorViewportCameraController.DefaultPanSpeed);
    Assert.True(controller.WheelZoomSpeed < EditorViewportCameraController.DefaultWheelZoomSpeed);
}

[Fact]
public void UpdateEffectiveSpeeds_WhenManualMode_IgnoresSelectionExtent() {
    EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);
    controller.SpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
    controller.ManualSpeedOverride = 12.5;
    EditorSelectionService.SetSelectedEntity(CreateFixedViewportEntity(new int2(40000, 20000)));

    controller.UpdateEffectiveSpeedsForTest(selectionBounds);

    Assert.Equal(12.5f, controller.MoveSpeed);
    Assert.Equal(12.5 * (EditorViewportCameraController.DefaultPanSpeed / EditorViewportCameraController.DefaultMoveSpeed), controller.PanSpeed, 4);
    Assert.Equal(12.5 * (EditorViewportCameraController.DefaultWheelZoomSpeed / EditorViewportCameraController.DefaultMoveSpeed), controller.WheelZoomSpeed, 4);
}

[Fact]
public void UpdateEffectiveSpeeds_WhenNoSupportedSelection_FallsBackToDefaults() {
    EditorViewportSelectionFramingService selectionBounds = new EditorViewportSelectionFramingService();
    EditorEntity cameraEntity = CreateCameraEntity(out CameraComponent camera);
    EditorViewportCameraController controller = CreateController(cameraEntity, camera);
    EditorSelectionService.SetSelectedEntity(new Entity());

    controller.UpdateEffectiveSpeedsForTest(selectionBounds);

    Assert.Equal(EditorViewportCameraController.DefaultMoveSpeed, controller.MoveSpeed);
    Assert.Equal(EditorViewportCameraController.DefaultPanSpeed, controller.PanSpeed);
    Assert.Equal(EditorViewportCameraController.DefaultWheelZoomSpeed, controller.WheelZoomSpeed);
}
```

- [ ] **Step 2: Run the controller tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportCameraControllerTests"
```

Expected: FAIL because `SpeedMode`, `ManualSpeedOverride`, and `UpdateEffectiveSpeedsForTest(...)` do not exist yet.

- [ ] **Step 3: Add adaptive speed state to the camera controller**

Modify `engine/helengine.editor/components/EditorViewportCameraController.cs`.

```csharp
/// <summary>
/// Minimum effective movement speed allowed by adaptive viewport camera navigation.
/// </summary>
public const float MinimumAdaptiveMoveSpeed = 0.02f;
/// <summary>
/// Maximum effective movement speed allowed by adaptive viewport camera navigation.
/// </summary>
public const float MaximumAdaptiveMoveSpeed = 64f;

/// <summary>
/// Gets or sets how this viewport derives effective camera speed.
/// </summary>
public byte SpeedMode { get; set; }
/// <summary>
/// Gets or sets the viewport-local authored manual movement speed override.
/// </summary>
public double ManualSpeedOverride { get; set; }
```

Initialize defaults in the constructor:

```csharp
SpeedMode = EditorViewportCameraSpeedMode.AutoFromSelection;
ManualSpeedOverride = DefaultMoveSpeed;
```

- [ ] **Step 4: Implement effective speed derivation**

Still in `EditorViewportCameraController.cs`, add one shared update method and call it from `Update()` before movement/pan/zoom input is applied.

```csharp
void UpdateEffectiveSpeeds(EditorViewportSelectionFramingService selectionBounds) {
    if (selectionBounds == null) {
        throw new ArgumentNullException(nameof(selectionBounds));
    }

    if (SpeedMode == EditorViewportCameraSpeedMode.ManualOverride) {
        ApplyManualSpeedOverride();
        return;
    }

    double selectionExtent = selectionBounds.ResolveSelectionExtentForTest(EditorSelectionService.SelectedEntity);
    if (selectionExtent <= 0.0) {
        ApplyDefaultEffectiveSpeeds();
        return;
    }

    double derivedMoveSpeed = Math.Clamp(selectionExtent * 0.001, MinimumAdaptiveMoveSpeed, MaximumAdaptiveMoveSpeed);
    MoveSpeed = (float)derivedMoveSpeed;
    PanSpeed = derivedMoveSpeed * (DefaultPanSpeed / DefaultMoveSpeed);
    WheelZoomSpeed = derivedMoveSpeed * (DefaultWheelZoomSpeed / DefaultMoveSpeed);
}
```

Add a small test-only forwarder:

```csharp
public void UpdateEffectiveSpeedsForTest(EditorViewportSelectionFramingService selectionBounds) {
    UpdateEffectiveSpeeds(selectionBounds);
}
```

- [ ] **Step 5: Run the controller tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportCameraControllerTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the adaptive controller behavior**

```powershell
rtk git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs
rtk git commit -m "Add adaptive editor viewport camera speeds"
```

---

### Task 3: Viewport Settings Overlay Controls

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Test: `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`

- [ ] **Step 1: Write the failing overlay tests**

Add tests to `engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs`.

```csharp
[Fact]
public void Overlay_WhenOpened_ExposesCameraSpeedModeAndManualSpeedControls() {
    TestHarness harness = TestHarness.Create();
    harness.OpenOverlay();

    Assert.NotNull(harness.GetCameraSpeedModeSliderForTest());
    Assert.NotNull(harness.GetCameraManualSpeedSliderForTest());
}

[Fact]
public void Overlay_WhenManualSpeedModeIsSelected_UpdatesViewportController() {
    TestHarness harness = TestHarness.Create();
    harness.OpenOverlay();

    harness.SetCameraSpeedModeForTest(EditorViewportCameraSpeedMode.ManualOverride);
    harness.SetManualCameraSpeedForTest(8.0);

    Assert.Equal(EditorViewportCameraSpeedMode.ManualOverride, harness.Controller.SpeedMode);
    Assert.Equal(8.0, harness.Controller.ManualSpeedOverride);
}
```

- [ ] **Step 2: Run the overlay tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests"
```

Expected: FAIL because the new overlay controls and harness helpers do not exist yet.

- [ ] **Step 3: Add viewport accessors for camera speed state**

Modify `engine/helengine.editor/components/ui/EditorViewport.cs` so the overlay can edit the controller-owned values through the viewport/controller path.

```csharp
/// <summary>
/// Gets or sets how this viewport derives effective camera speed.
/// </summary>
public byte CameraSpeedMode {
    get => CameraController.SpeedMode;
    set => CameraController.SpeedMode = value;
}

/// <summary>
/// Gets or sets the viewport-local authored manual movement speed override.
/// </summary>
public double ManualCameraSpeedOverride {
    get => CameraController.ManualSpeedOverride;
    set => CameraController.ManualSpeedOverride = value;
}
```

- [ ] **Step 4: Add the viewport settings overlay controls**

Modify `engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs` with one new camera speed section that mirrors the existing overlay section patterns.

```csharp
TextComponent CameraSpeedModeLabelText;
EditorSlider CameraSpeedModeSliderInternal;
TextComponent CameraManualSpeedLabelText;
EditorSlider CameraManualSpeedSliderInternal;
```

Use one simple two-state slider or toggle for:

- `0 = Auto From Selection`
- `1 = Manual Override`

And one manual speed slider:

- minimum: `0.02`
- maximum: `64.0`
- logarithmic scale

Update the visibility/interaction rules so the manual speed slider is only active when manual mode is selected.

- [ ] **Step 5: Run the overlay tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportSettingsOverlayTests"
```

Expected: PASS.

- [ ] **Step 6: Commit the overlay controls**

```powershell
rtk git add engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs
rtk git commit -m "Expose viewport camera speed controls"
```

---

### Task 4: Workspace Persistence and Integration

**Files:**
- Modify: `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs`
- Modify: `engine/helengine.editor/tests/EditorSessionWorkspaceTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`

- [ ] **Step 1: Write the failing workspace persistence tests**

Add tests to `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`.

```csharp
[Fact]
public void UiSaveAndLoad_WhenViewportUsesManualCameraSpeed_RestoresSpeedModeAndManualValue() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
    EditorWorkspacePanelInstance viewportInstance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
    ViewportWorkspacePanelController controller = harness.GetViewportControllerForTest(viewportInstance);
    controller.ViewportState.Viewport.CameraSpeedMode = EditorViewportCameraSpeedMode.ManualOverride;
    controller.ViewportState.Viewport.ManualCameraSpeedOverride = 6.5;

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.SaveSlot1);
    viewportInstance.Dockable.ActivatePanelMenuActionForTest(DockableEntityPanelMenuAction.Close);
    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.LoadSlot1);

    EditorWorkspacePanelInstance restoredViewport = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));
    Assert.Equal(EditorViewportCameraSpeedMode.ManualOverride, harness.GetViewportCameraSpeedMode(restoredViewport));
    Assert.Equal(6.5, harness.GetViewportManualCameraSpeed(restoredViewport));
}

[Fact]
public void ViewportCreation_WhenWorkspaceViewportOpens_DefaultsToAutoSelectionSpeedMode() {
    using EditorSessionHarness harness = EditorSessionHarness.Create();

    harness.Session.HandleUiMenuActionForTest(EditorTitleBarUiMenuAction.ShowViewport);
    EditorWorkspacePanelInstance viewportInstance = Assert.Single(harness.Session.GetPanelInstancesForTest("viewport"));

    Assert.Equal(EditorViewportCameraSpeedMode.AutoFromSelection, harness.GetViewportCameraSpeedMode(viewportInstance));
    Assert.Equal(EditorViewportCameraController.DefaultMoveSpeed, harness.GetViewportManualCameraSpeed(viewportInstance));
}
```

- [ ] **Step 2: Run the workspace persistence tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests"
```

Expected: FAIL because workspace serialization does not yet round-trip the new camera speed state.

- [ ] **Step 3: Persist the new viewport camera speed fields**

Modify `engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs` so the viewport saved-state payload includes:

```csharp
CameraSpeedMode = State.Viewport.CameraSpeedMode,
ManualCameraSpeedOverride = State.Viewport.ManualCameraSpeedOverride,
```

And restore those values with backward-compatible defaults:

```csharp
State.Viewport.CameraSpeedMode = document.CameraSpeedMode;
State.Viewport.ManualCameraSpeedOverride = document.ManualCameraSpeedOverride <= 0.0
    ? EditorViewportCameraController.DefaultMoveSpeed
    : document.ManualCameraSpeedOverride;
```

- [ ] **Step 4: Add harness accessors for the new viewport state**

Update `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs` harness helpers.

```csharp
public byte GetViewportCameraSpeedMode(EditorWorkspacePanelInstance instance) {
    ViewportWorkspacePanelController controller = GetViewportController(instance);
    return controller.ViewportState.Viewport.CameraSpeedMode;
}

public double GetViewportManualCameraSpeed(EditorWorkspacePanelInstance instance) {
    ViewportWorkspacePanelController controller = GetViewportController(instance);
    return controller.ViewportState.Viewport.ManualCameraSpeedOverride;
}
```

- [ ] **Step 5: Run the workspace persistence tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorSessionWorkspaceTests"
```

Expected: PASS for the new persistence/default-mode cases.

- [ ] **Step 6: Commit the workspace integration**

```powershell
rtk git add engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git commit -m "Persist viewport camera speed settings"
```

---

### Task 5: Final Verification

**Files:**
- Verify only

- [ ] **Step 1: Run the focused adaptive-speed regression slice**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportSelectionFramingServiceTests|FullyQualifiedName~EditorViewportCameraControllerTests|FullyQualifiedName~EditorViewportSettingsOverlayTests|FullyQualifiedName~EditorSessionWorkspaceTests"
```

Expected: PASS.

- [ ] **Step 2: Run the viewport keyboard/focus regression slice**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~EditorViewportKeyboardFocusTests"
```

Expected: PASS.

- [ ] **Step 3: Build the editor app**

Run:

```powershell
rtk dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj -c Debug --no-restore
```

Expected: build succeeds.

- [ ] **Step 4: Commit the final verified integration**

```powershell
rtk git add engine/helengine.editor/components/EditorViewportCameraController.cs engine/helengine.editor/components/ui/EditorViewport.cs engine/helengine.editor/components/ui/EditorViewportSettingsOverlayComponent.cs engine/helengine.editor/managers/scene/EditorViewportSelectionFramingService.cs engine/helengine.editor/managers/workspace/ViewportWorkspacePanelController.cs engine/helengine.editor/model/EditorViewportCameraSpeedMode.cs engine/helengine.editor.tests/EditorViewportSelectionFramingServiceTests.cs engine/helengine.editor.tests/EditorViewportCameraControllerTests.cs engine/helengine.editor.tests/EditorViewportSettingsOverlayTests.cs engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs
rtk git commit -m "Add adaptive editor viewport camera speed"
```

---

## Self-Review

### Spec Coverage

- Adaptive speed from selected object size: covered in Task 2.
- Per-viewport manual override: covered in Tasks 2 and 3.
- Viewport settings overlay support: covered in Task 3.
- Reuse of shared editor-only bounds seam: covered in Task 1.
- Persistence through workspace viewport state: covered in Task 4.
- Editor-only scope with no `helengine.core` changes: preserved by all tasks.

### Placeholder Scan

- No `TODO`, `TBD`, or “implement later” placeholders remain.
- Each task includes explicit files, concrete tests, exact commands, and concrete code snippets.

### Type Consistency

- Mode storage uses `byte` to match existing editor/runtime convention in this codebase.
- Manual override uses `double` to match current controller speed properties.
- Shared extent resolution stays in the editor-side framing service instead of introducing a new redundant helper type.

### TDD Check

- Every behavior change is introduced with a failing test first.
- Each task explicitly requires a red run before implementation and a green run after implementation.

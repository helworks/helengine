# Viewport Gizmo Label Drag Freeze Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep transform-gizmo axis-label text and billboard transforms frozen for the duration of a translation gizmo drag.

**Architecture:** `EditorGizmoDragService` remains the camera-scoped source of truth for active drag lifetime. `EditorViewportCameraAngleOverlayComponent` will use that exact state before applying any axis-label model or transform, retaining the labels last written on the frame before drag start. Existing visibility checks remain ahead of the freeze gate, so labels are hidden immediately when the translation tool or valid selection disappears.

**Tech Stack:** C#/.NET 9, xUnit, Helengine editor viewport and gizmo services.

---

## File structure

- Modify: `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs` — gate live axis-label updates on the camera-scoped gizmo-drag state after visibility validation.
- Modify: `engine/helengine.editor.tests/components/ui/EditorViewportCameraAngleOverlayComponentTests.cs` — cover the drag-state gate that protects the last applied label state.

### Task 1: Define the axis-label update gate

**Files:**

- Modify: `engine/helengine.editor.tests/components/ui/EditorViewportCameraAngleOverlayComponentTests.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs`

- [ ] **Step 1: Write the failing test**

Add this test and the reflection helper alongside the existing private-method test helpers:

```csharp
/// <summary>
/// Ensures transform-gizmo axis labels do not refresh while their viewport camera owns an active drag.
/// </summary>
[Fact]
public void ShouldRefreshAxisLabels_WhenTranslationGizmoIsDragging_ReturnsFalse() {
    InitializeCore();
    CameraComponent sceneCamera = new CameraComponent();
    EditorViewportCameraAngleOverlayComponent overlayComponent = new EditorViewportCameraAngleOverlayComponent(
        sceneCamera,
        CreateTestFont(),
        0,
        false);

    EditorGizmoDragService.BeginDrag(sceneCamera, new EditorEntity());

    bool shouldRefresh = InvokeShouldRefreshAxisLabels(overlayComponent);

    Assert.False(shouldRefresh);
    EditorGizmoDragService.EndDrag(sceneCamera);
}

static bool InvokeShouldRefreshAxisLabels(EditorViewportCameraAngleOverlayComponent overlayComponent) {
    MethodInfo method = typeof(EditorViewportCameraAngleOverlayComponent).GetMethod(
        "ShouldRefreshAxisLabels",
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new InvalidOperationException("Expected ShouldRefreshAxisLabels method.");
    object result = method.Invoke(overlayComponent, null) ??
                    throw new InvalidOperationException("ShouldRefreshAxisLabels returned null.");
    return (bool)result;
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportCameraAngleOverlayComponentTests.ShouldRefreshAxisLabels_WhenTranslationGizmoIsDragging_ReturnsFalse"
```

Expected: FAIL because `ShouldRefreshAxisLabels` does not exist.

- [ ] **Step 3: Implement the minimal shared drag gate**

Add this private method to `EditorViewportCameraAngleOverlayComponent`:

```csharp
/// <summary>
/// Determines whether the axis-label billboards may apply a newly resolved model or transform this frame.
/// </summary>
/// <returns>True when no transform-gizmo drag is active for this viewport camera.</returns>
bool ShouldRefreshAxisLabels() {
    return !EditorGizmoDragService.IsDragging(SceneCamera);
}
```

In `UpdateAxisLabels`, preserve its existing tool-mode and selection visibility checks, then add the gate before camera vectors, yaw, scale, or label data are recalculated:

```csharp
if (!ShouldRefreshAxisLabels()) {
    return;
}
```

This leaves the previously applied model, position, orientation, and scale intact while the same drag service tells the translation-arrow follow component to retain its frozen facing and scale.

- [ ] **Step 4: Run the focused test to verify it passes**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportCameraAngleOverlayComponentTests.ShouldRefreshAxisLabels_WhenTranslationGizmoIsDragging_ReturnsFalse"
```

Expected: PASS with one passing test.

- [ ] **Step 5: Run the affected component suites**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorViewportCameraAngleOverlayComponentTests|FullyQualifiedName~TransformTranslationGizmoFollowComponentTests"
```

Expected: all discovered tests pass.

- [ ] **Step 6: Commit the implementation**

```powershell
git add -- engine/helengine.editor/components/ui/EditorViewportCameraAngleOverlayComponent.cs engine/helengine.editor.tests/components/ui/EditorViewportCameraAngleOverlayComponentTests.cs
git commit -m "freeze viewport gizmo labels during drags"
```

## Plan self-review

- Spec coverage: Task 1 freezes label text and all billboard transforms through the existing camera-scoped drag service, while preserving pre-gate visibility checks.
- Placeholder scan: no unfinished tasks or unspecified implementation details remain.
- Type consistency: the plan uses existing `CameraComponent`, `EditorGizmoDragService`, `EditorEntity`, and xUnit reflection patterns from the targeted test file.


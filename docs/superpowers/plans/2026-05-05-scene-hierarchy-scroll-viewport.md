# Scene Hierarchy Scroll Viewport Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a real Scene Hierarchy scroll viewport boundary so hierarchy rows render only inside the panel body and no longer rely on dock sibling render ordering to appear hidden.

**Architecture:** `SceneHierarchyPanel` will own a dedicated content camera and content layer mask for hierarchy rows below the dock title bar. The panel will keep its existing node flattening, row pooling, and scroll offset logic, but rendering will be clipped by the panel-local camera viewport instead of being masked by row culling or dock render-order tricks.

**Tech Stack:** C#, xUnit, helengine 2D UI entities/components, `CameraComponent`, `ScrollComponent`, editor dock/panel infrastructure

---

## File Structure

- Modify: `engine/helengine.editor/EditorLayerMasks.cs`
  - Add a dedicated layer mask for Scene Hierarchy content rendering.
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
  - Create and manage the panel-local hierarchy content camera, viewport host, content layer assignment, scroll-root positioning, and clipping-aware hit testing.
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
  - Keep row metadata aligned with the viewport-based layout and expose any row state needed by tests.
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
  - Remove the temporary docked render-order bias workaround once clipping is real again.
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
  - Remove the temporary traversal-based dock render-order bias application.
- Modify: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
  - Add focused tests for content-layer assignment, viewport clipping setup, and input bounds.
- Modify: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`
  - Replace the workaround-oriented render-order assertions with viewport-boundary assertions.

### Task 1: Add the failing viewport-boundary tests

**Files:**
- Modify: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing Scene Hierarchy panel tests**

Add these tests to `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`:

```csharp
/// <summary>
/// Ensures Scene Hierarchy row visuals render on the dedicated hierarchy content layer instead of the shared editor UI layer.
/// </summary>
[Fact]
public void RefreshHierarchy_AssignsVisibleRowsToTheHierarchyContentLayer() {
    EditorEntity entity = new EditorEntity {
        Name = "Layered Hierarchy Entity"
    };
    SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
        Position = new float3(24f, 32f, 0f),
        Size = new int2(320, 176)
    };

    panel.RefreshHierarchy();

    List<SceneHierarchyRow> rows = GetPrivateField<List<SceneHierarchyRow>>(panel, "rows");
    SceneHierarchyRow row = null;
    for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++) {
        SceneHierarchyRow candidate = rows[rowIndex];
        if (candidate.Entity.Enabled && ReferenceEquals(candidate.NodeEntity, entity)) {
            row = candidate;
            break;
        }
    }

    Assert.NotNull(row);
    Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.Entity.LayerMask);
    Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.ArrowHost.LayerMask);
    Assert.Equal(EditorLayerMasks.SceneHierarchyContent, row.LabelHost.LayerMask);
}

/// <summary>
/// Ensures the Scene Hierarchy content camera viewport matches the panel body below the title bar.
/// </summary>
[Fact]
public void RefreshHierarchy_ConfiguresContentCameraViewportToMatchPanelBody() {
    new EditorEntity {
        Name = "Viewport Hierarchy Entity"
    };
    SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
        Position = new float3(40f, 64f, 0f),
        Size = new int2(320, 176)
    };

    panel.RefreshHierarchy();

    CameraComponent contentCamera = GetPrivateField<CameraComponent>(panel, "contentCameraComponent");

    Assert.Equal(40f, contentCamera.Viewport.X);
    Assert.Equal(64f + DockableEntity.TitleBarHeight, contentCamera.Viewport.Y);
    Assert.Equal(320f, contentCamera.Viewport.Z);
    Assert.Equal(176f, contentCamera.Viewport.W);
}
```

- [ ] **Step 2: Write the failing dock integration test**

Replace the temporary render-order assertion in `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs` with this viewport-focused assertion:

```csharp
CameraComponent hierarchyContentCamera = GetPrivateField<CameraComponent>(sceneHierarchyPanel, "contentCameraComponent");

Assert.Equal(sceneHierarchyPanel.Position.X, hierarchyContentCamera.Viewport.X);
Assert.Equal(sceneHierarchyPanel.Position.Y + sceneHierarchyPanel.TitleBarHeightPixels, hierarchyContentCamera.Viewport.Y);
Assert.Equal(sceneHierarchyPanel.Size.X, hierarchyContentCamera.Viewport.Z);
Assert.Equal(sceneHierarchyPanel.Size.Y, hierarchyContentCamera.Viewport.W);
```

Also remove the assertions that compare hierarchy row render orders to `propertiesPanel` background render order.

- [ ] **Step 3: Run the Scene Hierarchy panel tests to verify they fail**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests" --no-restore -v minimal
```

Expected:
- FAIL because `EditorLayerMasks.SceneHierarchyContent` does not exist yet
- FAIL because `contentCameraComponent` does not exist on `SceneHierarchyPanel`

- [ ] **Step 4: Run the dock integration test to verify it fails**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~UpdateLayout_WhenCalled_DocksSceneHierarchyAbovePropertiesOnTheRightSide" --no-restore -v minimal
```

Expected:
- FAIL because `SceneHierarchyPanel` does not expose a content camera viewport yet

- [ ] **Step 5: Commit the failing tests**

```bash
git add engine/helengine.editor.tests/SceneHierarchyPanelTests.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs
git commit -m "test: add scene hierarchy viewport boundary regressions"
```

### Task 2: Add the Scene Hierarchy content layer and content camera

**Files:**
- Modify: `engine/helengine.editor/EditorLayerMasks.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyRow.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`

- [ ] **Step 1: Add the dedicated Scene Hierarchy content layer**

Update `engine/helengine.editor/EditorLayerMasks.cs` to add one new mask constant:

```csharp
/// <summary>
/// Layer mask used by Scene Hierarchy row visuals rendered inside the hierarchy viewport.
/// </summary>
public const ushort SceneHierarchyContent = 0b0000010000000000;
```

- [ ] **Step 2: Add the failing `SceneHierarchyPanel` camera fields and constructor wiring**

Add these members near the existing `contentRoot` field in `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`:

```csharp
/// <summary>
/// Hidden entity that owns the Scene Hierarchy content camera.
/// </summary>
readonly EditorEntity contentCameraEntity;
/// <summary>
/// Camera that renders only the Scene Hierarchy row layer inside the panel body viewport.
/// </summary>
readonly CameraComponent contentCameraComponent;
/// <summary>
/// Root entity that owns scrollable hierarchy row content rendered by the content camera.
/// </summary>
readonly EditorEntity scrollContentRoot;
```

Initialize them in the constructor:

```csharp
contentCameraEntity = new EditorEntity {
    InternalEntity = true,
    LayerMask = EditorLayerMasks.SceneHierarchyContent
};
contentCameraComponent = new CameraComponent {
    LayerMask = EditorLayerMasks.SceneHierarchyContent,
    CameraDrawOrder = 254,
    ClearSettings = new CameraClearSettings(false, new float4(0f, 0f, 0f, 0f), false, 1.0f, false, 0)
};
contentCameraEntity.AddComponent(contentCameraComponent);

scrollContentRoot = new EditorEntity();
scrollContentRoot.LayerMask = EditorLayerMasks.SceneHierarchyContent;
scrollContentRoot.Position = float3.Zero;
contentRoot.AddChild(scrollContentRoot);
```

- [ ] **Step 3: Implement the minimal viewport layout in `SceneHierarchyPanel`**

Add these helper methods to `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`:

```csharp
/// <summary>
/// Updates the content camera viewport to match the current panel body rectangle.
/// </summary>
void UpdateContentViewport() {
    float viewportX = Position.X;
    float viewportY = Position.Y + TitleBarHeightPixels;
    float viewportWidth = Math.Max(1, Size.X);
    float viewportHeight = Math.Max(1, Size.Y);
    contentCameraComponent.Viewport = new float4(viewportX, viewportY, viewportWidth, viewportHeight);
}

/// <summary>
/// Updates the scroll-content root position from the current item scroll offset.
/// </summary>
void UpdateScrollContentPosition() {
    scrollContentRoot.Position = new float3(0f, -(scrollComponent.ScrollOffset * GetRowHeightPixels()), 0.1f);
}
```

Call both from:
- the constructor after `RefreshHierarchy()`
- `OnSizeChanged()`
- `HandleUiMetricsApplied()`
- `HandleScrollOffsetChanged(...)`
- `RefreshHierarchy()` after scroll counts are updated

- [ ] **Step 4: Move row entities under the scroll-content root and assign the new layer**

Update `CreateRow()` in `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`:

```csharp
var rowEntity = new EditorEntity();
rowEntity.LayerMask = EditorLayerMasks.SceneHierarchyContent;
rowEntity.Position = float3.Zero;

var arrowHost = new EditorEntity();
arrowHost.LayerMask = EditorLayerMasks.SceneHierarchyContent;

var labelHost = new EditorEntity();
labelHost.LayerMask = EditorLayerMasks.SceneHierarchyContent;

scrollContentRoot.AddChild(rowEntity);
```

Keep the row-local Y positioning as `row.Entity.Position = new float3(0, nodeIndex * rowHeight, 0.1f);` because the scroll offset will now be applied by `scrollContentRoot`.

- [ ] **Step 5: Run the focused tests to verify they pass**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests" --no-restore -v minimal
```

Expected:
- PASS for the new layer-mask and viewport tests

- [ ] **Step 6: Commit the content-layer and camera work**

```bash
git add engine/helengine.editor/EditorLayerMasks.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/SceneHierarchyRow.cs engine/helengine.editor.tests/SceneHierarchyPanelTests.cs
git commit -m "feat: add scene hierarchy content viewport camera"
```

### Task 3: Make input and scrolling agree with the viewport boundary

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`

- [ ] **Step 1: Add the failing input-boundary regression**

Add this test to `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`:

```csharp
/// <summary>
/// Ensures rows outside the visible Scene Hierarchy viewport are not hit by pointer resolution.
/// </summary>
[Fact]
public void UpdateContextMenuInput_WhenPointerTargetsClippedOverflow_DoesNotResolveAHiddenRow() {
    for (int entityIndex = 0; entityIndex < 20; entityIndex++) {
        new EditorEntity {
            Name = $"Hierarchy {entityIndex}"
        };
    }

    SceneHierarchyPanel panel = new SceneHierarchyPanel(CreateFont()) {
        Position = new float3(32f, 40f, 0f),
        Size = new int2(320, 176)
    };

    panel.RefreshHierarchy();

    bool resolved = (bool)InvokePrivate(panel, "TryGetRowAtScreenPoint", new int2(48, 40 + DockableEntity.TitleBarHeight + 220), null);

    Assert.False(resolved);
}
```

If the private helper signature requires an `out` parameter wrapper in this codebase, adapt the invocation to the existing reflection helper pattern already used in editor tests. Do not introduce a new production helper for testing.

- [ ] **Step 2: Make row hit testing viewport-relative**

Update `TryGetRowAtScreenPoint(...)` and `ContainsHierarchyRowPoint(...)` in `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs` to use:
- panel-body screen rectangle checks first
- row bounds based on `scrollContentRoot.Position.Y + row.Entity.Position.Y`

Use this shape:

```csharp
float scrollContentY = scrollContentRoot.Position.Y;
float rowTop = Position.Y + TitleBarHeightPixels + scrollContentY + candidate.Entity.Position.Y;
float rowBottom = rowTop + GetRowHeightPixels();
```

Reject any point outside the content viewport before testing a row.

- [ ] **Step 3: Move focus auto-scroll to viewport-driven row visibility**

Keep `EnsureNodeVisible(...)`, but make it reason about the content viewport rather than the old visible-row-only pool assumption. The implementation can stay item-based:

```csharp
int visibleRowCount = Math.Max(1, GetVisibleRowCount());
int visibleEndIndex = scrollComponent.ScrollOffset + visibleRowCount - 1;
```

Do not add a second scroll abstraction. Keep `ScrollComponent` as the single source of item scroll offset.

- [ ] **Step 4: Run the focused Scene Hierarchy tests**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests" --no-restore -v minimal
```

Expected:
- PASS for row hit testing, row capping, and viewport input regressions

- [ ] **Step 5: Commit the input-boundary work**

```bash
git add engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor.tests/SceneHierarchyPanelTests.cs
git commit -m "fix: bind scene hierarchy input to clipped viewport"
```

### Task 4: Remove the temporary dock render-order workaround

**Files:**
- Modify: `engine/helengine.editor/components/ui/dock/DockableEntity.cs`
- Modify: `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Delete the temporary dock bias members from `DockableEntity`**

Remove these members and methods from `engine/helengine.editor/components/ui/dock/DockableEntity.cs`:

```csharp
internal const byte DockedRenderOrderBiasStep = 47;
internal const byte MaximumDockedRenderOrderBias = DockedRenderOrderBiasStep * 2;
byte dockedRenderOrderBias;
internal void SetDockedRenderOrderBias(byte bias) { ... }
```

Restore `ApplyRenderOrderBias()` to the simpler docked/floating split:

```csharp
int boost = 0;
if (!isDocked) {
    boost = RenderOrder2D.FloatingPanelBias;
}
```

- [ ] **Step 2: Delete the traversal-based dock bias application**

Remove `ApplyDockRenderOrderBiases()` and its call site from `engine/helengine.editor/managers/dock/DockLayoutEngine.cs`.

- [ ] **Step 3: Update the dock integration test to assert viewport clipping instead of render ordering**

In `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`, keep the right-side layout assertions and add:

```csharp
CameraComponent hierarchyContentCamera = GetPrivateField<CameraComponent>(sceneHierarchyPanel, "contentCameraComponent");

Assert.Equal(sceneHierarchyPanel.Position.X, hierarchyContentCamera.Viewport.X);
Assert.Equal(sceneHierarchyPanel.Position.Y + sceneHierarchyPanel.TitleBarHeightPixels, hierarchyContentCamera.Viewport.Y);
Assert.Equal(sceneHierarchyPanel.Size.X, hierarchyContentCamera.Viewport.Z);
Assert.Equal(sceneHierarchyPanel.Size.Y, hierarchyContentCamera.Viewport.W);
```

Remove the `propertiesBackground` / hierarchy-row render-order assertions entirely.

- [ ] **Step 4: Run the dock integration test**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~UpdateLayout_WhenCalled_DocksSceneHierarchyAbovePropertiesOnTheRightSide" --no-restore -v minimal
```

Expected:
- PASS with no dock render-order workaround remaining

- [ ] **Step 5: Commit the workaround removal**

```bash
git add engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs
git commit -m "refactor: remove scene hierarchy dock overdraw workaround"
```

### Task 5: Run the final regression sweep

**Files:**
- Test: `engine/helengine.editor.tests/SceneHierarchyPanelTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs`

- [ ] **Step 1: Run the focused Scene Hierarchy and dock tests together**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyPanelTests|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests" --no-restore -v minimal
```

Expected:
- PASS for the viewport-boundary, input, and dock-layout regressions

- [ ] **Step 2: Run the broader editor tests that touch Scene Hierarchy behavior**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneHierarchyReparentTests|FullyQualifiedName~EditorSessionAddMenuTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests" --no-restore -v minimal
```

Expected:
- PASS with no regressions in hierarchy selection, reparenting, or panel refresh behavior

- [ ] **Step 3: Commit the final verified state**

```bash
git add engine/helengine.editor/EditorLayerMasks.cs engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/SceneHierarchyRow.cs engine/helengine.editor/components/ui/dock/DockableEntity.cs engine/helengine.editor/managers/dock/DockLayoutEngine.cs engine/helengine.editor.tests/SceneHierarchyPanelTests.cs engine/helengine.editor.tests/EditorSessionKeyboardFocusIntegrationTests.cs
git commit -m "feat: clip scene hierarchy content to panel viewport"
```

## Self-Review

- Spec coverage:
  - Real Scene Hierarchy viewport boundary: covered by Tasks 2 and 3.
  - Scoped implementation, not global scroll view: covered by file scope and task scope.
  - Input and focus agreement with visible viewport: covered by Task 3.
  - Removal of workaround-based dock masking: covered by Task 4.
  - Regression coverage: covered by Tasks 1, 4, and 5.
- Placeholder scan:
  - No `TODO`, `TBD`, or “appropriate handling” placeholders remain.
  - Each code-changing step includes concrete code or exact removal targets.
- Type consistency:
  - Uses one consistent name for the new mask: `EditorLayerMasks.SceneHierarchyContent`.
  - Uses one consistent camera field name: `contentCameraComponent`.
  - Uses one consistent scroll content root name: `scrollContentRoot`.

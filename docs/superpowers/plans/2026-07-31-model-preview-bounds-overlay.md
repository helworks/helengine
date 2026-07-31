# Model Preview Bounds Overlay Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a model-preview toolbar button that cycles line-rendered bounding box, bounding sphere, and no bounds overlay.

**Architecture:** `ModelPreviewBoundsOverlayFactory` will build reusable line-list box and sphere runtime models and overlay material. `ModelPreviewSource` will position both entities around its cached bounds and expose a display-mode setter. `PreviewPanel` will own a panel-scoped mode and the second toolbar button.

**Tech Stack:** C#/.NET 9, HelEngine line-list models, editor overlay materials, XUnit, existing editor text controls.

---

### Task 1: Create the line-based bounds overlays

**Files:**
- Create: `engine/helengine.editor/managers/preview/ModelPreviewBoundsDisplayMode.cs`
- Create: `engine/helengine.editor/managers/preview/ModelPreviewBoundsOverlayFactory.cs`
- Modify: `engine/helengine.editor/managers/preview/ModelPreviewSource.cs`
- Test: `engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs`

- [ ] **Step 1: Write failing bounds-overlay tests**

Add tests that create a model preview with `BoundsMin = (-2, -1, -3)` and `BoundsMax = (4, 5, 3)` and assert:

```csharp
Assert.Equal(ModelPreviewBoundsDisplayMode.None, source.BoundsDisplayMode);
source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Box);
Assert.True(GetPrivateField<EditorEntity>(source, "boundsBoxEntity").Enabled);
Assert.False(GetPrivateField<EditorEntity>(source, "boundsSphereEntity").Enabled);

source.SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode.Sphere);
Assert.False(GetPrivateField<EditorEntity>(source, "boundsBoxEntity").Enabled);
Assert.True(GetPrivateField<EditorEntity>(source, "boundsSphereEntity").Enabled);
```

Inspect each overlay mesh component and assert its runtime model uses one `RuntimeSubmesh` with `PrimitiveTopology = ModelPrimitiveTopology.LineList`. Assert the box raw vertices contain the local corners `(-3, -3, -3)` and `(3, 3, 3)`, while the sphere raw vertices reach the source's calculated bounds radius around the centered preview origin.

- [ ] **Step 2: Run the focused test and verify it fails**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests"
```

Expected: compilation failure because `ModelPreviewBoundsDisplayMode`, `BoundsDisplayMode`, and `SetBoundsDisplayMode` do not exist.

- [ ] **Step 3: Implement reusable line overlays and mode selection**

Create `ModelPreviewBoundsDisplayMode` with `None = 0`, `Box = 1`, and `Sphere = 2`.

Create `ModelPreviewBoundsOverlayFactory` with:
- `CreateBox(RenderManager3D render3D, float3 halfExtents)`: generates 8 corners and the 12 edges (24 line-list indices).
- `CreateSphere(RenderManager3D render3D, float radius)`: generates three 32-segment great circles in XY, XZ, and YZ planes at the requested radius.
- `CreateLineRuntimeModel`: builds the raw model, then replaces its runtime submesh collection with `PrimitiveTopology = ModelPrimitiveTopology.LineList`.
- `CreateOverlayMaterial`: returns `EditorVisualMaterialFactory.CreateOverlayStandardMaterial()`.

In `ModelPreviewSource`, add `boundsBoxEntity`, `boundsSphereEntity`, and `BoundsDisplayModeValue`; create both entities as children of `previewEntity`. Center the box at the existing model origin with half-extents `(boundsMax - boundsMin) * 0.5`. Center the sphere at the same preview origin and create its line model at the scalar radius from `ResolveBoundsRadius`. Implement:

```csharp
public ModelPreviewBoundsDisplayMode BoundsDisplayMode => BoundsDisplayModeValue;

public void SetBoundsDisplayMode(ModelPreviewBoundsDisplayMode displayMode) {
    BoundsDisplayModeValue = displayMode;
    boundsBoxEntity.Enabled = displayMode == ModelPreviewBoundsDisplayMode.Box;
    boundsSphereEntity.Enabled = displayMode == ModelPreviewBoundsDisplayMode.Sphere;
}
```

Initialize the source in `None` mode. Both overlays must use line-list meshes, never solid triangles.

- [ ] **Step 4: Run the focused test and verify it passes**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests"
```

Expected: all `ModelPreviewSourceTests` pass.

- [ ] **Step 5: Commit the overlay renderer**

```powershell
git add -- engine/helengine.editor/managers/preview/ModelPreviewBoundsDisplayMode.cs engine/helengine.editor/managers/preview/ModelPreviewBoundsOverlayFactory.cs engine/helengine.editor/managers/preview/ModelPreviewSource.cs engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs
git commit -m "feat: add model preview bounds overlays"
```

### Task 2: Add the bounds-mode toolbar button

**Files:**
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Test: `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`

- [ ] **Step 1: Write failing button-cycle tests**

Add a Preview-panel test that uses a real `ModelPreviewSource`, gets `boundsButtonInteractable`, and activates it three times:

```csharp
Activate(boundsButtonInteractable);
Assert.Equal(ModelPreviewBoundsDisplayMode.Box, source.BoundsDisplayMode);
Activate(boundsButtonInteractable);
Assert.Equal(ModelPreviewBoundsDisplayMode.Sphere, source.BoundsDisplayMode);
Activate(boundsButtonInteractable);
Assert.Equal(ModelPreviewBoundsDisplayMode.None, source.BoundsDisplayMode);
```

Add this class-level test helper so each activation follows the real button event sequence:

```csharp
void Activate(InteractableComponent interactable) {
    interactable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Hover);
    interactable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Press);
    interactable.OnCursor(int2.Zero, int2.Zero, PointerInteraction.Release);
}
```

Add a source-replacement test: select sphere mode, replace the model source, and assert the replacement source uses sphere mode. Also assert the bounds button is disabled for a texture preview.

- [ ] **Step 2: Run the focused test and verify it fails**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests"
```

Expected: failure because `boundsButtonInteractable` and `CycleModelBoundsDisplayMode` do not exist.

- [ ] **Step 3: Add the panel control**

In `PreviewPanel`, create a second 22×18 button immediately after the grid button. Its foreground is the existing text-control type displaying the compact `B` bounds glyph, so no new raster icon or icon-loader contract is required. Maintain `ModelPreviewBoundsDisplayMode ModelBoundsDisplayModeValue`, initialized to `None`. Implement:

```csharp
public void CycleModelBoundsDisplayMode() {
    if (ModelBoundsDisplayModeValue == ModelPreviewBoundsDisplayMode.None) {
        ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.Box;
    } else if (ModelBoundsDisplayModeValue == ModelPreviewBoundsDisplayMode.Box) {
        ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.Sphere;
    } else {
        ModelBoundsDisplayModeValue = ModelPreviewBoundsDisplayMode.None;
    }

    if (ActivePreviewSourceValue is ModelPreviewSource modelPreviewSource) {
        modelPreviewSource.SetBoundsDisplayMode(ModelBoundsDisplayModeValue);
    }

    UpdateBoundsButtonVisuals();
}
```

Synchronize the active model source whenever the panel changes source. Extend toolbar hit testing and preview-input exclusion to cover the complete two-button toolbar. The grid and bounds modes remain independent.

- [ ] **Step 4: Run the focused test and verify it passes**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests"
```

Expected: all `PreviewPanelTests` pass.

- [ ] **Step 5: Commit the toolbar control**

```powershell
git add -- engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs
git commit -m "feat: add model preview bounds control"
```

### Task 3: Validate the editor target

**Files:**
- Verify only: all files above

- [ ] **Step 1: Run both preview suites**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests|FullyQualifiedName~PreviewPanelTests"
```

Expected: all focused preview tests pass.

- [ ] **Step 2: Build the Visual Studio Debug editor app**

```powershell
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -p:BuildingInsideVisualStudio=true
```

Expected: zero build errors.

- [ ] **Step 3: Check staged scope**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no unrelated files staged.

# Model Preview Toolbar Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a persistent, model-only Preview-panel toolbar whose first button toggles a bounds-sized floor grid.

**Architecture:** `ModelPreviewSource` owns an internal XZ grid entity in its offscreen scene, deriving a square side length from the largest horizontal model-bound extent with a 5-unit minimum. `PreviewPanel` owns toolbar presentation, click/focus handling, and the panel-scoped visibility preference; it forwards that state only to model preview sources.

**Tech Stack:** C#/.NET 9, HelEngine entities/components, XUnit, existing editor toolbar controls and built-in grid material.

---

### Task 1: Add the model-preview floor grid

**Files:**
- Create: `engine/helengine.editor/managers/preview/ModelPreviewGridFactory.cs`
- Modify: `engine/helengine.editor/managers/preview/ModelPreviewSource.cs`
- Test: `engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs`

- [ ] **Step 1: Write the failing model-grid tests**

Add tests that create a runtime model with known bounds and assert the public grid state and internal entity configuration:

```csharp
[Fact]
public void Constructor_WhenModelBoundsAreSmallerThanFiveUnits_CreatesFiveUnitPreviewGrid() {
    ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);

    EditorEntity gridEntity = GetPrivateField<EditorEntity>(source, "previewGridEntity");

    Assert.True(source.IsGridVisible);
    MeshComponent gridMesh = Assert.IsType<MeshComponent>(Assert.Single(gridEntity.Components));
    Assert.Equal(5f, gridMesh.Model.BoundsMax.X - gridMesh.Model.BoundsMin.X);
    Assert.Equal(float3.One, gridEntity.LocalScale);
    Assert.Equal(new float3(0f, -1.001f, 0f), gridEntity.LocalPosition);
    source.Dispose();
}

[Fact]
public void SetGridVisible_WhenDisabled_HidesThePreviewGridEntity() {
    ModelPreviewSource source = new ModelPreviewSource(CreateRuntimeModel(), Core.Instance.RenderManager3D);

    source.SetGridVisible(false);

    Assert.False(source.IsGridVisible);
    Assert.False(GetPrivateField<EditorEntity>(source, "previewGridEntity").Enabled);
    source.Dispose();
}
```

Add a wide-model test using `BoundsMin = new float3(-8f, 0f, -1f)` and `BoundsMax = new float3(8f, 4f, 1f)` that expects a 16-unit grid mesh width and a `-2.001f` grid Y position.

- [ ] **Step 2: Run test and verify it fails**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests"
```

Expected: compilation failure stating that `ModelPreviewSource` has no `IsGridVisible` or `SetGridVisible` member.

- [ ] **Step 3: Create the grid factory and wire it into `ModelPreviewSource`**

Create `ModelPreviewGridFactory` with `Create(RenderManager3D render3D, float sideLength)`. Validate the arguments, build `TransformGizmoMeshFactory.CreateCenteredPlaneSquare(sideLength)`, assign `EditorViewportGridMaterialFactory.Create(render3D)`, map the local XY plane to XZ with a +90° X-axis rotation, and return an internal `EditorEntity` on `EditorLayerMasks.SceneModelPreview`.

In `ModelPreviewSource`, add:

```csharp
readonly EditorEntity previewGridEntity;
bool IsGridVisibleValue;

public bool IsGridVisible => IsGridVisibleValue;

public void SetGridVisible(bool isVisible) {
    IsGridVisibleValue = isVisible;
    previewGridEntity.Enabled = isVisible;
}
```

During construction, create the grid with `ModelPreviewGridFactory.Create(renderManager3D, ResolvePreviewGridSize())`, add it to `previewEntity`, position it at `(0, boundsMin.Y - GetBoundsCenter().Y - 0.001f, 0)`, and initialize visibility to `true`. Implement:

```csharp
float ResolvePreviewGridSize() {
    float width = Math.Abs(boundsMax.X - boundsMin.X);
    float depth = Math.Abs(boundsMax.Z - boundsMin.Z);
    return Math.Max(5f, Math.Max(width, depth));
}
```

The factory mesh must use that resolved side length directly; do not use entity scaling, because the grid shader reads local vertex positions to generate its cell lines.

- [ ] **Step 4: Run test and verify it passes**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests"
```

Expected: all `ModelPreviewSourceTests` pass.

- [ ] **Step 5: Commit the grid rendering change**

```powershell
git add -- engine/helengine.editor/managers/preview/ModelPreviewGridFactory.cs engine/helengine.editor/managers/preview/ModelPreviewSource.cs engine/helengine.editor.tests/managers/preview/ModelPreviewSourceTests.cs
git commit -m "feat: add model preview grid"
```

### Task 2: Add the Preview-panel model toolbar and persist its grid preference

**Files:**
- Modify: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs`

- [ ] **Step 1: Write the failing toolbar tests**

Add focused tests that build a `PreviewPanel`, use a real `ModelPreviewSource`, and inspect the toolbar root through the existing reflection helper:

```csharp
[Fact]
public void SetPreviewSource_WhenModelPreviewIsAssigned_ShowsGridToolbar() {
    PreviewPanel panel = new PreviewPanel(CreateFont()) { Size = new int2(416, 312) };
    ModelPreviewSource source = CreateModelPreviewSource();

    panel.SetPreviewSource(source);

    Assert.True(GetPrivateField<EditorEntity>(panel, "modelToolbarRoot").Enabled);
}

[Fact]
public void ToggleModelGrid_WhenNewModelPreviewIsAssigned_PreservesPanelGridPreference() {
    PreviewPanel panel = new PreviewPanel(CreateFont());
    ModelPreviewSource first = CreateModelPreviewSource();
    ModelPreviewSource second = CreateModelPreviewSource();
    panel.SetPreviewSource(first);

    panel.ToggleModelGrid();
    panel.SetPreviewSource(second);

    Assert.False(first.IsGridVisible);
    Assert.False(second.IsGridVisible);
}
```

Add a non-model source test that asserts `modelToolbarRoot.Enabled` is false for `TexturePreviewSource`, and a pointer test that clicks the grid button then asserts the model source’s grid is false while its interaction handler receives no drag.

- [ ] **Step 2: Run test and verify it fails**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests"
```

Expected: compilation failure stating that `PreviewPanel` has no `ToggleModelGrid` member or the `modelToolbarRoot` field cannot be found.

- [ ] **Step 3: Implement the model-only toolbar and panel-scoped state**

Add compact toolbar constants and fields to `PreviewPanel`: `modelToolbarRoot`, `modelToolbarBackground`, `gridButtonRoot`, `gridButtonBackground`, `gridButtonIcon`, `gridButtonInteractable`, focus target, hover/pressed/focused flags, and `IsModelGridVisibleValue = true`.

Create the toolbar under `contentRoot` using the existing 24px-high / 22px-wide viewport-toolbar convention. Give the button an `InteractableComponent` and register its focus target. Both pointer release and Enter/Space activation call:

```csharp
public void ToggleModelGrid() {
    IsModelGridVisibleValue = !IsModelGridVisibleValue;
    if (ActivePreviewSourceValue is ModelPreviewSource modelPreviewSource) {
        modelPreviewSource.SetGridVisible(IsModelGridVisibleValue);
    }

    UpdateModelGridButtonVisuals();
}
```

When assigning a source, enable the toolbar only for `ModelPreviewSource`, call `SetGridVisible(IsModelGridVisibleValue)` for that source, and disable it for texture/camera/empty previews. Update panel layout so the generic model preview sprite starts below the toolbar and its source is resized to the remaining content height. Route points inside the toolbar to its interactable/focus group before preview interaction handling, so a grid-button click cannot become an orbit or pan gesture.

Add a constructor overload accepting a toolbar grid-icon texture, keep the existing public constructors as compatibility wrappers, and pass `ViewportToolbarIcons.GridIcon` from both `EditorSession` preview-panel creation sites. The compatibility overloads use `TextureUtils.PixelTexture` for isolated tests that do not construct the session icon set.

- [ ] **Step 4: Run test and verify it passes**

Run:

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~PreviewPanelTests"
```

Expected: all `PreviewPanelTests` pass, including toolbar visibility, persistence, and pointer-isolation coverage.

- [ ] **Step 5: Commit the toolbar change**

```powershell
git add -- engine/helengine.editor/components/ui/PreviewPanel.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/components/ui/PreviewPanelTests.cs
git commit -m "feat: add model preview toolbar"
```

### Task 3: Validate the editor integration

**Files:**
- Verify only: `engine/helengine.editor/managers/preview/ModelPreviewGridFactory.cs`
- Verify only: `engine/helengine.editor/managers/preview/ModelPreviewSource.cs`
- Verify only: `engine/helengine.editor/components/ui/PreviewPanel.cs`
- Verify only: `engine/helengine.editor/EditorSession.cs`

- [ ] **Step 1: Run the combined focused suite**

```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ModelPreviewSourceTests|FullyQualifiedName~PreviewPanelTests"
```

Expected: all focused model-preview and Preview-panel tests pass.

- [ ] **Step 2: Build the Visual Studio debug target without restoring**

```powershell
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -p:BuildingInsideVisualStudio=true
```

Expected: build succeeds with zero errors. Report pre-existing warnings separately if they remain.

- [ ] **Step 3: Inspect the scoped worktree**

```powershell
git diff --check
git status --short
```

Expected: no whitespace errors and no unrelated modified or untracked files staged by this work.

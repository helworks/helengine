# Properties Panel Scroll Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the entire `PropertiesPanel` body scroll as one document so overflowing asset and entity properties stay inside the panel and remain reachable.

**Architecture:** Keep a fixed viewport root under the dock title bar, add a shared `ScrollComponent` to that fixed viewport, and move all existing body content under a new scrolling document root. Treat scroll offsets as pixel units by mapping `ScrollComponent.ItemCount` to total content height and `VisibleItemCount` to viewport height, while `ComponentPropertiesView` exposes its measured height so `PropertiesPanel` can compute one total document bottom for both asset and entity modes.

**Tech Stack:** C# / .NET 9, editor UI entities (`DockableEntity`, `EditorEntity`), `ScrollComponent`, xUnit.

---

> **Prerequisite:** The current workspace has an unrelated compile failure in `engine/helengine.core/managers/ObjectManager.cs` around `IDrawable2D.LayerMask`. Clear or rebase that issue before running the red/green commands below, otherwise the targeted tests will stop before reaching the new Properties panel assertions.

## File Structure

- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  - Split the current body into a fixed viewport host plus a scrolling document root.
  - Add one panel-owned `ScrollComponent`.
  - Compute total document height from visible sections.
  - Reset and clamp scroll state when the selection context changes.

- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
  - Track the measured layout height of the visible component sections.
  - Reset that measurement when the view is hidden or cleared.

- Modify: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
  - Add regression tests for scroll ownership, overflow range, content movement, asset-mode overflow, and scroll reset.
  - Add test helpers for an overflow-heavy entity and import-settings fixtures.

---

### Task 1: Add Failing Properties Panel Scroll Regressions

**Files:**
- Modify: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Write the failing scroll regressions**

Add these tests and helpers to `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`:

```csharp
/// <summary>
/// Ensures the shared Properties panel body owns one scroll controller when entity content exceeds the viewport.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenVisibleSectionsExceedViewport_CreatesContentScrollRange() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    panel.Size = new int2(240, 120);
    EditorEntity entity = CreateEntityWithOverflowComponents(8);

    panel.ShowEntityProperties(entity);

    ScrollComponent contentScrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");

    Assert.True(contentScrollComponent.MaximumScrollOffset > 0);
}

/// <summary>
/// Ensures changing the shared scroll offset moves the Properties panel document root upward.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenContentScrollOffsetChanges_RepositionsScrollContentRoot() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    panel.Size = new int2(240, 120);
    EditorEntity entity = CreateEntityWithOverflowComponents(8);

    panel.ShowEntityProperties(entity);

    ScrollComponent contentScrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");
    EditorEntity scrollContentRoot = GetPrivateField<EditorEntity>(panel, "ScrollContentRoot");
    float initialY = scrollContentRoot.LocalPosition.Y;

    Assert.True(contentScrollComponent.ScrollTo(24));

    Assert.Equal(initialY - 24f, scrollContentRoot.LocalPosition.Y);
}

/// <summary>
/// Ensures asset-mode controls also participate in the shared panel scroll range.
/// </summary>
[Fact]
public void ShowImportSettings_WhenAssetControlsExceedViewport_CreatesContentScrollRange() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    panel.Size = new int2(240, 72);

    panel.ShowImportSettings(
        CreateImportEntry(),
        CreateImportSettings(false, true),
        ["assimp", "custom"],
        ["windows", "android"],
        "windows");

    ScrollComponent contentScrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");

    Assert.True(contentScrollComponent.MaximumScrollOffset > 0);
}

/// <summary>
/// Ensures switching to a different entity resets the shared Properties panel scroll offset.
/// </summary>
[Fact]
public void ShowEntityProperties_WhenContextChanges_ResetsContentScrollOffset() {
    PropertiesPanel panel = new PropertiesPanel(CreateFont(), new ContentManager(TempRootPath));
    panel.Size = new int2(240, 120);
    EditorEntity firstEntity = CreateEntityWithOverflowComponents(8);
    EditorEntity secondEntity = CreateEntityWithOverflowComponents(2);

    panel.ShowEntityProperties(firstEntity);

    ScrollComponent contentScrollComponent = GetPrivateField<ScrollComponent>(panel, "ContentScrollComponent");
    EditorEntity scrollContentRoot = GetPrivateField<EditorEntity>(panel, "ScrollContentRoot");
    Assert.True(contentScrollComponent.ScrollTo(24));

    panel.ShowEntityProperties(secondEntity);

    Assert.Equal(0, contentScrollComponent.ScrollOffset);
    Assert.Equal(0f, scrollContentRoot.LocalPosition.Y);
}

EditorEntity CreateEntityWithOverflowComponents(int componentCount) {
    EditorEntity entity = new EditorEntity {
        Name = "Overflow"
    };

    for (int index = 0; index < componentCount; index++) {
        entity.AddComponent(new TestOverflowComponent {
            Value = index + 1
        });
    }

    return entity;
}

AssetBrowserEntry CreateImportEntry() {
    return AssetBrowserEntry.CreateFileSystemFile(
        "Sponza.obj",
        "Models/Sponza.obj",
        Path.Combine(TempRootPath, "Models", "Sponza.obj"),
        ".obj",
        AssetEntryKind.Model);
}

AssetImportSettings CreateImportSettings(bool windowsFlipWinding, bool androidFlipWinding) {
    AssetImportSettings settings = new AssetImportSettings();
    settings.Importer.ImporterId = "assimp";
    settings.Importer.SourceChecksum = "checksum";
    settings.Importer.AssetId = "asset-id";
    settings.Processor.Platforms["windows"] = new AssetPlatformProcessorSettings {
        Model = new ModelAssetProcessorSettings {
            FlipWinding = windowsFlipWinding
        }
    };
    settings.Processor.Platforms["android"] = new AssetPlatformProcessorSettings {
        Model = new ModelAssetProcessorSettings {
            FlipWinding = androidFlipWinding
        }
    };
    return settings;
}

public sealed class TestOverflowComponent : Component {
    /// <summary>
    /// Gets or sets one scalar value so the component view renders at least one editable row.
    /// </summary>
    public float Value { get; set; }
}
```

- [ ] **Step 2: Run the focused Properties panel tests to verify they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PropertiesPanelComponentShellTests -v minimal
```

Expected:

- `FAIL` because `PropertiesPanel` does not yet expose `ContentScrollComponent` or `ScrollContentRoot`.
- `FAIL` because the panel currently computes no shared scroll range, so `MaximumScrollOffset` stays `0`.

- [ ] **Step 3: Commit the failing tests only**

```bash
rtk git add engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs
rtk git commit -m "test: add properties panel scroll regressions"
```

### Task 2: Expose Measured Component Height For The Shared Document Layout

**Files:**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Re-run the Properties panel regressions and confirm `ComponentPropertiesView` still has no measurable height for the panel**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenVisibleSectionsExceedViewport_CreatesContentScrollRange -v minimal
```

Expected:

- `FAIL` because `PropertiesPanel` still cannot size a shared scroll document from component-section output.

- [ ] **Step 2: Add a measured `Height` property to `ComponentPropertiesView`**

Update `engine/helengine.editor/components/ui/ComponentPropertiesView.cs` with a tracked layout height:

```csharp
/// <summary>
/// Height of the currently visible component document.
/// </summary>
int LayoutHeightValue;

/// <summary>
/// Gets the measured height of the currently visible component document.
/// </summary>
public int Height => LayoutHeightValue;
```

Reset it when the view is cleared:

```csharp
public void ShowComponents(Entity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    LayoutHeightValue = 0;
    ClearActiveRows();
    ClearActiveSections();
    if (entity.Components == null || entity.Components.Count == 0) {
        RootEntity.Enabled = false;
        return;
    }

    RootEntity.Enabled = true;
    ...
}

public void Hide() {
    ClearActiveRows();
    ClearActiveSections();
    LayoutHeightValue = 0;
    RootEntity.Enabled = false;
}
```

Measure it at the end of `UpdateLayout`:

```csharp
public void UpdateLayout(int left, int top, int maxWidth) {
    if (!RootEntity.Enabled) {
        LayoutHeightValue = 0;
        return;
    }

    RootEntity.Position = new float3(left, top, 0.2f);
    int width = Math.Max(0, maxWidth);
    int y = 0;
    for (int i = 0; i < ActiveSections.Count; i++) {
        ComponentSectionView section = ActiveSections[i];
        LayoutSectionHeader(section, width, y);
        y += SectionHeaderHeight;

        if (!section.IsCollapsed) {
            y += SectionHeaderSpacing;
            for (int rowIndex = 0; rowIndex < section.Rows.Count; rowIndex++) {
                ComponentPropertyRow row = section.Rows[rowIndex];
                int rowHeight = row.Kind == ComponentPropertyRowKind.Header ? HeaderHeight : RowHeight;
                row.Entity.Enabled = true;
                LayoutRow(row, width, y, rowHeight);
                y += rowHeight + RowSpacing;
            }
        } else {
            SetSectionRowsEnabled(section, false);
        }

        y += SectionSpacing;
    }

    LayoutHeightValue = ActiveSections.Count == 0 ? 0 : Math.Max(0, y - SectionSpacing);
}
```

- [ ] **Step 3: Run the same focused regression to verify the height plumbing compiles, but the panel still fails without scroll integration**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenVisibleSectionsExceedViewport_CreatesContentScrollRange -v minimal
```

Expected:

- `FAIL` because `PropertiesPanel` still does not own the shared scroll viewport/document structure.
- No new failures from `ComponentPropertiesView` height measurement itself.

- [ ] **Step 4: Commit the measured-height support**

```bash
rtk git add engine/helengine.editor/components/ui/ComponentPropertiesView.cs
rtk git commit -m "feat: expose component properties layout height"
```

### Task 3: Add The Shared Scroll Viewport And Document Root To PropertiesPanel

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`

- [ ] **Step 1: Re-run the full Properties panel regression class before integrating the scroll viewport**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~PropertiesPanelComponentShellTests -v minimal
```

Expected:

- `FAIL` on the new scroll regressions because the panel still renders a single unbounded body.

- [ ] **Step 2: Split the panel body into a fixed viewport root and a movable scroll document**

Add these fields to `engine/helengine.editor/components/ui/PropertiesPanel.cs`:

```csharp
/// <summary>
/// Shared scroll controller for the Properties panel body.
/// </summary>
readonly ScrollComponent ContentScrollComponent;

/// <summary>
/// Scrollable document root that owns all panel body content.
/// </summary>
readonly EditorEntity ScrollContentRoot;
```

Replace the current constructor body-root setup with a fixed viewport root plus a scrolling document root:

```csharp
contentRoot = new EditorEntity();
contentRoot.LayerMask = LayerMask;
contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
AddChild(contentRoot);

ScrollContentRoot = new EditorEntity();
ScrollContentRoot.LayerMask = LayerMask;
ScrollContentRoot.Position = new float3(0f, 0f, 0.1f);
contentRoot.AddChild(ScrollContentRoot);

ContentScrollComponent = new ScrollComponent();
ContentScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
ContentScrollComponent.ScrollStepCount = Math.Max(1, UiMetrics.ScalePixels(24));
ContentScrollComponent.ScrollOffsetChanged += HandleContentScrollOffsetChanged;
contentRoot.AddComponent(ContentScrollComponent);
```

Reparent body content under `ScrollContentRoot` instead of `contentRoot`:

```csharp
ScrollContentRoot.AddChild(importSettingsView.Root);
ScrollContentRoot.AddChild(MaterialView.Root);
ScrollContentRoot.AddChild(TransformRoot);
ScrollContentRoot.AddChild(ComponentView.Root);
ScrollContentRoot.AddChild(AddComponentButtonRoot);
```

Also update `AddLine()` so each generated line host is attached to `ScrollContentRoot`:

```csharp
TextComponent AddLine() {
    var host = new EditorEntity();
    host.LayerMask = LayerMask;
    host.Position = float3.Zero;
    ScrollContentRoot.AddChild(host);
    ...
}
```

- [ ] **Step 3: Add the shared scroll-state and content-position helpers**

Add these methods to `PropertiesPanel.cs`:

```csharp
/// <summary>
/// Repositions the scrolling document from the current pixel scroll offset.
/// </summary>
void UpdateScrollContentPosition() {
    ScrollContentRoot.Position = new float3(0f, -ContentScrollComponent.ScrollOffset, 0.1f);
}

/// <summary>
/// Updates the scroll viewport and pixel-based document range for the current layout pass.
/// </summary>
/// <param name="contentHeight">Measured document height in pixels.</param>
void UpdateContentScrollState(int contentHeight) {
    int viewportWidth = Math.Max(1, Size.X);
    int viewportHeight = Math.Max(1, Size.Y);
    int safeContentHeight = Math.Max(viewportHeight, contentHeight);

    ContentScrollComponent.Size = new int2(viewportWidth, viewportHeight);
    ContentScrollComponent.ItemCount = safeContentHeight;
    ContentScrollComponent.VisibleItemCount = viewportHeight;
    ContentScrollComponent.ScrollStepCount = Math.Max(1, UiMetrics.ScalePixels(24));
    ContentScrollComponent.ClampScrollOffset();
    UpdateScrollContentPosition();
}

/// <summary>
/// Resets the shared panel scroll offset when the visible context changes.
/// </summary>
void ResetContentScroll() {
    ContentScrollComponent.ResetScrollOffset();
    UpdateScrollContentPosition();
}

/// <summary>
/// Reapplies the document transform after one scroll-offset change.
/// </summary>
/// <param name="scrollComponent">Scroll owner that raised the event.</param>
/// <param name="scrollOffset">Current pixel offset.</param>
void HandleContentScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
    UpdateScrollContentPosition();
}
```

Update metric and size hooks:

```csharp
protected override void OnSizeChanged() {
    base.OnSizeChanged();
    if (!isInitialized) {
        return;
    }

    LayoutLines();
}

protected override void HandleUiMetricsApplied() {
    MinSize = new int2(UiMetrics.ScalePixels(220), UiMetrics.ScalePixels(160));
    contentRoot.Position = new float3(0f, TitleBarHeightPixels, 0.05f);
    ContentScrollComponent.ScrollStepCount = Math.Max(1, UiMetrics.ScalePixels(24));
    UpdateScrollContentPosition();
}
```

- [ ] **Step 4: Run the two structural scroll tests to verify the viewport/document scaffolding now works**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenVisibleSectionsExceedViewport_CreatesContentScrollRange|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenContentScrollOffsetChanges_RepositionsScrollContentRoot" -v minimal
```

Expected:

- `PASS` for both structural entity scroll tests.
- `FAIL` still remaining for asset-mode overflow and scroll reset until the context-reset and total-height logic is finished.

- [ ] **Step 5: Commit the shared viewport/document split**

```bash
rtk git add engine/helengine.editor/components/ui/PropertiesPanel.cs
rtk git commit -m "feat: add shared properties panel scroll viewport"
```

### Task 4: Finish Content Height Accounting, Asset Coverage, And Scroll Reset

**Files:**
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs`
- Test: `engine/helengine.editor.tests/PropertiesPanelMutationTests.cs`

- [ ] **Step 1: Re-run the remaining scroll regressions to verify the unresolved gaps**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests.ShowImportSettings_WhenAssetControlsExceedViewport_CreatesContentScrollRange|FullyQualifiedName~PropertiesPanelComponentShellTests.ShowEntityProperties_WhenContextChanges_ResetsContentScrollOffset" -v minimal
```

Expected:

- `FAIL` because `LayoutLines()` does not yet compute a full shared document height for every visible mode.
- `FAIL` because context-switch methods do not yet reset the shared scroll state.

- [ ] **Step 2: Make `LayoutLines()` compute one total document bottom and feed the shared `ScrollComponent`**

Replace the tail of `LayoutLines()` in `PropertiesPanel.cs` with a content-height-aware layout:

```csharp
void LayoutLines() {
    int rowWidth = Math.Max(Size.X, MinSize.X);
    int maxWidth = Math.Max(0, rowWidth - ContentPadding * 2);
    float lineHeight = (float)Math.Max((double)font.LineHeight, 1.0);

    float offsetY = ContentTopMargin;
    for (int i = 0; i < lineTexts.Count; i++) {
        TextComponent text = lineTexts[i];
        EditorEntity host = lineHosts[i];
        if (string.IsNullOrWhiteSpace(text.Text)) {
            host.Enabled = false;
            continue;
        }

        host.Enabled = true;
        host.Position = new float3(ContentPadding, (float)Math.Round(offsetY), 0.2f);
        text.Size = new int2(maxWidth, (int)Math.Ceiling(lineHeight));
        offsetY += lineHeight + LineSpacing;
    }

    int contentBottom = (int)Math.Ceiling(offsetY);

    if (importSettingsView.IsVisible) {
        int viewTop = (int)Math.Round(offsetY);
        importSettingsView.UpdateLayout(ContentPadding, viewTop, maxWidth);
        contentBottom = Math.Max(contentBottom, viewTop + importSettingsView.Height);
    }

    if (MaterialView.IsVisible) {
        int viewTop = (int)Math.Round(offsetY);
        MaterialView.UpdateLayout(ContentPadding, viewTop, maxWidth);
        contentBottom = Math.Max(contentBottom, viewTop + MaterialView.Height);
    }

    if (ShowTransformControls) {
        int transformTop = (int)Math.Round(offsetY);
        UpdateTransformLayout(transformTop, maxWidth);

        int addComponentTop = transformTop + GetTransformSectionHeight() + ComponentSectionSpacing;
        LayoutAddComponentButton(addComponentTop, maxWidth);
        contentBottom = Math.Max(contentBottom, addComponentTop + AddComponentButtonHeight);

        int componentTop = addComponentTop + AddComponentButtonHeight + AddComponentListSpacing;
        ComponentView.UpdateLayout(0, componentTop, rowWidth);
        contentBottom = Math.Max(contentBottom, componentTop + ComponentView.Height);
    } else {
        TransformRoot.Enabled = false;
        AddComponentButtonRoot.Enabled = false;
        ComponentView.Hide();
    }

    UpdateContentScrollState(contentBottom + ContentPadding);
}
```

- [ ] **Step 3: Reset the shared scroll offset at every context transition**

Call `ResetContentScroll()` at the start of each context-switch method in `PropertiesPanel.cs`:

```csharp
public void ShowImportSettings(...) {
    ...
    ResetContentScroll();
    currentEntry = entry;
    ...
}

public void ShowImportError(AssetBrowserEntry entry, string message) {
    ...
    ResetContentScroll();
    currentEntry = null;
    ...
}

public void ShowEmpty() {
    ResetContentScroll();
    currentEntry = null;
    ...
}

public void ShowMaterialSettings(...) {
    ...
    ResetContentScroll();
    currentEntry = entry;
    ...
}

public void ShowGeneratedAssetSummary(AssetBrowserEntry entry) {
    ...
    ResetContentScroll();
    currentEntry = null;
    ...
}

public void ShowSceneAssetSummary(AssetBrowserEntry entry) {
    ...
    ResetContentScroll();
    currentEntry = null;
    ...
}

public void ShowEntityProperties(Entity entity) {
    if (entity == null) {
        throw new ArgumentNullException(nameof(entity));
    }

    ResetContentScroll();
    currentEntry = null;
    HideRemoveComponentDialog();
    importSettingsView.Hide();
    MaterialView.Hide();
    SelectedEntity = entity;
    ApplyLines(Array.Empty<string>());
    SyncTransformFields(entity);
    ComponentView.ShowComponents(entity);
    SetTransformVisible(true);
    LayoutLines();
}
```

- [ ] **Step 4: Run the focused Properties panel and supporting regression suite to verify the full feature**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PropertiesPanelComponentShellTests|FullyQualifiedName~PropertiesPanelMutationTests|FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~ScrollComponentTests" -v minimal
```

Expected:

- `PASS` for the new scroll regressions.
- `PASS` for existing Properties panel mutation tests.
- `PASS` for existing asset-import-settings and generic scroll tests.

- [ ] **Step 5: Commit the completed Properties panel scroll integration**

```bash
rtk git add engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/PropertiesPanelComponentShellTests.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs
rtk git commit -m "feat: scroll properties panel content"
```

## Self-Review

- Spec coverage:
  - Entire panel body scrolls: implemented by Tasks 3 and 4.
  - Asset and entity modes share one scroll owner: covered by Task 1 asset/entity tests and Task 4 layout accounting.
  - Scroll resets on context change: covered by Task 1 reset test and Task 4 reset wiring.
  - Modal host stays outside scroll tree: preserved by Task 3 file structure and no `ModalHost` reparenting.

- Placeholder scan:
  - No `TODO`, `TBD`, or “appropriate handling” placeholders remain.
  - Each task names exact files, commands, and code changes.

- Type consistency:
  - Shared field names stay consistent across tasks: `ContentScrollComponent`, `ScrollContentRoot`, `Height`, `ResetContentScroll`, `UpdateContentScrollState`, `HandleContentScrollOffsetChanged`.


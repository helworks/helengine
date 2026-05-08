# Platform Tab Strip Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extract a reusable scrollable platform-tab strip for the editor, integrate it into the asset processor, and support overflow arrows plus keyboard-driven auto-reveal.

**Architecture:** Add a new editor-owned `PlatformTabStripView` that composes `TabComponent` instances and arrow buttons, owns overflow state and selection reveal, and exposes a narrow API for host screens. Migrate `AssetImportSettingsView` to consume the shared strip while leaving `BuildDialog` unchanged but compatible with the new API shape.

**Tech Stack:** C#, helengine editor UI entities/components, existing `TabComponent`, xUnit editor tests

---

### Task 1: Add Failing Tests For Shared Platform Tab Strip Behavior

**Files:**
- Create: `engine/helengine.editor.tests/PlatformTabStripViewTests.cs`
- Test: `engine/helengine.editor.tests/PlatformTabStripViewTests.cs`

- [ ] **Step 1: Write the failing test file**

```csharp
namespace helengine.editor.tests {
    /// <summary>
    /// Verifies the shared platform tab strip behavior.
    /// </summary>
    public sealed class PlatformTabStripViewTests {
        /// <summary>
        /// Ensures one tab is created for each supplied platform id.
        /// </summary>
        [Fact]
        public void SetPlatforms_WhenPlatformsAreProvided_CreatesOneTabPerPlatform() {
            FontAsset font = TestFontFactory.Create();
            PlatformTabStripView view = new PlatformTabStripView(font, 1, 88, 24, 6, 24);

            view.SetPlatforms(new[] { "windows", "ps2", "linux" }, "windows", _ => { });

            Assert.Equal(3, view.TabCount);
        }

        /// <summary>
        /// Ensures overflow arrows activate when the tabs exceed the available width.
        /// </summary>
        [Fact]
        public void UpdateLayout_WhenTabsOverflow_EnablesOverflowArrows() {
            FontAsset font = TestFontFactory.Create();
            PlatformTabStripView view = new PlatformTabStripView(font, 1, 88, 24, 6, 24);

            view.SetPlatforms(new[] { "windows", "ps2", "linux", "gamecube", "wii" }, "windows", _ => { });
            view.UpdateLayout(0, 0, 220);

            Assert.True(view.CanScrollRight);
        }

        /// <summary>
        /// Ensures selecting a clipped tab scrolls the strip until the tab becomes visible.
        /// </summary>
        [Fact]
        public void SetSelectedPlatform_WhenSelectedTabIsClipped_RevealsTheTab() {
            FontAsset font = TestFontFactory.Create();
            PlatformTabStripView view = new PlatformTabStripView(font, 1, 88, 24, 6, 24);

            view.SetPlatforms(new[] { "windows", "ps2", "linux", "gamecube", "wii" }, "windows", _ => { });
            view.UpdateLayout(0, 0, 220);

            view.SetSelectedPlatform("wii");

            Assert.True(view.HorizontalScrollOffset > 0);
            Assert.True(view.IsPlatformFullyVisible("wii"));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PlatformTabStripViewTests"`

Expected: FAIL because `PlatformTabStripView` does not exist yet.

- [ ] **Step 3: Commit the failing test**

```bash
git add engine/helengine.editor.tests/PlatformTabStripViewTests.cs
git commit -m "test: add platform tab strip view coverage"
```

### Task 2: Implement Shared Platform Tab Strip View

**Files:**
- Create: `engine/helengine.editor/components/ui/PlatformTabStripView.cs`
- Modify: `engine/helengine.core/components/2d/interactable/TabComponent.cs`
- Test: `engine/helengine.editor.tests/PlatformTabStripViewTests.cs`

- [ ] **Step 1: Create the shared strip view skeleton**

```csharp
namespace helengine.editor {
    /// <summary>
    /// Renders a reusable overflow-aware platform tab row for editor surfaces.
    /// </summary>
    public class PlatformTabStripView {
        /// <summary>
        /// Initializes a new platform tab strip view.
        /// </summary>
        public PlatformTabStripView(FontAsset font, ushort layerMask, int tabWidth, int tabHeight, int tabSpacing, int arrowButtonWidth) {
        }

        /// <summary>
        /// Gets the root entity that owns the tab strip visuals.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the number of currently rendered tabs.
        /// </summary>
        public int TabCount { get; }

        /// <summary>
        /// Gets the current horizontal scroll offset in pixels.
        /// </summary>
        public int HorizontalScrollOffset { get; }

        /// <summary>
        /// Gets a value indicating whether the strip can scroll left.
        /// </summary>
        public bool CanScrollLeft { get; }

        /// <summary>
        /// Gets a value indicating whether the strip can scroll right.
        /// </summary>
        public bool CanScrollRight { get; }

        /// <summary>
        /// Rebuilds the tab row for the supplied platforms and selected id.
        /// </summary>
        public void SetPlatforms(IReadOnlyList<string> platformIds, string selectedPlatformId, Action<string> selectionChanged) {
        }

        /// <summary>
        /// Updates the selected platform and reveals it if needed.
        /// </summary>
        public void SetSelectedPlatform(string platformId) {
        }

        /// <summary>
        /// Updates the strip layout using the supplied top-left position and available width.
        /// </summary>
        public void UpdateLayout(int left, int top, int width) {
        }

        /// <summary>
        /// Returns whether one platform tab is fully visible inside the viewport.
        /// </summary>
        public bool IsPlatformFullyVisible(string platformId) {
            return false;
        }
    }
}
```

- [ ] **Step 2: Implement minimal overflow, arrows, and reveal behavior**

```csharp
public void SetPlatforms(IReadOnlyList<string> platformIds, string selectedPlatformId, Action<string> selectionChanged) {
    if (platformIds == null) {
        throw new ArgumentNullException(nameof(platformIds));
    } else if (platformIds.Count == 0) {
        throw new ArgumentException("At least one platform id must be provided.", nameof(platformIds));
    } else if (selectionChanged == null) {
        throw new ArgumentNullException(nameof(selectionChanged));
    }

    ClearTabs();
    SelectionChanged = selectionChanged;
    SelectedPlatformId = selectedPlatformId;

    for (int i = 0; i < platformIds.Count; i++) {
        string platformId = platformIds[i];
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform ids must be provided.", nameof(platformIds));
        }

        AddTab(platformId);
    }

    UpdateSelectedVisualState();
    RevealSelectedPlatform();
    UpdateOverflowState();
}

public void SetSelectedPlatform(string platformId) {
    if (!PlatformIndexById.TryGetValue(platformId, out int _)) {
        throw new InvalidOperationException("The requested platform tab does not exist.");
    }

    SelectedPlatformId = platformId;
    UpdateSelectedVisualState();
    RevealSelectedPlatform();
    UpdateOverflowState();
}

public void UpdateLayout(int left, int top, int width) {
    if (width <= 0) {
        throw new ArgumentOutOfRangeException(nameof(width), "Layout width must be positive.");
    }

    Root.Position = new float3(left, top, 0.1f);
    ViewportWidth = width;
    LayoutArrowButtons();
    LayoutTabHosts();
    RevealSelectedPlatform();
    UpdateOverflowState();
}
```

- [ ] **Step 3: Add keyboard reveal support by using selection updates as the reveal path**

```csharp
void HandleTabClicked(string platformId) {
    SetSelectedPlatform(platformId);
    SelectionChanged(platformId);
}

void HandleArrowLeftClicked() {
    HorizontalScrollOffset = Math.Max(0, HorizontalScrollOffset - GetScrollStepPixels());
    LayoutTabHosts();
    UpdateOverflowState();
}

void HandleArrowRightClicked() {
    HorizontalScrollOffset = Math.Min(GetMaximumScrollOffsetPixels(), HorizontalScrollOffset + GetScrollStepPixels());
    LayoutTabHosts();
    UpdateOverflowState();
}
```

- [ ] **Step 4: Update `TabComponent` only if the shared strip needs one narrow focus hook**

```csharp
/// <summary>
/// Raises when the tab becomes selected through user interaction.
/// </summary>
public event Action Selected;

void HandleClicked() {
    if (Clicked != null) {
        Clicked();
    }

    if (Selected != null) {
        Selected();
    }
}
```

Use this step only if the strip cannot reuse the existing click path cleanly. Do not redesign `TabComponent`.

- [ ] **Step 5: Run the new strip tests**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PlatformTabStripViewTests"`

Expected: PASS

- [ ] **Step 6: Commit the shared strip implementation**

```bash
git add engine/helengine.editor/components/ui/PlatformTabStripView.cs engine/helengine.core/components/2d/interactable/TabComponent.cs engine/helengine.editor.tests/PlatformTabStripViewTests.cs
git commit -m "feat: add shared platform tab strip view"
```

### Task 3: Integrate Platform Tab Strip Into Asset Import Settings View

**Files:**
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Test: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`

- [ ] **Step 1: Add the failing asset processor integration test**

```csharp
[Fact]
public void Show_WhenManyPlatformsAreProvided_UsesSharedPlatformStripAndRevealsSelectedPlatform() {
    FontAsset font = TestFontFactory.Create();
    AssetImportSettingsView view = new AssetImportSettingsView(font, 1);
    AssetImportSettings settings = new AssetImportSettings();
    List<string> platforms = new List<string> {
        "windows", "ps2", "linux", "gamecube", "wii", "xbox", "switch"
    };

    view.Show(new[] { "assimp" }, settings, platforms, "switch", AssetEntryKind.Model);
    view.UpdateLayout(0, 0, 220);

    Assert.Equal("switch", view.SelectedPlatformId);
    Assert.True(view.PlatformTabStrip.IsPlatformFullyVisible("switch"));
}
```

- [ ] **Step 2: Run the asset processor test to verify it fails**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.AssetImportSettingsViewTests.Show_WhenManyPlatformsAreProvided_UsesSharedPlatformStripAndRevealsSelectedPlatform"`

Expected: FAIL because `AssetImportSettingsView` still owns the local tab row.

- [ ] **Step 3: Replace local tab bookkeeping with the shared strip**

```csharp
readonly PlatformTabStripView PlatformTabStrip;

public PlatformTabStripView PlatformTabStripView => PlatformTabStrip;

public AssetImportSettingsView(FontAsset font, ushort layerMask) {
    PlatformTabStrip = new PlatformTabStripView(font, layerMask, PlatformTabWidth, ControlHeight, PlatformTabSpacing, ControlHeight);
    RootEntity.AddChild(PlatformTabStrip.Root);
}

void RebuildPlatformTabs() {
    PlatformTabStrip.SetPlatforms(SupportedPlatformIds, CurrentPlatformId, HandlePlatformTabClicked);
}

void UpdatePlatformTabLayout(int currentTop, int width) {
    PlatformTabStrip.UpdateLayout(0, currentTop, width);
}

void UpdatePlatformTabVisualState() {
    PlatformTabStrip.SetSelectedPlatform(CurrentPlatformId);
}
```

- [ ] **Step 4: Remove the old local tab-row fields and methods**

```csharp
// Remove:
// - PlatformTabsHost
// - PlatformTabButtonHosts
// - PlatformTabButtons
// - ClearPlatformTabs()
// - UpdatePlatformTabLayout()
//
// Keep:
// - SupportedPlatformIds
// - CurrentPlatformId
// - HandlePlatformTabClicked(...)
```

- [ ] **Step 5: Run the asset processor test slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.AssetImportSettingsViewTests"`

Expected: PASS

- [ ] **Step 6: Commit the asset processor migration**

```bash
git add engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs
git commit -m "refactor: use shared platform tab strip in asset settings"
```

### Task 4: Add Focused Reuse Coverage For Future Build Dialog Adoption

**Files:**
- Modify: `engine/helengine.editor.tests/PlatformTabStripViewTests.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Add one compatibility-oriented strip test**

```csharp
[Fact]
public void UpdateLayout_WhenWidthMatchesBuildDialogTabBand_DoesNotRequireHostSpecificLayoutRules() {
    FontAsset font = TestFontFactory.Create();
    PlatformTabStripView view = new PlatformTabStripView(font, 1, 120, 24, 0, 24);

    view.SetPlatforms(new[] { "windows", "ps2", "linux" }, "windows", _ => { });
    view.UpdateLayout(0, 0, 360);

    Assert.False(view.CanScrollLeft);
    Assert.False(view.CanScrollRight);
}
```

- [ ] **Step 2: Update any build-dialog assumptions that depended on shared tab defaults only**

```csharp
// Keep BuildDialog unchanged in production code.
// Only adjust tests if they rely on the old assumption that all platform-tab logic is local to each screen.
```

- [ ] **Step 3: Run the combined regression slice**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PlatformTabStripViewTests|FullyQualifiedName~helengine.editor.tests.AssetImportSettingsViewTests|FullyQualifiedName~helengine.editor.tests.BuildDialogTests"`

Expected: PASS

- [ ] **Step 4: Commit the final regression coverage**

```bash
git add engine/helengine.editor.tests/PlatformTabStripViewTests.cs engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "test: cover reusable platform tab strip layout"
```

### Task 5: Final Verification And Handoff

**Files:**
- Modify: `docs/superpowers/plans/2026-05-08-platform-tab-strip.md`

- [ ] **Step 1: Run the full targeted verification one more time**

Run: `dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~helengine.editor.tests.PlatformTabStripViewTests|FullyQualifiedName~helengine.editor.tests.AssetImportSettingsViewTests|FullyQualifiedName~helengine.editor.tests.BuildDialogTests|FullyQualifiedName~helengine.editor.tests.TabComponentTests"`

Expected: PASS

- [ ] **Step 2: Update this plan checklist to reflect completed execution**

```markdown
- [x] Step completed
```

- [ ] **Step 3: Commit any final plan or test expectation adjustments**

```bash
git add docs/superpowers/plans/2026-05-08-platform-tab-strip.md
git commit -m "docs: update platform tab strip execution plan status"
```

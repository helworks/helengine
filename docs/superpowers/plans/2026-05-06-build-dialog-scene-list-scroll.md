# Build Dialog Scene List Scroll Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the `BuildDialog` scene list use the same bounded wheel-scroll viewport model as the queue and build logs so large scene sets stop overflowing the left column.

**Architecture:** Add a scene-list-specific `ScrollComponent` and a pooled visible-row bundle so `BuildDialog` only renders the rows that fit inside the bordered scene-list area. Keep scene order persistence on the existing per-scene config path, and move scene selection persistence away from visible checkbox sweeps so hidden selected scenes survive virtualization.

**Tech Stack:** C#, xUnit, `ScrollComponent`, `EditorEntity`, `TextBoxComponent`, `CheckBoxComponent`, `TextComponent`

---

## File Structure

- Create: `engine/helengine.editor/components/ui/BuildDialogSceneRow.cs`
  - One reusable scene-row bundle containing the order text box, scene label, and checkbox for a single visible scene row.
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
  - Add scene-list scroll state, pooled-row lifecycle, viewport math helpers, and hidden-selection-safe persistence.
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
  - Add the failing regressions for scene-list virtualization and hidden selected scene persistence, and use the existing lower-left layout test as a green verification guard.

## Task 1: Add the failing scene-list scroll regression

**Files:**
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing test**

```csharp
/// <summary>
/// Ensures the scene list uses a scroll viewport when the active platform exposes more scenes than fit inside the bordered list area.
/// </summary>
[Fact]
public void Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    List<string> sceneIds = CreateSceneIds(18);

    dialog.Show(
        ["windows"],
        sceneIds,
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/Map00.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows"
                }
            ]
        });

    ScrollComponent sceneListScrollComponent = GetPrivateField<ScrollComponent>(dialog, "SceneListScrollComponent");
    List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

    Assert.True(sceneListScrollComponent.MaximumScrollOffset > 0);
    Assert.Equal(sceneListScrollComponent.VisibleItemCount, mapLabelTexts.Count);
    Assert.Equal("Scenes/Map00.helen", mapLabelTexts[0].Text);

    Assert.True(sceneListScrollComponent.ScrollTo(1));

    Assert.Equal("Scenes/Map01.helen", mapLabelTexts[0].Text);
    Assert.DoesNotContain("Scenes/Map00.helen", mapLabelTexts[0].Text);

    dialog.Show(
        ["windows"],
        sceneIds,
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/Map00.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows"
                }
            ]
        });

    Assert.Equal(0, sceneListScrollComponent.ScrollOffset);
    Assert.Equal("Scenes/Map00.helen", mapLabelTexts[0].Text);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset"
```

Expected: FAIL because `BuildDialog` does not yet expose `SceneListScrollComponent` and still renders every scene row directly under `SceneListRoot`.

- [ ] **Step 3: Commit the red test**

```bash
rtk git add engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "test: capture build dialog scene list overflow regression"
```

## Task 2: Implement the scene-list scroll viewport and pooled rows

**Files:**
- Create: `engine/helengine.editor/components/ui/BuildDialogSceneRow.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Add the reusable scene-row bundle**

Create `engine/helengine.editor/components/ui/BuildDialogSceneRow.cs` with one row class that mirrors `BuildDialogQueueRow`, but exposes the three scene controls needed for rebinding:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Bundles the reusable visuals that render one visible scene row inside the build dialog.
    /// </summary>
    public sealed class BuildDialogSceneRow {
        /// <summary>
        /// Initializes one pooled scene row using the shared build-dialog styling.
        /// </summary>
        /// <param name="font">Font used by the scene label and order field.</param>
        /// <param name="metrics">Scaled editor UI metrics used to size the row controls.</param>
        /// <param name="layerMask">Layer mask applied to the row hierarchy.</param>
        /// <param name="panelOrder">Render order used for panel-background controls.</param>
        /// <param name="textOrder">Render order used for text and checkbox visuals.</param>
        public BuildDialogSceneRow(FontAsset font, EditorUiMetrics metrics, ushort layerMask, byte panelOrder, byte textOrder) {
            if (font == null) {
                throw new ArgumentNullException(nameof(font));
            }

            if (metrics == null) {
                throw new ArgumentNullException(nameof(metrics));
            }

            Root = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true,
                Enabled = false
            };

            OrderHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(OrderHost);

            OrderField = new TextBoxComponent(
                new int2(
                    metrics.ScalePixels(BuildDialog.SceneOrderFieldWidth),
                    metrics.ScalePixels(BuildDialog.SceneOrderFieldHeight)),
                font,
                string.Empty);
            OrderField.SetRenderOrders(panelOrder, textOrder);
            OrderHost.AddComponent(OrderField);

            LabelHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(LabelHost);

            LabelText = new TextComponent {
                Font = font,
                Text = string.Empty,
                Color = ThemeManager.Colors.InputForegroundPrimary,
                RenderOrder2D = textOrder
            };
            LabelHost.AddComponent(LabelText);

            CheckBoxHost = new EditorEntity {
                LayerMask = layerMask,
                Position = float3.Zero,
                InternalEntity = true
            };
            Root.AddChild(CheckBoxHost);

            CheckBox = new CheckBoxComponent(
                new int2(metrics.ScalePixels(18), metrics.ScalePixels(18)),
                font,
                false);
            CheckBox.SetRenderOrders(panelOrder, textOrder);
            CheckBoxHost.AddComponent(CheckBox);
        }

        /// <summary>
        /// Gets the root entity for the pooled row.
        /// </summary>
        public EditorEntity Root { get; }

        /// <summary>
        /// Gets the host entity for the order textbox.
        /// </summary>
        public EditorEntity OrderHost { get; }

        /// <summary>
        /// Gets the order textbox bound to the current scene id.
        /// </summary>
        public TextBoxComponent OrderField { get; }

        /// <summary>
        /// Gets the host entity for the scene label.
        /// </summary>
        public EditorEntity LabelHost { get; }

        /// <summary>
        /// Gets the scene label text component.
        /// </summary>
        public TextComponent LabelText { get; }

        /// <summary>
        /// Gets the host entity for the scene selection checkbox.
        /// </summary>
        public EditorEntity CheckBoxHost { get; }

        /// <summary>
        /// Gets the checkbox bound to the current scene id.
        /// </summary>
        public CheckBoxComponent CheckBox { get; }

        /// <summary>
        /// Gets or sets the scene id currently rendered by the row.
        /// </summary>
        public string SceneId { get; set; }
    }
}
```

- [ ] **Step 2: Replace direct scene-row instantiation with scroll state and pooled row layout**

Modify `engine/helengine.editor/components/ui/BuildDialog.cs` to add the new scene-list root state:

```csharp
readonly EditorEntity SceneListItemsRoot;
readonly ScrollComponent SceneListScrollComponent;
readonly List<BuildDialogSceneRow> SceneRows;
bool IsBindingSceneRows;
```

Initialize those fields in the constructor next to `SceneListRoot`:

```csharp
SceneRows = new List<BuildDialogSceneRow>(16);

SceneListItemsRoot = new EditorEntity {
    LayerMask = LayerMask,
    Position = new float3(GetSceneListPaddingPixels(), GetSceneListPaddingPixels(), 0.1f),
    InternalEntity = true
};
SceneListRoot.AddChild(SceneListItemsRoot);

SceneListScrollComponent = new ScrollComponent();
SceneListScrollComponent.UpdateOrder = Core.Instance.ObjectManager.GetUpdateOrderForLayer(1);
SceneListScrollComponent.ScrollOffsetChanged += HandleSceneListScrollOffsetChanged;
SceneListItemsRoot.AddComponent(SceneListScrollComponent);
```

Replace the body of `RebuildActivePlatformSceneRows()` with the pooled-row flow:

```csharp
void RebuildActivePlatformSceneRows() {
    EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
    EnsureSceneOrderEntries(platformConfig);

    DisplayedSceneIds.Clear();
    List<string> orderedSceneIds = BuildDisplayedSceneIds(platformConfig);
    for (int index = 0; index < orderedSceneIds.Count; index++) {
        DisplayedSceneIds.Add(orderedSceneIds[index]);
    }

    SceneListScrollComponent.ItemCount = DisplayedSceneIds.Count;
    SceneListScrollComponent.VisibleItemCount = GetSceneListVisibleRowCount();
    SceneListScrollComponent.Size = new int2(GetSceneListViewportWidth(), GetSceneListViewportHeight());
    SceneListScrollComponent.ClampScrollOffset();
    EnsureSceneRowCount(SceneListScrollComponent.VisibleItemCount);
    UpdateSceneListRowsLayout();

    LayoutLowerLeftControls();
    OutputDirectoryField.Text = platformConfig.OutputDirectoryPath ?? string.Empty;
    OutputDirectoryField.SetInvalidState(false);
    CodeModuleField.Text = string.Join(", ", platformConfig.SelectedCodeModuleIds ?? []);
    CodeModuleField.SetInvalidState(false);
    DebugBuildCheckBox.IsChecked = platformConfig.DebugBuild;
    SetSceneListInvalidState(false);
}
```

Add the new scene-list scroll helpers:

```csharp
void HandleSceneListScrollOffsetChanged(ScrollComponent scrollComponent, int scrollOffset) {
    if (scrollComponent == null) {
        throw new ArgumentNullException(nameof(scrollComponent));
    }

    UpdateSceneListRowsLayout();
}

void EnsureSceneRowCount(int count) {
    for (int index = SceneRows.Count; index < count; index++) {
        BuildDialogSceneRow row = new BuildDialogSceneRow(DialogFont, DialogMetrics, LayerMask, DialogPanelOrder, DialogTextOrder);
        row.OrderField.TextChanged += currentOrderField => HandleSceneOrderFieldChanged(row.SceneId, currentOrderField);
        row.OrderField.Submitted += currentOrderField => HandleSceneOrderFieldSubmitted(row.SceneId, currentOrderField);
        row.CheckBox.CheckedChanged += (currentCheckBox, isChecked) => HandleSceneSelectionChanged(row.SceneId, currentCheckBox, isChecked);
        SceneListItemsRoot.AddChild(row.Root);
        SceneRows.Add(row);
    }
}

void UpdateSceneListRowsLayout() {
    EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
    int visibleRowCount = SceneListScrollComponent.VisibleItemCount < 1
        ? GetSceneListVisibleRowCount()
        : SceneListScrollComponent.VisibleItemCount;

    SceneListScrollComponent.VisibleItemCount = visibleRowCount;
    SceneListScrollComponent.Size = new int2(GetSceneListViewportWidth(), GetSceneListViewportHeight());
    EnsureSceneRowCount(visibleRowCount);

    MapLabelHosts.Clear();
    MapLabelTexts.Clear();
    MapCheckBoxHosts.Clear();
    MapCheckBoxes.Clear();
    MapOrderHosts.Clear();
    MapOrderFields.Clear();

    IsBindingSceneRows = true;
    try {
        int scrollOffset = SceneListScrollComponent.ScrollOffset;
        for (int rowIndex = 0; rowIndex < SceneRows.Count; rowIndex++) {
            BuildDialogSceneRow row = SceneRows[rowIndex];
            int sceneIndex = scrollOffset + rowIndex;
            if (sceneIndex < 0 || sceneIndex >= DisplayedSceneIds.Count) {
                row.Root.Enabled = false;
                continue;
            }

            string sceneId = DisplayedSceneIds[sceneIndex];
            row.SceneId = sceneId;
            row.Root.Enabled = true;
            row.Root.Position = new float3(0f, rowIndex * GetSceneRowHeightPixels(), 0.1f);
            row.OrderHost.Position = new float3(0f, DialogMetrics.ScalePixels(2), 0.1f);
            row.LabelHost.Position = new float3(GetSceneLabelX(), 0f, 0.1f);
            row.CheckBoxHost.Position = new float3(GetSceneCheckBoxX(), DialogMetrics.ScalePixels(2), 0.1f);
            row.OrderField.Text = GetSceneOrderNumber(platformConfig, sceneId).ToString();
            row.OrderField.SetInvalidState(false);
            row.LabelText.Text = sceneId;
            row.CheckBox.IsChecked = platformConfig.SelectedSceneIds.Contains(sceneId);

            MapOrderHosts.Add(row.OrderHost);
            MapOrderFields.Add(row.OrderField);
            MapLabelHosts.Add(row.LabelHost);
            MapLabelTexts.Add(row.LabelText);
            MapCheckBoxHosts.Add(row.CheckBoxHost);
            MapCheckBoxes.Add(row.CheckBox);
        }
    } finally {
        IsBindingSceneRows = false;
    }
}
```

Add the scene-list sizing helpers that mirror the queue/build-log pattern:

```csharp
int GetSceneListPaddingPixels() {
    return DialogMetrics.ScalePixels(SceneListPadding);
}

int GetSceneRowHeightPixels() {
    return DialogMetrics.ScalePixels(SceneRowHeight);
}

int GetSceneListViewportWidth() {
    return GetBuildColumnWidth() - (GetSceneListPaddingPixels() * 2);
}

int GetSceneListViewportHeight() {
    return Math.Max(1, SceneListBackground.Size.Y - (GetSceneListPaddingPixels() * 2));
}

int GetSceneListVisibleRowCount() {
    return Math.Max(1, GetSceneListViewportHeight() / Math.Max(1, GetSceneRowHeightPixels()));
}

int GetSceneLabelX() {
    return GetSceneListPaddingPixels() + DialogMetrics.ScalePixels(SceneOrderFieldWidth) + DialogMetrics.ScalePixels(8);
}

int GetSceneCheckBoxX() {
    return GetSceneListViewportWidth() - DialogMetrics.ScalePixels(18);
}
```

Update the dialog lifecycle so the scene-list scroll offset matches the queue and log behavior:

```csharp
SceneListScrollComponent.ResetScrollOffset();
```

Add that call to `Show()` before `RebuildActivePlatformSceneRows()`, to `Hide()`, and to `HandlePlatformTabClicked()` before rebuilding rows for the newly active platform.

- [ ] **Step 3: Run the scene-list regression plus the existing layout guards**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset|FullyQualifiedName~BuildDialogTests.Show_WhenManyScenesAreAvailable_KeepsCopySettingsButtonInsideDialogBounds|FullyQualifiedName~BuildDialogTests.Show_CreatesBorderedSceneListContainer"
```

Expected: PASS. The new scene-list test should report a positive scroll range and a changed first visible label after `ScrollTo(1)`. The existing lower-left control and bordered-container tests should remain green.

- [ ] **Step 4: Commit the viewport implementation**

```bash
rtk git add engine/helengine.editor/components/ui/BuildDialogSceneRow.cs engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "feat: scroll build dialog scene list"
```

## Task 3: Add the failing hidden-selection persistence regression

**Files:**
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

- [ ] **Step 1: Write the failing test**

Add a second regression that captures the selection-persistence bug introduced by virtualization if `BuildDialog` still reconstructs selection from only the visible checkbox slice:

```csharp
/// <summary>
/// Ensures Add to Build preserves a selected scene that is outside the current visible scene-list viewport.
/// </summary>
[Fact]
public void HandleAddToBuildClicked_WhenSelectedSceneIsOutsideVisibleViewport_PreservesHiddenSelection() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    BuildDialogAddRequest raisedRequest = null;
    List<string> sceneIds = CreateSceneIds(18);
    dialog.AddRequested += request => raisedRequest = request;

    dialog.Show(
        ["windows"],
        sceneIds,
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/Map14.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows"
                }
            ]
        });

    List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

    Assert.DoesNotContain("Scenes/Map14.helen", mapLabelTexts.Select(label => label.Text));

    InvokePrivate(dialog, "HandleAddToBuildClicked");

    Assert.NotNull(raisedRequest);
    Assert.Equal(
        [
            "Scenes/Map14.helen"
        ],
        raisedRequest.SelectedSceneIds);
}
```

- [ ] **Step 2: Run the hidden-selection regression to verify it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.HandleAddToBuildClicked_WhenSelectedSceneIsOutsideVisibleViewport_PreservesHiddenSelection"
```

Expected: FAIL because `SyncActivePlatformConfig()` still clears `platformConfig.SelectedSceneIds` and rebuilds it from `MapCheckBoxes`, which now contains only the visible scene rows.

- [ ] **Step 3: Commit the red hidden-selection test**

```bash
rtk git add engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "test: capture hidden build dialog scene selection regression"
```

## Task 4: Decouple scene selection persistence from visible rows

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Update the selection handlers and sync path**

Modify `BuildDialog.cs` so the persisted active-platform config is updated by scene id as checkbox state changes instead of by sweeping only the currently visible rows.

Change the checkbox handler signature and add an early rebind guard:

```csharp
void HandleSceneSelectionChanged(string sceneId, CheckBoxComponent checkBox, bool isChecked) {
    if (string.IsNullOrWhiteSpace(sceneId)) {
        throw new ArgumentException("Scene id is required.", nameof(sceneId));
    }

    if (checkBox == null) {
        throw new ArgumentNullException(nameof(checkBox));
    }

    if (IsBindingSceneRows) {
        return;
    }

    EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
    if (isChecked) {
        if (!platformConfig.SelectedSceneIds.Contains(sceneId)) {
            platformConfig.SelectedSceneIds.Add(sceneId);
        }

        SetSceneListInvalidState(false);
        return;
    }

    platformConfig.SelectedSceneIds.Remove(sceneId);
}
```

Stop rebuilding selected scenes from the visible row slice inside `SyncActivePlatformConfig()`:

```csharp
void SyncActivePlatformConfig() {
    if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
        return;
    }

    EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
    EnsureSceneOrderEntries(platformConfig);
    platformConfig.OutputDirectoryPath = OutputDirectoryField.Text ?? string.Empty;
    platformConfig.SelectedCodeModuleIds = ParseCodeModuleIds(CodeModuleField.Text);
    platformConfig.DebugBuild = DebugBuildCheckBox.IsChecked;
    EnsurePlatformSelectionDefaults(platformConfig);
}
```

Change `HasAnySelectedScene()` so validation follows the persisted active config instead of visible checkboxes:

```csharp
bool HasAnySelectedScene() {
    if (CurrentBuildConfig == null || string.IsNullOrWhiteSpace(ActivePlatformId)) {
        return false;
    }

    EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(ActivePlatformId);
    return platformConfig.SelectedSceneIds.Count > 0;
}
```

Keep the order-field handlers guarded during rebinding:

```csharp
if (IsBindingSceneRows) {
    return;
}
```

Add that early return near the top of both `HandleSceneOrderFieldChanged()` and `HandleSceneOrderFieldSubmitted()` so programmatic row rebinding does not rewrite scene orders or trigger recursive rebuilds.

- [ ] **Step 2: Run the new regression plus the existing scene-selection tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.HandleAddToBuildClicked_WhenSelectedSceneIsOutsideVisibleViewport_PreservesHiddenSelection|FullyQualifiedName~BuildDialogTests.HandleAddToBuildClicked_WhenVisibleRowsAreReordered_PreservesDisplayedSceneSelection|FullyQualifiedName~BuildDialogTests.HandleAddToBuildClicked_WhenNoScenesSelected_ShakesAndMarksSceneListInvalidUntilSelectionReturns|FullyQualifiedName~BuildDialogTests.Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset"
```

Expected: PASS. The hidden-scene regression should now preserve `Scenes/Map14.helen`, and the existing selection-order tests should remain green.

- [ ] **Step 3: Run the full BuildDialog test class**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests"
```

Expected: PASS for the whole `BuildDialogTests` class with no new scene-list regressions.

- [ ] **Step 4: Commit the persistence fix**

```bash
rtk git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "fix: preserve build dialog scene selections while scrolling"
```

## Self-Review

- Spec coverage:
  - Bounded scene-list viewport and wheel scrolling are covered by Tasks 1-2.
  - Lower-left control bounds staying intact are covered by Task 2’s green verification against the existing layout regression.
  - Hidden selected scenes surviving virtualization are covered by Tasks 3-4.
  - Existing scene ordering and selection semantics staying intact are covered by Task 4’s targeted reruns plus the full `BuildDialogTests` pass.
- Placeholder scan:
  - No `TODO`, `TBD`, or “similar to” placeholders remain.
  - Each code-changing step includes concrete code blocks or exact methods to add/replace.
- Type consistency:
  - `BuildDialogSceneRow`, `SceneListItemsRoot`, `SceneListScrollComponent`, `SceneRows`, and `IsBindingSceneRows` are used consistently across all tasks.
  - The new checkbox handler signature is consistently `HandleSceneSelectionChanged(string sceneId, CheckBoxComponent checkBox, bool isChecked)`.

Plan complete and saved to `docs/superpowers/plans/2026-05-06-build-dialog-scene-list-scroll.md`. Two execution options:

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration

**2. Inline Execution** - Execute tasks in this session using executing-plans, batch execution with checkpoints

**Which approach?**

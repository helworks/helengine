# Build Queue Card Layout And Scroll Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make `BuildDialog` queue cards taller, keep queue text out of the remove-button lane, show a compact fixed-height multiline summary, and preserve scrolling when many builds are queued.

**Architecture:** Keep the existing `QueueScrollComponent` and pooled `BuildDialogQueueRow` virtualization model instead of replacing it with a document scroller. Redesign each queue row as a taller fixed-height card, update all queue-row geometry to use scaled helpers consistently, and replace status-message card content with a compact three-line summary built from straightforward queue metadata.

**Tech Stack:** C# / .NET 9, editor UI entities (`EditorEntity`, `TextComponent`, `ButtonComponent`, `ScrollComponent`), xUnit.

---

## File Structure

- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
  - Increase queue row height and keep queue math consistently metric-scaled.
  - Replace queue-card text composition with a compact 2-3 line summary that omits `StatusMessage`.
  - Recompute row positions, text bounds, visible-row count, and scroll behavior from the taller fixed card height.

- Modify: `engine/helengine.editor/components/ui/BuildDialogQueueRow.cs`
  - Accept `EditorUiMetrics` so the row shell can initialize scaled background/text/button sizes.
  - Keep one reusable row bundle, but update its default bounds to match the taller fixed card design.

- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
  - Update queue-card tests that currently expect `StatusMessage` inside queue cards.
  - Add regressions for multiline summary content, reserved button-safe text width, and queue scrolling with the new summary format.

---

### Task 1: Add Failing Build Queue Card Regressions

**Files:**
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Update the queue-card tests to reflect the approved behavior**

Replace and extend the queue-card tests in `engine/helengine.editor.tests/BuildDialogTests.cs` with these exact assertions:

```csharp
/// <summary>
/// Ensures one queue row is rendered for each persisted queued build item and cards omit verbose status-message text.
/// </summary>
[Fact]
public void Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem() {
    BuildDialog dialog = new BuildDialog(CreateFont());

    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-1",
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending,
                    DebugBuild = true,
                    SelectedBuildProfileId = "b1",
                    SelectedGraphicsProfileId = "g1",
                    SelectedCodegenProfileId = "c1",
                    SelectedCodeModuleIds = [
                        "gameplay",
                        "ai"
                    ]
                },
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-2",
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/Menu.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows-two",
                    Status = EditorBuildQueueItemStatus.Failed,
                    StatusMessage = "Unsupported scene format."
                }
            ]
        });

    List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");
    string[] firstLines = queueItemTexts[0].Text.Split('\n');
    string[] secondLines = queueItemTexts[1].Text.Split('\n');

    Assert.Equal(2, queueItemTexts.Count);
    Assert.Equal(3, firstLines.Length);
    Assert.Equal("windows | Pending", firstLines[0]);
    Assert.Equal("1 scene(s) | Debug", firstLines[1]);
    Assert.Equal("build b1 | gfx g1 | codegen c1 | modules 2", firstLines[2]);
    Assert.Equal("windows | Failed", secondLines[0]);
    Assert.Equal("1 scene(s) | Release", secondLines[1]);
    Assert.DoesNotContain("Unsupported scene format.", queueItemTexts[1].Text);
}

/// <summary>
/// Ensures a long optional capability summary is clipped on the third line before it reaches the remove button lane.
/// </summary>
[Fact]
public void Show_WhenQueueItemsProvided_ClipsCapabilitySummaryOnThirdLine() {
    BuildDialog dialog = new BuildDialog(CreateFont());

    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-1",
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Failed,
                    DebugBuild = false,
                    SelectedBuildProfileId = "build-profile-with-a-very-long-name",
                    SelectedGraphicsProfileId = "graphics-profile-with-a-very-long-name",
                    SelectedCodegenProfileId = "codegen-profile-with-a-very-long-name",
                    SelectedCodeModuleIds = [
                        "gameplay",
                        "ai",
                        "editor"
                    ],
                    StatusMessage = "This failure belongs in the build log, not in the card."
                }
            ]
        });

    TextComponent queueText = Assert.Single(GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts"));
    string[] lines = queueText.Text.Split('\n');

    Assert.Equal(3, lines.Length);
    Assert.Equal("windows | Failed", lines[0]);
    Assert.Equal("1 scene(s) | Release", lines[1]);
    Assert.DoesNotContain("This failure belongs in the build log, not in the card.", queueText.Text);
    Assert.EndsWith("...", lines[2]);
}

/// <summary>
/// Ensures the queue-card text width is reduced before layout so the remove button keeps a dedicated right-edge lane.
/// </summary>
[Fact]
public void Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton() {
    BuildDialog dialog = new BuildDialog(CreateFont());

    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = [
                new EditorBuildQueueItemDocument {
                    QueueItemId = "queue-1",
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ],
                    OutputDirectoryPath = @"C:\builds\windows",
                    Status = EditorBuildQueueItemStatus.Pending,
                    DebugBuild = true,
                    SelectedBuildProfileId = "b1",
                    SelectedGraphicsProfileId = "g1",
                    SelectedCodegenProfileId = "c1",
                    SelectedCodeModuleIds = [
                        "gameplay",
                        "ai"
                    ]
                }
            ]
        });

    TextComponent queueText = Assert.Single(GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts"));
    RoundedRectComponent queueCardBackground = Assert.Single(GetPrivateField<List<RoundedRectComponent>>(dialog, "QueueItemCardBackgrounds"));
    EditorEntity removeButtonHost = Assert.Single(GetPrivateField<List<EditorEntity>>(dialog, "QueueItemRemoveButtonHosts"));

    Assert.Equal(
        queueCardBackground.Size.X - (BuildDialog.QueueCardTextPadding * 2) - BuildDialog.QueueCardRemoveButtonWidth - BuildDialog.QueueCardTextButtonGap,
        queueText.Size.X);
    Assert.True(removeButtonHost.LocalPosition.X >= queueText.Parent.LocalPosition.X + queueText.Size.X + BuildDialog.QueueCardTextButtonGap);
}

/// <summary>
/// Ensures the queued-build list still virtualizes rows when the queue exceeds the visible card count and uses queue metadata rather than status messages to identify the visible item.
/// </summary>
[Fact]
public void Show_WhenQueueItemsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    List<EditorBuildQueueItemDocument> queueItems = [];

    for (int index = 0; index < 9; index++) {
        queueItems.Add(new EditorBuildQueueItemDocument {
            QueueItemId = "queue-" + (index + 1).ToString(),
            PlatformId = "platform-" + (index + 1).ToString(),
            SelectedSceneIds = [
                "Scenes/City.helen"
            ],
            OutputDirectoryPath = @"C:\builds\windows",
            Status = EditorBuildQueueItemStatus.Pending,
            DebugBuild = index % 2 == 0
        });
    }

    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = queueItems
        });

    ScrollComponent queueScrollComponent = GetPrivateField<ScrollComponent>(dialog, "QueueScrollComponent");
    List<TextComponent> queueItemTexts = GetPrivateField<List<TextComponent>>(dialog, "QueueItemTexts");

    Assert.True(queueScrollComponent.MaximumScrollOffset > 0);
    Assert.Equal(queueScrollComponent.VisibleItemCount, queueItemTexts.Count);
    Assert.Contains("platform-1 | Pending", queueItemTexts[0].Text);

    Assert.True(queueScrollComponent.ScrollTo(1));

    Assert.Contains("platform-2 | Pending", queueItemTexts[0].Text);
    Assert.DoesNotContain("platform-1 | Pending", queueItemTexts[0].Text);

    dialog.Show(
        ["windows"],
        [
            "Scenes/City.helen"
        ],
        "windows",
        new EditorBuildConfigDocument {
            Platforms = [
                new EditorBuildPlatformConfigDocument {
                    PlatformId = "windows",
                    SelectedSceneIds = [
                        "Scenes/City.helen"
                    ]
                }
            ],
            QueueItems = queueItems
        });

    Assert.Equal(0, queueScrollComponent.ScrollOffset);
    Assert.Contains("platform-1 | Pending", queueItemTexts[0].Text);
}
```

- [ ] **Step 2: Run the focused queue-card tests to verify they fail for the right reasons**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ClipsCapabilitySummaryOnThirdLine|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset" -v minimal
```

Expected:

- `FAIL` because `BuildQueueItemText(...)` still includes `StatusMessage`.
- `FAIL` because the current queue card only builds two lines and clips the second line instead of the optional third line.
- `FAIL` because row geometry still mixes scaled sizes with raw `QueueRowHeight` for row placement and visible-row calculations.

- [ ] **Step 3: Commit the failing queue-card regressions only**

```bash
rtk git add engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "test: add build queue card layout regressions"
```

### Task 2: Scale The Queue Row Shell For Taller Fixed Cards

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialogQueueRow.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Re-run the width/layout regression to confirm the current row shell still initializes compact unscaled bounds**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton -v minimal
```

Expected:

- `FAIL` because the current queue shell still initializes around the short existing row design and the later layout code still uses raw `QueueRowHeight`.

- [ ] **Step 2: Pass `EditorUiMetrics` into `BuildDialogQueueRow` and initialize taller scaled defaults**

Update the constructor in `engine/helengine.editor/components/ui/BuildDialogQueueRow.cs` like this:

```csharp
/// <summary>
/// Initializes a new queue row with the shared dialog styling.
/// </summary>
/// <param name="font">Font used to render the queue-row text.</param>
/// <param name="metrics">Scaled editor UI metrics used to size the queue row.</param>
/// <param name="layerMask">Layer mask applied to the row hierarchy.</param>
/// <param name="panelOrder">Render order used for row backgrounds and separators.</param>
/// <param name="textOrder">Render order used for row labels and buttons.</param>
public BuildDialogQueueRow(FontAsset font, EditorUiMetrics metrics, ushort layerMask, byte panelOrder, byte textOrder) {
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

    Background = new RoundedRectComponent {
        FillColor = ThemeManager.Colors.SurfacePrimary,
        BorderColor = ThemeManager.Colors.SurfacePrimary,
        BorderThickness = 0f,
        Radius = 0f,
        RenderOrder2D = panelOrder,
        Size = new int2(
            metrics.ScalePixels(BuildDialog.QueueColumnWidth - 4),
            metrics.ScalePixels(BuildDialog.QueueRowHeight))
    };
    Root.AddComponent(Background);

    SeparatorHost = new EditorEntity {
        LayerMask = layerMask,
        Position = float3.Zero,
        InternalEntity = true
    };
    Root.AddChild(SeparatorHost);

    Separator = new SpriteComponent {
        Texture = TextureUtils.PixelTexture,
        Color = ThemeManager.Colors.AccentTertiary,
        RenderOrder2D = panelOrder,
        Size = new int2(metrics.ScalePixels(BuildDialog.QueueColumnWidth - 4), metrics.ScalePixels(1))
    };
    SeparatorHost.AddComponent(Separator);

    RemoveButtonHost = new EditorEntity {
        LayerMask = layerMask,
        Position = float3.Zero,
        InternalEntity = true
    };
    Root.AddChild(RemoveButtonHost);

    RemoveButton = new ButtonComponent(
        "X",
        new int2(
            metrics.ScalePixels(BuildDialog.QueueCardRemoveButtonWidth),
            metrics.ScalePixels(28)),
        font,
        HandleRemoveButtonClicked);
    RemoveButton.SetRenderOrders(panelOrder, textOrder);
    RemoveButtonHost.AddComponent(RemoveButton);

    TextHost = new EditorEntity {
        LayerMask = layerMask,
        Position = float3.Zero,
        InternalEntity = true
    };
    Root.AddChild(TextHost);

    Text = new TextComponent {
        Font = font,
        Text = string.Empty,
        Color = ThemeManager.Colors.InputForegroundPrimary,
        RenderOrder2D = textOrder,
        Size = new int2(1, metrics.ScalePixels(BuildDialog.QueueRowHeight))
    };
    TextHost.AddComponent(Text);
}
```

Update `CreateQueueRow()` in `engine/helengine.editor/components/ui/BuildDialog.cs` to pass `DialogMetrics`:

```csharp
BuildDialogQueueRow CreateQueueRow() {
    BuildDialogQueueRow row = new BuildDialogQueueRow(DialogFont, DialogMetrics, LayerMask, DialogPanelOrder, DialogTextOrder);
    row.RemoveRequested += HandleQueueRowRemoveRequested;
    QueueItemsRoot.AddChild(row.Root);
    return row;
}
```

- [ ] **Step 3: Increase the fixed queue-row height constant before the full layout change**

Change the queue-row constant block in `engine/helengine.editor/components/ui/BuildDialog.cs` to:

```csharp
/// <summary>
/// Height reserved for each queued build row.
/// </summary>
public const int QueueRowHeight = 80;
/// <summary>
/// Width reserved for the queue-row remove button.
/// </summary>
public const int QueueCardRemoveButtonWidth = 32;
```

- [ ] **Step 4: Run the full-width row test to verify the row shell now uses the taller fixed-height baseline, while summary-content tests still fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersFullWidthRowPerQueueItem|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem" -v minimal
```

Expected:

- `PASS` or move closer on the row-height assertions for the full-width-row test.
- `FAIL` still remaining on queue text content because `BuildQueueItemText(...)` still uses the old status-message-based card text.

- [ ] **Step 5: Commit the taller scaled queue-row shell**

```bash
rtk git add engine/helengine.editor/components/ui/BuildDialogQueueRow.cs engine/helengine.editor/components/ui/BuildDialog.cs
rtk git commit -m "feat: scale build queue row shell"
```

### Task 3: Replace Queue Card Content And Fix Row/Scroll Geometry

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Re-run the queue summary regressions before changing the text builder**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ClipsCapabilitySummaryOnThirdLine|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset" -v minimal
```

Expected:

- `FAIL` because queue cards still build the old status-message-based text and still use raw `QueueRowHeight` for some row math.

- [ ] **Step 2: Replace the queue-card text builder with the approved compact summary**

In `engine/helengine.editor/components/ui/BuildDialog.cs`, replace `BuildQueueItemText(...)` with this version and add the new capability-summary helper directly above it:

```csharp
/// <summary>
/// Builds the optional compact capability summary shown on the third queue-card line.
/// </summary>
/// <param name="queueItem">Persisted queue item to summarize.</param>
/// <returns>Clipped third-line summary, or an empty string when no optional values are present.</returns>
string BuildQueueItemCapabilitySummary(EditorBuildQueueItemDocument queueItem) {
    if (queueItem == null) {
        throw new ArgumentNullException(nameof(queueItem));
    }

    List<string> segments = new List<string>();
    if (!string.IsNullOrWhiteSpace(queueItem.SelectedBuildProfileId)) {
        segments.Add("build " + queueItem.SelectedBuildProfileId);
    }

    if (!string.IsNullOrWhiteSpace(queueItem.SelectedGraphicsProfileId)) {
        segments.Add("gfx " + queueItem.SelectedGraphicsProfileId);
    }

    if (!string.IsNullOrWhiteSpace(queueItem.SelectedCodegenProfileId)) {
        segments.Add("codegen " + queueItem.SelectedCodegenProfileId);
    }

    if (queueItem.SelectedCodeModuleIds != null && queueItem.SelectedCodeModuleIds.Count > 0) {
        segments.Add("modules " + queueItem.SelectedCodeModuleIds.Count);
    }

    if (segments.Count == 0) {
        return string.Empty;
    }

    return ClipTextToWidth(string.Join(" | ", segments), GetQueueCardTextWidth());
}

/// <summary>
/// Builds one queue-row summary string for the supplied persisted queue item.
/// </summary>
/// <param name="queueItem">Persisted queue item to summarize.</param>
/// <returns>Queue summary text shown in the queue column.</returns>
string BuildQueueItemText(EditorBuildQueueItemDocument queueItem) {
    if (queueItem == null) {
        throw new ArgumentNullException(nameof(queueItem));
    }

    List<string> lines = new List<string>(3) {
        queueItem.PlatformId + " | " + queueItem.Status,
        queueItem.SelectedSceneIds.Count + " scene(s) | " + (queueItem.DebugBuild ? "Debug" : "Release")
    };

    string capabilitySummary = BuildQueueItemCapabilitySummary(queueItem);
    if (!string.IsNullOrWhiteSpace(capabilitySummary)) {
        lines.Add(capabilitySummary);
    }

    return string.Join("\n", lines);
}
```

Delete the old `BuildQueueItemStatusMessage(...)` helper entirely once `BuildQueueItemText(...)` no longer calls it.

- [ ] **Step 3: Convert row positioning, text bounds, and visible-row math to scaled card geometry**

In `engine/helengine.editor/components/ui/BuildDialog.cs`, update the queue layout helpers and row placement like this:

```csharp
/// <summary>
/// Gets the number of visible queue rows that fit within the current queue viewport.
/// </summary>
/// <returns>Visible queue row count.</returns>
int GetQueueVisibleRowCount() {
    return Math.Max(1, GetQueueRowsViewportHeight() / Math.Max(1, GetQueueCardHeight()));
}
```

Update the queue-row layout block inside `UpdateQueueRowsLayout()`:

```csharp
int scrollOffset = QueueScrollComponent.ScrollOffset;
for (int rowIndex = 0; rowIndex < QueueRows.Count; rowIndex++) {
    BuildDialogQueueRow row = QueueRows[rowIndex];
    int queueIndex = scrollOffset + rowIndex;
    if (queueIndex < 0 || queueIndex >= queueItemCount) {
        DisableQueueRow(row);
        continue;
    }

    EditorBuildQueueItemDocument queueItem = CurrentBuildConfig.QueueItems[queueIndex];
    row.QueueItemId = queueItem.QueueItemId;
    row.Root.Enabled = true;
    row.Root.Position = new float3(2f, rowIndex * GetQueueCardHeight(), 0.1f);
    row.Background.Size = new int2(GetQueueCardWidth(), GetQueueCardHeight());
    row.SeparatorHost.Position = new float3(0f, GetQueueCardHeight() - DialogMetrics.ScalePixels(1), 0.2f);
    row.Separator.Size = new int2(GetQueueCardWidth(), DialogMetrics.ScalePixels(1));
    row.RemoveButtonHost.Position = new float3(
        GetQueueCardWidth() - GetQueueCardRemoveButtonWidthPixels() - GetQueueCardTextPaddingPixels(),
        GetQueueCardTextPaddingPixels(),
        0.2f);
    row.TextHost.Position = new float3(
        GetQueueCardTextPaddingPixels(),
        GetQueueCardTextPaddingPixels(),
        0.2f);
    row.Text.Size = new int2(
        GetQueueCardTextWidth(),
        Math.Max(1, GetQueueCardHeight() - (GetQueueCardTextPaddingPixels() * 2)));
    row.Text.Text = BuildQueueItemText(queueItem);

    QueueItemHosts.Add(row.Root);
    QueueItemTexts.Add(row.Text);
    QueueItemRemoveButtonHosts.Add(row.RemoveButtonHost);
    QueueItemRemoveButtons.Add(row.RemoveButton);
    QueueItemCardBackgrounds.Add(row.Background);
}
```

- [ ] **Step 4: Run the queue-card regression group to verify the redesigned cards and scrolling behavior**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersOneQueueRowPerItem|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ClipsCapabilitySummaryOnThirdLine|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_ReservesTextWidthForRemoveButton|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_RendersFullWidthRowPerQueueItem|FullyQualifiedName~BuildDialogTests.Show_WhenQueueItemsProvided_CreatesRemoveButtonPerQueueItem" -v minimal
```

Expected:

- `PASS` for the queue summary-content tests.
- `PASS` for the reserved remove-button lane test.
- `PASS` for the queue virtualization/scroll-offset test.
- `PASS` for the existing full-width-row and remove-button tests with the taller fixed card geometry.

- [ ] **Step 5: Commit the queue-card content and geometry integration**

```bash
rtk git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "feat: redesign build queue cards"
```

### Task 4: Verify The Full Build Dialog Queue Surface

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialogQueueRow.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Run the focused Build Dialog suite that covers queue cards, build logs, and footer layout together**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests" -v minimal
```

Expected:

- `PASS` for the new queue-card regressions.
- `PASS` for existing build-log tests, proving verbose failure text still lives in the log section.
- `PASS` for existing footer/layout tests, proving the taller queue cards did not disturb the left-side dialog controls.

- [ ] **Step 2: Run the supporting queue/session regression suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorBuildQueueServiceTests" -v minimal
```

Expected:

- `PASS` for queue/session tests, proving the UI-only queue-card redesign did not affect queue persistence or execution semantics.

- [ ] **Step 3: Commit the completed Build Queue card and scroll integration**

```bash
rtk git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/components/ui/BuildDialogQueueRow.cs engine/helengine.editor.tests/BuildDialogTests.cs
rtk git commit -m "feat: improve build queue card layout and scrolling"
```

## Self-Review

- Spec coverage:
  - Taller fixed-height queue cards: implemented by Tasks 2 and 3.
  - Text no longer overlaps the remove `X`: covered by Task 1 reserved-width regression and Task 3 row layout.
  - Queue cards show compact straightforward metadata only: covered by Task 1 summary tests and Task 3 `BuildQueueItemText(...)`.
  - `StatusMessage` stays out of queue cards and in build logs: covered by Task 1 content regressions and Task 4 full Build Dialog verification.
  - Queue continues to scroll when many builds are queued: covered by Task 1 virtualization regression and Task 3 queue scroll/layout math.

- Placeholder scan:
  - No `TODO`, `TBD`, or vague “handle appropriately” language remains.
  - Each task names exact files, tests, commands, and code changes.

- Type consistency:
  - Shared names remain consistent across tasks: `QueueScrollComponent`, `BuildQueueItemText`, `BuildQueueItemCapabilitySummary`, `GetQueueCardHeight`, `GetQueueVisibleRowCount`, and `BuildDialogQueueRow(FontAsset font, EditorUiMetrics metrics, ...)`.

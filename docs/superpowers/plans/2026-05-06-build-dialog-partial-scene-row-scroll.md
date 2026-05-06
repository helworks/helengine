# Build Dialog Partial Scene Row Scroll Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Build dialog scene list allocate and render a partially visible trailing row instead of stopping at the previous full row boundary.

**Architecture:** Keep the existing `BuildDialog` scene-list virtualization and clipping. Fix only the visible-row capacity math so the scene-row pool rounds up when the viewport clips into the next row, then lock the behavior with a deterministic regression in `BuildDialogTests`.

**Tech Stack:** C#, xUnit, helengine editor UI, pooled `ScrollComponent` scene-list virtualization in `BuildDialog`.

---

### Task 1: Add a RED regression for partial scene-row rendering

**Files:**
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Add a failing test that forces a 3.5-row scene-list viewport**

Add this test near the existing `Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset` coverage in `engine/helengine.editor.tests/BuildDialogTests.cs`:

```csharp
/// <summary>
/// Ensures the scene list allocates one trailing pooled row when the viewport clips into the next row.
/// </summary>
[Fact]
public void UpdateSceneListRowsLayout_WhenViewportClipsNextRow_RendersPartiallyVisibleTrailingRow() {
    BuildDialog dialog = new BuildDialog(CreateFont());
    IReadOnlyList<string> sceneIds = CreateSceneIds(18);

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

    RoundedRectComponent sceneListBackground = GetPrivateField<RoundedRectComponent>(dialog, "SceneListBackground");
    ScrollComponent sceneListScrollComponent = GetPrivateField<ScrollComponent>(dialog, "SceneListScrollComponent");

    sceneListBackground.Size = new int2(
        sceneListBackground.Size.X,
        (BuildDialog.SceneListPadding * 2) + (BuildDialog.SceneRowHeight * 3) + (BuildDialog.SceneRowHeight / 2));
    sceneListScrollComponent.VisibleItemCount = 0;

    InvokePrivate(dialog, "UpdateSceneListRowsLayout");

    List<TextComponent> mapLabelTexts = GetPrivateField<List<TextComponent>>(dialog, "MapLabelTexts");

    Assert.Equal(4, sceneListScrollComponent.VisibleItemCount);
    Assert.Equal(4, mapLabelTexts.Count);
    Assert.Equal("Scenes/Map03.helen", mapLabelTexts[^1].Text);

    Assert.True(sceneListScrollComponent.ScrollTo(1));

    Assert.Equal("Scenes/Map01.helen", mapLabelTexts[0].Text);
    Assert.Equal("Scenes/Map04.helen", mapLabelTexts[^1].Text);
}
```

- [ ] **Step 2: Run the new test to verify RED**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.UpdateSceneListRowsLayout_WhenViewportClipsNextRow_RendersPartiallyVisibleTrailingRow"
```

Expected: FAIL with `VisibleItemCount` equal to `3` instead of `4`, proving the current row-capacity math rounds down.

- [ ] **Step 3: Commit the failing regression**

```powershell
git add engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "test: cover partial build scene row scrolling"
```

### Task 2: Change scene-list row capacity from floor to ceil

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Test: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Update the visible scene-row helper to round up**

In `engine/helengine.editor/components/ui/BuildDialog.cs`, replace the current helper:

```csharp
int GetSceneListVisibleRowCount() {
    int contentHeight = Math.Max(1, GetSceneListViewportHeight() - (GetSceneListPaddingPixels() * 2));
    return Math.Max(1, contentHeight / Math.Max(1, GetSceneRowHeightPixels()));
}
```

with:

```csharp
int GetSceneListVisibleRowCount() {
    int rowHeight = Math.Max(1, GetSceneRowHeightPixels());
    int contentHeight = Math.Max(1, GetSceneListViewportHeight() - (GetSceneListPaddingPixels() * 2));
    return Math.Max(1, (contentHeight + rowHeight - 1) / rowHeight);
}
```

- [ ] **Step 2: Keep the scene-row virtualization path unchanged apart from consuming the new count**

Do not restructure `UpdateSceneListRowsLayout()`, `EnsureSceneRowCount(int count)`, or `DisableSceneRow(BuildDialogSceneRow row)`. The intended result is that the existing pooled-row binding path now receives `4` rows for a 3.5-row viewport and the clip boundary naturally trims the bottom row.

The relevant code should remain structurally like this:

```csharp
void UpdateSceneListRowsLayout() {
    int visibleRowCount = SceneListScrollComponent.VisibleItemCount;
    if (visibleRowCount < 1) {
        visibleRowCount = GetSceneListVisibleRowCount();
    }

    SceneListScrollComponent.VisibleItemCount = visibleRowCount;
    SceneListScrollComponent.Size = new int2(GetSceneListViewportWidth(), GetSceneListViewportHeight());
    EnsureSceneRowCount(visibleRowCount);

    // Existing row binding and disable logic remains in place.
}
```

- [ ] **Step 3: Run the focused regression to verify GREEN**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.UpdateSceneListRowsLayout_WhenViewportClipsNextRow_RendersPartiallyVisibleTrailingRow"
```

Expected: PASS with `VisibleItemCount == 4` and the scrolled trailing row advancing from `Scenes/Map03.helen` to `Scenes/Map04.helen`.

- [ ] **Step 4: Run the adjacent Build dialog scroll coverage**

Run:

```powershell
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests.Show_WhenSceneRowsExceedViewport_VirtualizesRowsAndRespondsToScrollOffset|FullyQualifiedName~BuildDialogTests"
```

Expected: PASS, including the existing scene-list virtualization test and the rest of the Build dialog suite.

- [ ] **Step 5: Commit the implementation**

```powershell
git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "fix: render partial build scene rows"
```

# Asset Browser Right-Click Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Right-clicking an asset row selects that row first, then opens the existing asset context menu.

**Architecture:** Keep the change inside the asset browser view/panel pair. `AssetBrowserView` should expose a small selection helper that updates the persistent row selection without navigating or activating the asset, and `AssetBrowserPanel` should call that helper only when the right-click lands on a different row. The existing left-click activation path, folder navigation, and context menu contents should remain unchanged.

**Tech Stack:** C#, existing editor UI components, existing asset-browser tests.

---

### Task 1: Add the right-click selection behavior to the asset browser

**Files:**
- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Modify `engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs`
- Test `engine/helengine.editor.tests/AssetBrowserTabVisibilityTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one test that right-clicking a different asset row updates the persistent selection before the menu opens. The test should set up a small project with at least two files, right-click the second row, and assert that the browser now reports the second file as selected and the existing asset context menu is visible.

Add one test that right-clicking the already selected row does not re-emit a selection change. The test should capture the selection event count, right-click the same row twice, and assert that the count does not increase on the second right-click.

Example test shape:

```csharp
[Fact]
public void AssetBrowserPanel_WhenRightClickHitsDifferentRow_SelectsItBeforeShowingMenu() {
    string projectRoot = CreateProjectRoot();
    File.WriteAllText(Path.Combine(projectRoot, "assets", "alpha.txt"), "alpha");
    File.WriteAllText(Path.Combine(projectRoot, "assets", "beta.txt"), "beta");

    Core core = new Core(new CoreInitializationOptions {
        ContentRootPath = projectRoot
    });
    TestInputBackend input = new TestInputBackend();
    core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), input);

    AssetBrowserPanel panel = new AssetBrowserPanel(CreateFont(), projectRoot);
    panel.UpdateLayout(320, 240);

    AssetBrowserView view = GetPrivateField<AssetBrowserView>(panel, "BrowserView");
    List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(view, "Rows");

    int selectionCount = 0;
    panel.AssetSelected += _ => selectionCount++;

    SetMouseStateAndUpdate(core, input, panel, rows[1]);

    Assert.Equal("beta.txt", GetPrivateField<string>(view, "SelectedRelativePath"));
    Assert.True(GetPrivateField<ContextMenu>(panel, "AssetContextMenu").IsVisible);
    Assert.Equal(1, selectionCount);
}

[Fact]
public void AssetBrowserPanel_WhenRightClickHitsTheAlreadySelectedRow_DoesNotDuplicateSelectionEvents() {
    string projectRoot = CreateProjectRoot();
    File.WriteAllText(Path.Combine(projectRoot, "assets", "alpha.txt"), "alpha");

    Core core = new Core(new CoreInitializationOptions {
        ContentRootPath = projectRoot
    });
    TestInputBackend input = new TestInputBackend();
    core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), input);

    AssetBrowserPanel panel = new AssetBrowserPanel(CreateFont(), projectRoot);
    panel.UpdateLayout(320, 240);

    AssetBrowserView view = GetPrivateField<AssetBrowserView>(panel, "BrowserView");
    List<AssetBrowserRow> rows = GetPrivateField<List<AssetBrowserRow>>(view, "Rows");

    int selectionCount = 0;
    panel.AssetSelected += _ => selectionCount++;

    SetMouseStateAndUpdate(core, input, panel, rows[0]);
    SetMouseStateAndUpdate(core, input, panel, rows[0]);

    Assert.Equal("alpha.txt", GetPrivateField<string>(view, "SelectedRelativePath"));
    Assert.Equal(1, selectionCount);
}

void SetMouseStateAndUpdate(Core core, TestInputBackend input, AssetBrowserPanel panel, AssetBrowserRow row) {
    int2 panelPosition = new int2((int)Math.Round(panel.Position.X), (int)Math.Round(panel.Position.Y));
    int2 rowPoint = new int2(
        panelPosition.X + (int)Math.Round(row.Entity.Position.X) + 4,
        panelPosition.Y + (int)Math.Round(row.Entity.Position.Y) + 4);

    input.SetMouseState(new MouseState(
        rowPoint.X,
        rowPoint.Y,
        0,
        ButtonState.Released,
        ButtonState.Released,
        ButtonState.Released,
        ButtonState.Pressed,
        ButtonState.Released));

    core.Update();
}
```

Use the existing asset-browser test helpers and reflection patterns already in the test project so the new assertions can read the private browser state without changing production code just for the test.

- [ ] **Step 2: Run the new test and confirm it fails**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~AssetBrowser -v minimal
```

Expected: the new right-click selection assertions fail because the panel still opens the context menu without selecting a different row first.

- [ ] **Step 3: Implement the minimal browser selection helper and panel hit test**

Add a small browser helper in `AssetBrowserView` that can select one entry without activating it or navigating. Keep the helper narrow and make it reuse the existing persistent-selection fields and `RefreshRowSelectionVisuals()` path so the highlight updates through the same code that left-click selection already uses. The helper should not raise `AssetActivated`; the panel should only raise the selection event when the clicked row changed and the right-click was on a file entry.

Update `AssetBrowserPanel.UpdateContextMenuInput()` so it:

1. locates the row under the right-click position
2. compares that row’s relative path against the browser’s current persistent selection
3. selects the row first when the clicked row is different
4. raises the existing asset-selection event for file entries after the selection update
5. opens the existing context menu after the selection update

Keep the empty-space right-click path unchanged. Do not route this through the asset activation event, because the selection change should not navigate folders or double-trigger asset opening.

- [ ] **Step 4: Run the tests and verify they pass**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter FullyQualifiedName~AssetBrowser -v minimal
```

Expected: the new right-click selection tests pass, and the existing asset-browser tests continue to pass.

- [ ] **Step 5: Commit**

```powershell
git add engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor/components/ui/asset/AssetBrowserPanel.cs engine/helengine.editor.tests/AssetBrowserTabVisibilityTests.cs
git commit -m "Select asset rows before opening context menu"
```

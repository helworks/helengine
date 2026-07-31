# Top Menu Outside-Click Dismissal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the editor title-bar menu stack when a mouse press occurs anywhere outside its visible menus and top-level menu buttons.

**Architecture:** `EditorTitleBar` owns the complete title-bar menu stack, so it will expose one outside-press handler that checks its visible menu and button bounds before calling its existing `HideMenus` method. A small update component on the title-bar root will invoke that handler once per input frame, independently of the engine control that receives the click.

**Tech Stack:** C#/.NET 9, helengine pointer input, xUnit.

---

### Task 1: Reproduce outside-click dismissal

**Files:**
- Modify: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`
- Test: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`

- [ ] **Step 1: Write the failing test**

```csharp
[Fact]
public void FileMenu_WhenPointerPressesOutsideTitleBarMenuStack_ClosesMenu() {
    // Initialize the test input and title bar, open File, then press inside a dockable below the menu.
    // Assert.False(fileMenu.IsVisible);
}
```

- [ ] **Step 2: Run test to verify it fails**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~EditorTitleBarAddMenuTests.FileMenu_WhenPointerPressesOutsideTitleBarMenuStack_ClosesMenu"
```

Expected: FAIL because the title-bar menu remains visible after the outside press.

- [ ] **Step 3: Add the inside-menu preservation regression**

```csharp
[Fact]
public void FileMenu_WhenPointerPressesInsideVisibleMenu_RemainsOpenUntilActivation() {
    // Open File, press a visible row, and assert the menu is still visible before the release activates it.
}
```

- [ ] **Step 4: Run both regressions to verify the inside case currently passes and the outside case fails**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~EditorTitleBarAddMenuTests.FileMenu_WhenPointerPressesOutsideTitleBarMenuStack_ClosesMenu|FullyQualifiedName~EditorTitleBarAddMenuTests.FileMenu_WhenPointerPressesInsideVisibleMenu_RemainsOpenUntilActivation"
```

Expected: one outside-click failure and one inside-menu pass.

### Task 2: Route title-bar-wide outside presses

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Create: `engine/helengine.editor/components/ui/EditorTitleBarMenuDismissalUpdater.cs`
- Test: `engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs`

- [ ] **Step 1: Implement the minimal outside-press decision in `EditorTitleBar`**

```csharp
void DismissMenusForOutsidePointerPress(int2 pointer) {
    if (!AreAnyMenusVisible() || IsPointerInsideVisibleTitleBarMenuOrButton(pointer)) {
        return;
    }

    HideMenus();
}
```

The bounds check must include every visible top-level menu and submenu plus every title-bar menu button, so menu switching remains intact.

- [ ] **Step 2: Add the root-owned updater**

```csharp
public override void Update() {
    if (Core.Instance.Input.WasMouseLeftButtonPressed() || Core.Instance.Input.WasMouseRightButtonPressed()) {
        TitleBar.DismissMenusForOutsidePointerPress(Core.Instance.Input.GetMousePosition());
    }
}
```

Attach the updater to the title-bar root after menu construction. It must ignore frames without a mouse press.

- [ ] **Step 3: Run the two regressions to verify they pass**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~EditorTitleBarAddMenuTests.FileMenu_WhenPointerPressesOutsideTitleBarMenuStack_ClosesMenu|FullyQualifiedName~EditorTitleBarAddMenuTests.FileMenu_WhenPointerPressesInsideVisibleMenu_RemainsOpenUntilActivation"
```

Expected: PASS, two tests.

- [ ] **Step 4: Run title-bar menu coverage**

Run:

```powershell
dotnet test 'engine/helengine.editor.tests/helengine.editor.tests.csproj' --no-restore --filter "FullyQualifiedName~EditorTitleBarAddMenuTests|FullyQualifiedName~EditorTitleBarBuildMenuTests|FullyQualifiedName~EditorTitleBarProjectMenuTests"
```

Expected: PASS with no failures.

- [ ] **Step 5: Commit**

```powershell
git add -- 'engine/helengine.editor/components/ui/EditorTitleBar.cs' 'engine/helengine.editor/components/ui/EditorTitleBarMenuDismissalUpdater.cs' 'engine/helengine.editor.tests/EditorTitleBarAddMenuTests.cs'
git commit -m "fix: dismiss title menus on outside press"
```

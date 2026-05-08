# Current Map Dirty Title Indicator Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Show `*` in the editor title bar whenever the currently open map has unsaved changes, and remove it immediately after the map is fully saved again.

**Architecture:** Keep dirty-state ownership in `EditorSession`, which already owns `CurrentScenePath`, `IsSceneDirty`, save/open/reset flows, and `BuildWindowTitle()`. `EditorTitleBar` stays presentation-only and simply renders the composed title string it receives from `EditorSession`.

**Tech Stack:** C#/.NET 9, xUnit, existing editor session/title bar test suites

---

### Task 1: Add failing dirty-title regression coverage

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`

- [ ] **Step 1: Write the failing save-test coverage for dirty title state**

Add two tests to `EditorSessionSceneSaveTests.cs` near the existing title assertions:

```csharp
/// <summary>
/// Ensures mutating the current scene marks the visible map title as dirty.
/// </summary>
[Fact]
public void HandleSceneMutated_WhenCurrentSceneHasPath_AppendsDirtyMarkerToWindowTitle() {
    EditorSession session = CreateSessionForSceneSave();
    string currentScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirtyScene.helen");

    SetPrivateField(session, "CurrentScenePath", currentScenePath);
    InvokePrivate(session, "RefreshWindowTitle");
    InvokePrivate(session, "HandleSceneMutated");
    InvokePrivate(session, "RefreshWindowTitle");

    EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
    Assert.Equal("DirtyScene* - helengine - project.heproj", titleBar.Title);
}

/// <summary>
/// Ensures saving a dirty current scene clears the visible dirty marker from the title.
/// </summary>
[Fact]
public void HandleSceneSaveRequested_WhenDirtySceneIsSaved_RemovesDirtyMarkerFromWindowTitle() {
    EditorSession session = CreateSessionForSceneSave();
    string savePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "Saved.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(savePath));

    SetPrivateField(session, "CurrentScenePath", savePath);
    InvokePrivate(session, "HandleSceneMutated");
    InvokePrivate(session, "RefreshWindowTitle");
    InvokePrivate(session, "HandleSceneSaveRequested", savePath);

    EditorTitleBar titleBar = GetPrivateField<EditorTitleBar>(session, "titleBar");
    Assert.Equal("Saved - helengine - project.heproj", titleBar.Title);
}
```

- [ ] **Step 2: Write the failing platform-title coverage so the dirty suffix preserves the active-platform suffix**

Add one test to `EditorSessionPlatformsTests.cs` near `SetActiveProjectPlatform_WhenPlatformChanges_RefreshesWindowTitle`:

```csharp
/// <summary>
/// Ensures dirty scene titles preserve the active platform suffix.
/// </summary>
[Fact]
public void RefreshWindowTitle_WhenSceneIsDirtyAndPlatformIsActive_AppendsDirtyMarkerBeforePlatformSuffix() {
    EditorSession session = CreateSessionForPlatforms();
    string currentScenePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "DirtyScene.helen");

    SetPrivateField(session, "CurrentScenePath", currentScenePath);
    SetPrivateField(session, "ActiveProjectPlatform", "ps2");
    InvokePrivate(session, "HandleSceneMutated");
    InvokePrivate(session, "RefreshWindowTitle");

    Assert.Equal("DirtyScene* - helengine - project.heproj [PS2]", session.WindowTitle);
}
```

- [ ] **Step 3: Run the new focused tests to verify they fail for the right reason**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests.HandleSceneMutated_WhenCurrentSceneHasPath_AppendsDirtyMarkerToWindowTitle|FullyQualifiedName~EditorSessionSceneSaveTests.HandleSceneSaveRequested_WhenDirtySceneIsSaved_RemovesDirtyMarkerFromWindowTitle|FullyQualifiedName~EditorSessionPlatformsTests.RefreshWindowTitle_WhenSceneIsDirtyAndPlatformIsActive_AppendsDirtyMarkerBeforePlatformSuffix" -v minimal
```

Expected:

```text
FAIL
Expected: DirtyScene* - helengine - project.heproj
Actual:   DirtyScene - helengine - project.heproj
```

- [ ] **Step 4: Commit the failing tests**

```bash
git add engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionPlatformsTests.cs
git commit -m "test: cover dirty current map title indicator"
```

### Task 2: Implement the current-map dirty title composition in EditorSession

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`

- [ ] **Step 1: Add one explicit current-map dirty helper in `EditorSession`**

In `EditorSession.cs`, add a helper near `MarkSceneClean()` / `RefreshWindowTitle()`:

```csharp
/// <summary>
/// Returns whether the currently open map has unsaved editor changes.
/// </summary>
/// <returns>True when the current map should display a dirty marker.</returns>
bool IsCurrentMapDirty() {
    return IsSceneDirty;
}

/// <summary>
/// Appends the current-map dirty marker to one resolved scene display name when needed.
/// </summary>
/// <param name="sceneDisplayName">Resolved scene display name.</param>
/// <returns>Scene display name with the dirty marker applied when required.</returns>
string BuildSceneDisplayTitle(string sceneDisplayName) {
    if (string.IsNullOrWhiteSpace(sceneDisplayName)) {
        throw new InvalidOperationException("Scene display name must be provided.");
    }

    return IsCurrentMapDirty()
        ? $"{sceneDisplayName}*"
        : sceneDisplayName;
}
```

- [ ] **Step 2: Update `BuildWindowTitle()` to use the new current-map dirty helper**

Replace the current scene-title branch inside `BuildWindowTitle()` with:

```csharp
string BuildWindowTitle() {
    string platformSuffix = string.IsNullOrWhiteSpace(ActiveProjectPlatform)
        ? string.Empty
        : $" [{ActiveProjectPlatform.ToUpperInvariant()}]";
    string title = $"helengine - {ProjectDisplayName}{platformSuffix}";
    if (string.IsNullOrWhiteSpace(CurrentScenePath)) {
        return title;
    }

    string sceneDisplayName = ResolveSceneDisplayName(CurrentScenePath);
    string sceneTitle = BuildSceneDisplayTitle(sceneDisplayName);
    return $"{sceneTitle} - {title}";
}
```

- [ ] **Step 3: Refresh the visible window title immediately when scene dirty state changes**

Update the dirty-state methods in `EditorSession.cs`:

```csharp
/// <summary>
/// Marks the current scene as dirty after one user-authored mutation.
/// </summary>
void HandleSceneMutated() {
    IsSceneDirty = true;
    RefreshWindowTitle();
}

/// <summary>
/// Marks the current scene as clean after one successful save, load, or reset.
/// </summary>
void MarkSceneClean() {
    IsSceneDirty = false;
    RefreshWindowTitle();
}
```

Then remove the now-redundant explicit `RefreshWindowTitle();` calls immediately after `MarkSceneClean();` in:
- `HandleSceneSaveRequested`
- `LoadSceneIntoSession`
- `ResetToNewScene`

The resulting blocks should look like:

```csharp
SceneSaveService.Save(fullPath, CurrentSceneSettings);
CurrentScenePath = Path.GetFullPath(fullPath);
MarkSceneClean();
assetBrowserPanel.RefreshEntries();
saveFileDialog.Hide();
```

and:

```csharp
CurrentScenePath = Path.GetFullPath(fullPath);
CurrentSceneSettings = loadedSceneDocument.SceneSettings;
sceneCanvasProfileState.ApplySceneSettings(CurrentSceneSettings);
MarkSceneClean();
EditorSelectionService.ClearSelection();
```

- [ ] **Step 4: Run the focused tests to verify the implementation passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests.HandleSceneMutated_WhenCurrentSceneHasPath_AppendsDirtyMarkerToWindowTitle|FullyQualifiedName~EditorSessionSceneSaveTests.HandleSceneSaveRequested_WhenDirtySceneIsSaved_RemovesDirtyMarkerFromWindowTitle|FullyQualifiedName~EditorSessionPlatformsTests.RefreshWindowTitle_WhenSceneIsDirtyAndPlatformIsActive_AppendsDirtyMarkerBeforePlatformSuffix" -v minimal
```

Expected:

```text
PASS
3 passed
```

- [ ] **Step 5: Run the adjacent editor-session save/open/platform regression slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionPlatformsTests" -v minimal
```

Expected:

```text
PASS
All tests passed
```

- [ ] **Step 6: Commit the implementation**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionPlatformsTests.cs
git commit -m "feat: show dirty marker for current map title"
```

### Task 3: Final verification and branch completion

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`

- [ ] **Step 1: Run the final verified slice one more time from a clean tree**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionPlatformsTests" -v minimal
```

Expected:

```text
PASS
All tests passed
```

- [ ] **Step 2: Inspect the final diff for scope control**

Run:

```bash
git diff --stat HEAD~2..HEAD
```

Expected:

```text
Only EditorSession.cs and the targeted editor-session test files changed for this feature.
```

- [ ] **Step 3: Finish the branch**

Use the required completion workflow:

```text
Implementation complete. What would you like to do?

1. Merge back to main locally
2. Push and create a Pull Request
3. Keep the branch as-is (I'll handle it later)
4. Discard this work
```

## Self-Review

- Spec coverage:
  - dirty marker added when current map is unsaved: Task 1 + Task 2
  - dirty marker cleared after successful save: Task 1 + Task 2
  - current map title stays session-owned, title bar stays presentation-only: Task 2
  - platform suffix preserved: Task 1 + Task 2
- Placeholder scan:
  - no `TODO`, `TBD`, or “appropriate handling” placeholders remain
- Type consistency:
  - uses the existing `EditorSession`, `IsSceneDirty`, `RefreshWindowTitle()`, `BuildWindowTitle()`, and `EditorTitleBar.Title` seams already present in the codebase

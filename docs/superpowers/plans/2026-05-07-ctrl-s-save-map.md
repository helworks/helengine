# Ctrl+S Save Map Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add `Ctrl+S` as an editor-global shortcut that triggers the existing `Save Map` flow without changing plain `S` behavior or bypassing blocking modals.

**Architecture:** Detect `Ctrl+S` in `EditorKeyboardFocusUpdateComponent`, where editor-global keyboard commands already live, and route it into the existing `EditorSession.HandleSaveMapRequested()` path. Keep all save behavior in `EditorSession`, and use tests around the existing save/session seam rather than inventing a separate shortcut-only save implementation.

**Tech Stack:** C#/.NET 9, xUnit, existing editor keyboard focus and editor session tests

---

### Task 1: Add failing `Ctrl+S` regression coverage

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`

- [ ] **Step 1: Add a failing test for `Ctrl+S` saving the current scene when a path already exists**

Add this test to `EditorSessionSceneSaveTests.cs` near the existing save-title/save-path coverage:

```csharp
/// <summary>
/// Ensures Ctrl+S saves the current scene through the existing Save Map flow when a scene path already exists.
/// </summary>
[Fact]
public void HandleGlobalSaveShortcut_WhenCurrentScenePathExists_SavesTheScene() {
    EditorSession session = CreateSessionForSceneSave();
    string savePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "ShortcutSave.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(savePath));

    SetPrivateField(session, "CurrentScenePath", savePath);

    InvokePrivate(session, "HandleGlobalSaveShortcut");

    Assert.True(File.Exists(savePath));
    Assert.Equal(savePath, GetPrivateField<string>(session, "CurrentScenePath"));
}
```

- [ ] **Step 2: Add a failing test for `Ctrl+S` showing the save dialog when the current scene has no path**

Add this test to `EditorSessionSceneSaveTests.cs` near `HandleSaveMapRequested_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog`:

```csharp
/// <summary>
/// Ensures Ctrl+S shows the save dialog when the current scene has not been saved yet.
/// </summary>
[Fact]
public void HandleGlobalSaveShortcut_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog() {
    EditorSession session = CreateSessionForSceneSave();

    SetPrivateField(session, "CurrentScenePath", string.Empty);
    InvokePrivate(session, "HandleGlobalSaveShortcut");

    SaveFileDialog saveFileDialog = GetPrivateField<SaveFileDialog>(session, "saveFileDialog");
    Assert.True(saveFileDialog.IsVisible);
}
```

- [ ] **Step 3: Add a failing test for `Ctrl+S` being ignored while a blocking modal dialog is visible**

Add this test to `EditorSessionSceneOpenTests.cs`, where unsaved-changes/modal behavior is already covered:

```csharp
/// <summary>
/// Ensures Ctrl+S does not save the scene while a blocking modal dialog is visible.
/// </summary>
[Fact]
public void HandleGlobalSaveShortcut_WhenUnsavedChangesDialogIsVisible_DoesNothing() {
    EditorSession session = CreateSessionForSceneOpen();
    string savePath = Path.Combine(TempProjectRootPath, "assets", "Scenes", "BlockedShortcutSave.helen");
    Directory.CreateDirectory(Path.GetDirectoryName(savePath));

    SetPrivateField(session, "CurrentScenePath", savePath);
    UnsavedChangesDialog dialog = GetPrivateField<UnsavedChangesDialog>(session, "unsavedChangesDialog");
    dialog.Show();

    InvokePrivate(session, "HandleGlobalSaveShortcut");

    Assert.False(File.Exists(savePath));
    Assert.True(dialog.IsVisible);
}
```

- [ ] **Step 4: Run the focused shortcut tests to verify they fail for the expected missing-command reason**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests.HandleGlobalSaveShortcut_WhenCurrentScenePathExists_SavesTheScene|FullyQualifiedName~EditorSessionSceneSaveTests.HandleGlobalSaveShortcut_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog|FullyQualifiedName~EditorSessionSceneOpenTests.HandleGlobalSaveShortcut_WhenUnsavedChangesDialogIsVisible_DoesNothing" -v minimal
```

Expected:

```text
FAIL
Method not found: HandleGlobalSaveShortcut
```

- [ ] **Step 5: Commit the failing tests**

```bash
git add engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs
git commit -m "test: cover ctrl+s save map shortcut"
```

### Task 2: Implement the editor-global `Ctrl+S` save command route

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`

- [ ] **Step 1: Add one dedicated global save-shortcut handler to `EditorSession`**

In `EditorSession.cs`, add a new non-public method near `HandleSaveMapRequested()`:

```csharp
/// <summary>
/// Handles the editor-global Ctrl+S shortcut by routing into the existing Save Map flow when editor-global input is not blocked.
/// </summary>
void HandleGlobalSaveShortcut() {
    if (unsavedChangesDialog != null && unsavedChangesDialog.Enabled) {
        return;
    }

    if (saveFileDialog != null && saveFileDialog.Enabled) {
        return;
    }

    if (openFileDialog != null && openFileDialog.Enabled) {
        return;
    }

    if (reparentEntityDialog != null && reparentEntityDialog.Enabled) {
        return;
    }

    if (platformsDialog != null && platformsDialog.Enabled) {
        return;
    }

    if (profilesDialog != null && profilesDialog.Enabled) {
        return;
    }

    if (buildDialog != null && buildDialog.Enabled) {
        return;
    }

    if (buildDialogCopySettingsDialog != null && buildDialogCopySettingsDialog.Enabled) {
        return;
    }

    if (sceneSettingsDialog != null && sceneSettingsDialog.Enabled) {
        return;
    }

    if (preferencesDialog != null && preferencesDialog.Enabled) {
        return;
    }

    if (assetPickerModal != null && assetPickerModal.Enabled) {
        return;
    }

    HandleSaveMapRequested();
}
```

Keep it intentionally narrow: it should only decide whether the global shortcut is blocked, then delegate to the existing save-map handler.

- [ ] **Step 2: Route `Ctrl+S` through the existing editor-wide keyboard update component**

In `EditorKeyboardFocusUpdateComponent.cs`, update the keyboard handling chain so `Ctrl+S` is detected before the plain `S` activation path:

```csharp
InputSystem input = Core.Instance.Input;
bool controlPressed = input.IsKeyDown(Keys.LeftControl) || input.IsKeyDown(Keys.RightControl);
bool shiftPressed = input.IsKeyDown(Keys.LeftShift) || input.IsKeyDown(Keys.RightShift);

if (controlPressed && input.WasKeyPressed(Keys.Tab)) {
    EditorKeyboardFocusService.HandleCtrlTab(!shiftPressed);
} else if (!controlPressed && input.WasKeyPressed(Keys.Tab)) {
    EditorKeyboardFocusService.HandleTab(!shiftPressed);
} else if (controlPressed && input.WasKeyPressed(Keys.S)) {
    if (Core.Instance.Session is EditorSession session) {
        typeof(EditorSession)
            .GetMethod("HandleGlobalSaveShortcut", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(session, null);
    }
} else if (input.WasKeyPressed(Keys.Enter)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.Enter);
} else if (input.WasKeyPressed(Keys.Space)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.Space);
} else if (input.WasKeyPressed(Keys.W)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.W);
} else if (input.WasKeyPressed(Keys.R)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.R);
} else if (input.WasKeyPressed(Keys.S)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.S);
}
```

Then refactor immediately so this does **not** keep reflection in production code. Replace the reflective invocation with an explicit callback or strongly typed session seam already available in this component’s context. The final implementation should be a direct call path, not reflective dispatch.

The end state should be:

```csharp
} else if (controlPressed && input.WasKeyPressed(Keys.S)) {
    editorSession.HandleGlobalSaveShortcut();
} else if (input.WasKeyPressed(Keys.S)) {
    EditorKeyboardFocusService.HandleActivationKey(Keys.S);
}
```

If `EditorKeyboardFocusUpdateComponent` does not currently hold an `EditorSession` reference, add exactly that dependency and thread it through the existing session setup where the component is created.

- [ ] **Step 3: Run the focused shortcut tests to verify the implementation passes**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests.HandleGlobalSaveShortcut_WhenCurrentScenePathExists_SavesTheScene|FullyQualifiedName~EditorSessionSceneSaveTests.HandleGlobalSaveShortcut_WhenCurrentScenePathIsEmpty_ShowsSaveFileDialog|FullyQualifiedName~EditorSessionSceneOpenTests.HandleGlobalSaveShortcut_WhenUnsavedChangesDialogIsVisible_DoesNothing" -v minimal
```

Expected:

```text
PASS
3 passed
```

- [ ] **Step 4: Run the adjacent save/open/focus regression slice**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~TextBoxComponentKeyboardFocusTests" -v minimal
```

Expected:

```text
PASS
All tests passed
```

- [ ] **Step 5: Commit the implementation**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs
git commit -m "feat: add ctrl+s save map shortcut"
```

### Task 3: Final verification and branch completion

**Files:**
- Modify: none
- Test: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- Test: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Test: `engine/helengine.editor.tests/TextBoxComponentKeyboardFocusTests.cs`

- [ ] **Step 1: Run the final verified slice from the clean branch state**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~TextBoxComponentKeyboardFocusTests" -v minimal
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
Only EditorKeyboardFocusUpdateComponent.cs, EditorSession.cs, and the targeted editor-session test files changed for this feature.
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
  - `Ctrl+S` saves when the scene already has a path: Task 1 + Task 2
  - `Ctrl+S` opens save dialog when unsaved scene has no path: Task 1 + Task 2
  - `Ctrl+S` is blocked by modal dialogs: Task 1 + Task 2
  - plain `S` behavior remains unchanged: Task 2 + Task 3
- Placeholder scan:
  - no `TODO`, `TBD`, or vague “appropriate handling” placeholders remain
- Type consistency:
  - plan uses the existing `HandleSaveMapRequested()` save seam and the existing editor-wide keyboard update component, and explicitly requires a direct call path instead of reflection in the final implementation

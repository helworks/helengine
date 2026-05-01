# Tabbed Profiles Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the existing `ProfilesDialog` into a two-tab modal that separates Build and Graphics profiles and only commits edits when Save is pressed.

**Architecture:** Keep the existing profile persistence service and editor-session confirm flow. The dialog will keep a local staging copy of the current platform profile record in `CurrentDocument`, render two tabs (`Build Profiles` and `Graphics Profiles`), and keep edits in memory while the user switches tabs or platforms. Only the Save action should emit the staged document through `ProfilesDialogSelection`.

**Tech Stack:** C#, xUnit, existing editor modal system, existing editor-local JSON profile persistence.

---

## File Map

### Tabbed dialog refactor
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

### Intentionally unchanged in this slice
- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- `engine/helengine.editor/model/ProfilesDialogSelection.cs`

The session and persistence service already provide the correct commit boundary. This slice only changes how the dialog edits and stages data before Save.

## Task 1: Split the profiles modal into tabs with draft-only edits

**Files:**
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing tests**

Rewrite the dialog tests so they prove the modal starts on the Build tab, hides the Graphics tab content, and keeps edits local until Save:

```csharp
[Fact]
public void Show_WhenOpened_ActivatesBuildTabAndHidesGraphicsTab() {
    ProfilesDialog dialog = new ProfilesDialog(CreateFont());
    EditorProfileSettingsDocument document = CreateProfileDocument();

    dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

    int selectedTabIndex = GetPrivateField<int>(dialog, "SelectedTabIndex");
    EditorEntity buildContentHost = GetPrivateField<EditorEntity>(dialog, "BuildContentHost");
    EditorEntity graphicsContentHost = GetPrivateField<EditorEntity>(dialog, "GraphicsContentHost");

    Assert.Equal(0, selectedTabIndex);
    Assert.True(buildContentHost.Enabled);
    Assert.False(graphicsContentHost.Enabled);
}

[Fact]
public void Show_WhenTabChanges_KeepsDraftEditsOutOfTheSourceDocumentUntilSave() {
    ProfilesDialog dialog = new ProfilesDialog(CreateFont());
    EditorProfileSettingsDocument document = CreateProfileDocument();

    dialog.Show(document, new List<string> { "windows", "ps2" }, "windows");

    TextBoxComponent textureScaleTextBox = GetPrivateField<TextBoxComponent>(dialog, "TextureScaleTextBox");
    ComboBoxComponent platformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "PlatformComboBox");

    textureScaleTextBox.Text = "75";
    InvokePrivate(dialog, "HandleGraphicsTabClicked");
    InvokePrivate(dialog, "HandleBuildTabClicked");

    Assert.Equal("75", textureScaleTextBox.Text);
    Assert.Equal(50, document.Platforms[0].Build.TextureScalePercent);
    Assert.Equal("windows", platformComboBox.SelectedItem);

    ProfilesDialogSelection selection = null;
    dialog.ConfirmRequested += value => selection = value;
    InvokePrivate(dialog, "HandleSaveClicked");

    Assert.NotNull(selection);
    Assert.NotSame(document, selection.ProfileSettingsDocument);
    Assert.Equal(75, selection.ProfileSettingsDocument.Platforms[0].Build.TextureScalePercent);
    Assert.Equal(50, document.Platforms[0].Build.TextureScalePercent);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ProfilesDialogTests" -v minimal
```

Expected: the tests fail because `ProfilesDialog` still uses the single-page layout and mutates the loaded profile document directly.

- [ ] **Step 3: Write the minimal implementation**

Refactor `ProfilesDialog` so it stages edits in memory and only emits the staged document on Save:

```csharp
readonly EditorEntity BuildContentHost;
readonly EditorEntity GraphicsContentHost;
readonly ButtonComponent BuildTabButton;
readonly ButtonComponent GraphicsTabButton;
int SelectedTabIndex;

public void Show(EditorProfileSettingsDocument document, IReadOnlyList<string> supportedPlatforms, string activePlatformId) {
    CurrentDocument = CopyProfileSettingsDocument(document);
    SelectedTabIndex = 0;
    LoadSelectedPlatformIntoFields(activePlatformId);
    RefreshTabVisibility();
    Enabled = true;
}

void HandleBuildTabClicked() {
    if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
        StatusText.Text = errorMessage;
        return;
    }

    SelectedTabIndex = 0;
    LoadSelectedPlatformIntoFields(CurrentPlatformId);
    RefreshTabVisibility();
}

void HandleGraphicsTabClicked() {
    if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
        StatusText.Text = errorMessage;
        return;
    }

    SelectedTabIndex = 1;
    LoadSelectedPlatformIntoFields(CurrentPlatformId);
    RefreshTabVisibility();
}

void HandleSaveClicked() {
    if (!TryStoreCurrentPlatformFields(out string errorMessage)) {
        StatusText.Text = errorMessage;
        return;
    }

    ConfirmRequested?.Invoke(new ProfilesDialogSelection(ActivePlatformId, CurrentDocument));
}
```

The implementation should keep one draft document per platform record, and the active tab should only control which controls are visible. Switching tabs must copy the current field values into `CurrentDocument` before loading the next tab's controls.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the dialog tests pass, confirming that tab switches do not commit edits and Save returns a draft copy instead of mutating the original document.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/ProfilesDialog.cs engine/helengine.editor.tests/ProfilesDialogTests.cs docs/superpowers/plans/2026-05-01-platform-profiles-dialog.md
git commit -m "Refactor profiles dialog into tabs"
```

## Coverage Check

- Two tabs instead of one merged section: covered by Task 1.
- Draft-only edits while switching tabs: covered by Task 1.
- Save-only commit boundary: covered by Task 1.
- Existing session and persistence flow staying unchanged: covered by the architecture and file map notes.

The plan intentionally leaves `EditorSession`, `EditorProfileSettingsService`, and `ProfilesDialogSelection` unchanged unless the compile step exposes an unexpected mismatch.

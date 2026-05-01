# Persistent Active Build Platform Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let the editor persist and change the current active platform from Build Settings while keeping `supportedPlatforms` as the project build list and leaving the Windows host backend unchanged.

**Architecture:** The Build Settings dialog becomes the place where the user edits both the enabled platform list and the persistent active platform. `BuildSettingsSelection` will carry both pieces of state back to `EditorSession`, which will then rewrite `project.heproj`, update `settings/project.json`, and refresh `AssetImportManager.CurrentPlatformId`. Existing build and import consumers already read `CurrentProjectPlatform`, so the main work is to make that value user-editable and durable across editor restarts.

**Tech Stack:** C#, xUnit, existing editor UI components, `helengine.projectfile`, `helengine.platforms`, editor-local settings in `user_settings/project.json`.

---

## File Map

### Build Settings dialog and selection model
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/model/BuildSettingsSelection.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`

### Editor session persistence and active-platform fallback
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

### Downstream active-platform regressions
- Modify: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

## Task 1: Add the persistent active-platform control to Build Settings

**Files:**
- Modify: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Modify: `engine/helengine.editor/model/BuildSettingsSelection.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`

- [ ] **Step 1: Write the failing dialog tests**

Add focused coverage in `BuildSettingsDialogTests.cs` for the new active-platform control and the widened selection model:

```csharp
[Fact]
public void Show_WhenActivePlatformIsProvided_SeedsTheActivePlatformComboBox() {
    BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());

    dialog.Show(
        CreateAvailablePlatforms("windows", "linux", "android"),
        new List<string> { "windows", "linux" },
        "linux");

    ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
    Assert.True(activePlatformComboBox.HasSelection);
    Assert.Equal("Linux Vulkan", activePlatformComboBox.SelectedItem);
}
```

```csharp
[Fact]
public void HandleSaveClicked_WhenActivePlatformChanges_RaisesConfirmWithActivePlatformId() {
    BuildSettingsDialog dialog = new BuildSettingsDialog(CreateFont());
    BuildSettingsSelection raisedSelection = null;
    dialog.ConfirmRequested += selection => raisedSelection = selection;

    dialog.Show(
        CreateAvailablePlatforms("windows", "linux"),
        new List<string> { "windows", "linux" },
        "windows");

    ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
    List<CheckBoxComponent> platformCheckBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
    activePlatformComboBox.SelectedIndex = 1;
    platformCheckBoxes[0].IsChecked = true;
    platformCheckBoxes[1].IsChecked = true;

    InvokePrivate(dialog, "HandleSaveClicked");

    Assert.NotNull(raisedSelection);
    Assert.Equal("linux", raisedSelection.ActivePlatformId);
    Assert.Equal(new[] { "windows", "linux" }, raisedSelection.SelectedPlatformIds);
}
```

The tests should fail until the dialog accepts the current active platform, renders it through a dedicated combo box, and returns it in the confirm result.

- [ ] **Step 2: Run the dialog tests and confirm the failure is the expected missing behavior**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the new tests fail because `BuildSettingsDialog` does not yet expose an active-platform combo box and `BuildSettingsSelection` does not yet carry the active platform id

- [ ] **Step 3: Implement the dialog changes**

Update the dialog and selection model to make the new tests pass:

```csharp
public sealed class BuildSettingsSelection {
    public string ActivePlatformId { get; }
    public IReadOnlyList<string> SelectedPlatformIds { get; }

    public BuildSettingsSelection(string activePlatformId, IReadOnlyList<string> selectedPlatformIds) {
        if (string.IsNullOrWhiteSpace(activePlatformId)) {
            throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId));
        }
        if (selectedPlatformIds == null) {
            throw new ArgumentNullException(nameof(selectedPlatformIds));
        }

        ActivePlatformId = activePlatformId;
        SelectedPlatformIds = new List<string>(selectedPlatformIds);
    }
}
```

In `BuildSettingsDialog.cs`:
- add an `ActivePlatformComboBox` above the fixed 3-column table
- seed it from the installed-and-supported platform list
- keep missing platforms visible in the table but exclude them from the active-platform selector
- change `Show(...)` to accept the current active platform id
- change `HandleSaveClicked()` to create `new BuildSettingsSelection(activePlatformId, selectedPlatformIds)`

Keep the 3-column table intact:
- `Platform Name`
- `Status`
- `Enabled`

- [ ] **Step 4: Re-run the dialog tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- all `BuildSettingsDialogTests` pass

- [ ] **Step 5: Commit the dialog slice**

```bash
git add engine/helengine.editor/components/ui/BuildSettingsDialog.cs engine/helengine.editor/model/BuildSettingsSelection.cs engine/helengine.editor.tests/BuildSettingsDialogTests.cs
git commit -m "Add active platform selector to build settings dialog"
```

## Task 2: Persist and validate the active platform in EditorSession

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

- [ ] **Step 1: Write the failing session tests**

Add tests that prove Build Settings now controls the persistent active platform and that the session falls back when the active platform is no longer valid:

```csharp
[Fact]
public async Task HandleBuildSettingsRequested_WhenInvoked_ShowsDialogWithCurrentActivePlatformSelected() {
    await WriteProjectFileAsync(new List<string> { "windows", "linux" }, "1.0.0-custom");
    WritePlatformManifest(
        "1.0.0-custom",
        new List<AvailablePlatformDescriptor> {
            new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
            new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", true)
        },
        new List<string> { "windows", "linux" });
    EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows", "linux" });
    localSettingsService.SaveActivePlatform("linux");
    EditorSession session = CreateSession(new List<string> { "windows", "linux" }, localSettingsService, "linux");

    InvokePrivate(session, "HandleBuildSettingsRequested");

    BuildSettingsDialog dialog = GetPrivateField<BuildSettingsDialog>(session, "buildSettingsDialog");
    ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
    Assert.Equal("Linux Vulkan", activePlatformComboBox.SelectedItem);
}
```

```csharp
[Fact]
public async Task HandleBuildSettingsDialogConfirmed_WhenActivePlatformChanges_PersistsTheNewPlatform() {
    await WriteProjectFileAsync(new List<string> { "windows", "linux" }, "1.0.0-custom");
    WritePlatformManifest(
        "1.0.0-custom",
        new List<AvailablePlatformDescriptor> {
            new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
            new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", true)
        },
        new List<string> { "windows", "linux" });
    EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows", "linux" });
    localSettingsService.SaveActivePlatform("windows");
    EditorSession session = CreateSession(new List<string> { "windows", "linux" }, localSettingsService, "windows");

    InvokePrivate(session, "HandleBuildSettingsDialogConfirmed", new BuildSettingsSelection("linux", new List<string> { "windows", "linux" }));

    ProjectFileReadResult readResult = await new ProjectFileReader().ReadAsync(ProjectFilePath);
    Assert.True(readResult.Succeeded);
    Assert.Equal("linux", session.CurrentProjectPlatform);
    Assert.Equal("linux", GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService").LoadActivePlatform());
    Assert.Equal("linux", GetPrivateField<AssetImportManager>(session, "assetImportManager").CurrentPlatformId);
}
```

```csharp
[Fact]
public async Task HandleBuildSettingsDialogConfirmed_WhenActivePlatformIsMissing_FallsBackToTheFirstSupportedInstalledPlatform() {
    await WriteProjectFileAsync(new List<string> { "windows", "linux" }, "1.0.0-custom");
    WritePlatformManifest(
        "1.0.0-custom",
        new List<AvailablePlatformDescriptor> {
            new AvailablePlatformDescriptor("windows", "Windows DirectX", string.Empty, "platforms/windows", true),
            new AvailablePlatformDescriptor("linux", "Linux Vulkan", string.Empty, "platforms/linux", false)
        },
        new List<string> { "windows" });
    EditorProjectLocalSettingsService localSettingsService = new EditorProjectLocalSettingsService(TempProjectRootPath, new List<string> { "windows", "linux" });
    localSettingsService.SaveActivePlatform("linux");
    EditorSession session = CreateSession(new List<string> { "windows", "linux" }, localSettingsService, "linux");

    InvokePrivate(session, "HandleBuildSettingsDialogConfirmed", new BuildSettingsSelection("linux", new List<string> { "windows" }));

    Assert.Equal("windows", session.CurrentProjectPlatform);
    Assert.Equal("windows", GetPrivateField<EditorProjectLocalSettingsService>(session, "ProjectLocalSettingsService").LoadActivePlatform());
}
```

These tests should fail until `EditorSession` seeds the dialog with the current active platform, persists the selected active platform separately from `supportedPlatforms`, and revalidates the active platform against installed platforms.

- [ ] **Step 2: Run the session tests and confirm the failure is the expected missing wiring**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildSettingsTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the new tests fail because the dialog is still only receiving `supportedPlatforms`, and confirmation still only rewrites the supported-platform list

- [ ] **Step 3: Implement the EditorSession changes**

Update `EditorSession.cs` so the active platform is treated as persisted state, not just an implicit fallback:
- change `HandleBuildSettingsRequested()` to pass `CurrentProjectPlatform` into `BuildSettingsDialog.Show(...)`
- change `HandleBuildSettingsDialogConfirmed(...)` to save `SupportedPlatformIds` to `project.heproj`, save `ActivePlatformId` to `settings/project.json`, then refresh `ActiveProjectPlatform` and `assetImportManager.CurrentPlatformId`
- keep `ResolveNextActiveProjectPlatform(...)` as the fallback helper, but make it validate the currently selected active platform first and then fall back to the first supported installed platform
- when the loaded active platform is invalid during startup, persist the fallback immediately so the editor reopens on the last valid installed platform

The dialog should still be the only UI surface that edits this value; `EditorSession` owns the persistence and synchronization.

- [ ] **Step 4: Re-run the session tests and verify they pass**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildSettingsTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- all `EditorSessionBuildSettingsTests` pass

- [ ] **Step 5: Commit the session slice**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs
git commit -m "Persist active build platform in editor session"
```

## Task 3: Prove the active platform continues to drive downstream editor behavior

**Files:**
- Modify: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Add the downstream regression tests**

Add one test that proves the import/settings path uses the new active platform after the Build Settings dialog changes it, and one test that proves the Build dialog still opens with that updated platform:

```csharp
[Fact]
public void HandleBuildSettingsDialogConfirmed_WhenActivePlatformChanges_UpdatesAssetImportSettingsPlatform() {
    EditorSession session = CreateSession();
    AssetImportManager manager = GetPrivateField<AssetImportManager>(session, "assetImportManager");

    InvokePrivate(session, "HandleBuildSettingsDialogConfirmed", new BuildSettingsSelection("windows", new List<string> { "windows", "android" }));

    Assert.Equal("windows", session.CurrentProjectPlatform);
    Assert.Equal("windows", manager.CurrentPlatformId);
}
```

```csharp
[Fact]
public void HandleBuildRequested_WhenActivePlatformChanges_ShowsBuildDialogForTheNewPlatform() {
    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(TempProjectRootPath);
    EditorBuildQueueService buildQueueService = new EditorBuildQueueService(buildConfigService, new TestEditorBuildExecutor([]));
    EditorSession session = CreateSession(buildConfigService, buildQueueService, "linux");

    InvokePrivate(session, "HandleBuildRequested");

    BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
    Assert.True(dialog.IsVisible);
    Assert.Equal("linux", GetPrivateField<string>(dialog, "ActivePlatformId"));
}
```

These tests should be narrow: they only prove that the rest of the editor continues to follow `CurrentProjectPlatform` after the persistent active-platform change.

- [ ] **Step 2: Run the downstream regression tests**

Run:

```bash
dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionAssetImportSettingsTests|FullyQualifiedName~EditorSessionBuildQueueTests" -p:UseCommonOutputDirectory=true -p:CopyLocalLockFileAssemblies=true -p:UseSharedCompilation=false -p:BuildInParallel=false -m:1 -nr:false -v minimal
```

Expected:
- the assertions pass and confirm the active platform still drives import settings and build dialog state after the Build Settings change

- [ ] **Step 3: Commit the downstream regression slice**

```bash
git add engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs
git commit -m "Verify active platform propagates through editor workflows"
```

## Self-Review Checklist

- The plan covers the spec requirement that the current platform is persistent and separate from `supportedPlatforms`.
- The plan covers the Build Settings dialog UI change and the session persistence flow.
- The plan keeps the runtime host backend unchanged.
- The plan does not introduce a second platform source of truth.
- The plan does not depend on any undefined helper methods or file paths.

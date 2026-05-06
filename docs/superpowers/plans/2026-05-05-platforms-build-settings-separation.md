# Platforms And Build Settings Separation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separate project platform enablement from builder-owned platform configuration so `Platforms...` owns `settings/platforms.json`, active-platform choice stays user-local, and build/profile dialogs only show enabled-and-available platforms.

**Architecture:** Salvage the useful title-bar and modal scaffolding already on `main`, but replace the stale ownership model. Introduce one project-shared platform settings service for `settings/platforms.json`, keep active platform in `EditorProjectLocalSettingsService`, and move shared builder-owned per-platform profile data to flat `settings/platform.<platform-id>.json` files while leaving local build queue/output state under `user_settings`.

**Tech Stack:** C#, xUnit, existing editor modal components, existing installed-platform resolution in `helengine.platforms`, JSON settings files under `settings` and `user_settings`.

---

## File Map

### Project platform topology
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformsService.cs`
- Create: `engine/helengine.editor/model/PlatformsSelection.cs`
- Create: `engine/helengine.editor/components/ui/PlatformsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/PlatformsDialogTests.cs`
- Create: `engine/helengine.editor.tests/EditorProjectPlatformsServiceTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`

### Per-platform shared builder settings
- Modify: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/model/ProfilesDialogSelection.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

### Enabled-and-available filtering for build flows
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

### Solution wiring
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

## Task 1: Add project-shared platform settings service for `settings/platforms.json`

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectPlatformsService.cs`
- Create: `engine/helengine.editor.tests/EditorProjectPlatformsServiceTests.cs`

- [ ] **Step 1: Write the failing settings-service tests**

Create `engine/helengine.editor.tests/EditorProjectPlatformsServiceTests.cs` with focused coverage for:

```csharp
/// <summary>
/// Ensures project-supported platforms are loaded from settings/platforms.json instead of .heproj.
/// </summary>
[Fact]
public void Load_WhenPlatformsFileExists_ReturnsConfiguredSupportedPlatforms() {
    string projectRootPath = CreateTempProjectRoot();
    Directory.CreateDirectory(Path.Combine(projectRootPath, "settings"));
    File.WriteAllText(Path.Combine(projectRootPath, "settings", "platforms.json"), """
    {
      "supportedPlatforms": [ "windows", "ps2" ]
    }
    """);

    EditorProjectPlatformsService service = new EditorProjectPlatformsService(projectRootPath);

    EditorProjectPlatformsDocument document = service.Load();

    Assert.Equal(new[] { "windows", "ps2" }, document.SupportedPlatforms);
}

/// <summary>
/// Ensures the service seeds one minimal windows-only file when the project settings file is missing.
/// </summary>
[Fact]
public void Load_WhenPlatformsFileIsMissing_CreatesDefaultWindowsDocument() {
    string projectRootPath = CreateTempProjectRoot();
    EditorProjectPlatformsService service = new EditorProjectPlatformsService(projectRootPath);

    EditorProjectPlatformsDocument document = service.Load();

    Assert.Equal(new[] { "windows" }, document.SupportedPlatforms);
    Assert.True(File.Exists(Path.Combine(projectRootPath, "settings", "platforms.json")));
}

/// <summary>
/// Ensures the service rejects empty supported-platform lists because Platforms... must not save zero platforms.
/// </summary>
[Fact]
public void Save_WhenSupportedPlatformsIsEmpty_Throws() {
    string projectRootPath = CreateTempProjectRoot();
    EditorProjectPlatformsService service = new EditorProjectPlatformsService(projectRootPath);

    EditorProjectPlatformsDocument document = new EditorProjectPlatformsDocument {
        SupportedPlatforms = []
    };

    Assert.Throws<InvalidOperationException>(() => service.Save(document));
}
```

- [ ] **Step 2: Run the new settings-service tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectPlatformsServiceTests" -v minimal
```

Expected:
- `FAIL` because `EditorProjectPlatformsService` and `EditorProjectPlatformsDocument` do not exist yet.

- [ ] **Step 3: Implement `EditorProjectPlatformsDocument` and `EditorProjectPlatformsService`**

Add `engine/helengine.editor/managers/project/EditorProjectPlatformsDocument.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores the project-shared supported platform ids persisted in settings/platforms.json.
    /// </summary>
    public sealed class EditorProjectPlatformsDocument {
        /// <summary>
        /// Gets or sets the project-supported platform identifiers.
        /// </summary>
        public List<string> SupportedPlatforms { get; set; } = [];
    }
}
```

Add `engine/helengine.editor/managers/project/EditorProjectPlatformsService.cs` with this core shape:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Loads and persists project-shared supported platforms stored in settings/platforms.json.
    /// </summary>
    public sealed class EditorProjectPlatformsService {
        static JsonSerializerOptions JsonSerializerOptions { get; } = new() {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        string ProjectRootPath { get; }
        string PlatformsFilePath => Path.Combine(ProjectRootPath, "settings", "platforms.json");

        public EditorProjectPlatformsService(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            ProjectRootPath = Path.GetFullPath(projectRootPath);
        }

        public EditorProjectPlatformsDocument Load() {
            EditorProjectPlatformsDocument document = TryLoadDocument();
            if (document == null) {
                document = CreateDefaultDocument();
                Save(document);
            }

            Normalize(document);
            return document;
        }

        public void Save(EditorProjectPlatformsDocument document) {
            if (document == null) {
                throw new ArgumentNullException(nameof(document));
            }

            Normalize(document);
            if (document.SupportedPlatforms.Count == 0) {
                throw new InvalidOperationException("At least one supported platform is required.");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(PlatformsFilePath));
            string json = JsonSerializer.Serialize(document, JsonSerializerOptions);
            File.WriteAllText(PlatformsFilePath, json);
        }
    }
}
```

- [ ] **Step 4: Re-run the settings-service tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectPlatformsServiceTests" -v minimal
```

Expected:
- all `EditorProjectPlatformsServiceTests` pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorProjectPlatformsDocument.cs engine/helengine.editor/managers/project/EditorProjectPlatformsService.cs engine/helengine.editor.tests/EditorProjectPlatformsServiceTests.cs
rtk git commit -m "feat: add project platform settings service"
```

## Task 2: Replace stale BuildSettings ownership with a real `Platforms...` modal

**Files:**
- Create: `engine/helengine.editor/model/PlatformsSelection.cs`
- Create: `engine/helengine.editor/components/ui/PlatformsDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Create: `engine/helengine.editor.tests/PlatformsDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs`

- [ ] **Step 1: Write the failing platforms-dialog tests**

Create `engine/helengine.editor.tests/PlatformsDialogTests.cs` with coverage like:

```csharp
/// <summary>
/// Ensures the dialog renders enabled-platform checkboxes and an active-platform dropdown constrained to enabled entries.
/// </summary>
[Fact]
public void Show_WhenOpened_PopulatesCheckboxesAndActivePlatformDropdown() {
    PlatformsDialog dialog = new PlatformsDialog(CreateFont());

    dialog.Show(
        new[] { "windows", "ps2", "linux" },
        new[] { "windows", "ps2" },
        "ps2");

    ComboBoxComponent activePlatformComboBox = GetPrivateField<ComboBoxComponent>(dialog, "ActivePlatformComboBox");
    Assert.Equal("ps2", activePlatformComboBox.SelectedItem);
    Assert.Equal(2, activePlatformComboBox.Items.Count);
}

/// <summary>
/// Ensures saving is blocked when the active platform is no longer one of the enabled platforms.
/// </summary>
[Fact]
public void HandleSaveClicked_WhenActivePlatformIsNotEnabled_LeavesDialogOpenAndShowsValidation() {
    PlatformsDialog dialog = new PlatformsDialog(CreateFont());
    dialog.Show(new[] { "windows", "ps2" }, new[] { "windows", "ps2" }, "ps2");

    List<CheckBoxComponent> checkBoxes = GetPrivateField<List<CheckBoxComponent>>(dialog, "PlatformCheckBoxes");
    checkBoxes[1].IsChecked = false;

    InvokePrivate(dialog, "HandleSaveClicked");

    TextComponent statusText = GetPrivateField<TextComponent>(dialog, "StatusText");
    Assert.Contains("active platform", statusText.Text, StringComparison.OrdinalIgnoreCase);
    Assert.True(dialog.Enabled);
}
```

- [ ] **Step 2: Run the new platform-dialog tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformsDialogTests|FullyQualifiedName~EditorTitleBarBuildMenuTests" -v minimal
```

Expected:
- `FAIL` because `PlatformsDialog` and `PlatformsSelection` do not exist, and the title-bar event still talks about build settings.

- [ ] **Step 3: Implement `PlatformsSelection`, `PlatformsDialog`, and title-bar rename**

Add `engine/helengine.editor/model/PlatformsSelection.cs`:

```csharp
namespace helengine.editor {
    /// <summary>
    /// Stores one confirmed project platform selection from Platforms....
    /// </summary>
    public sealed class PlatformsSelection {
        public PlatformsSelection(IReadOnlyList<string> supportedPlatformIds, string activePlatformId) {
            SupportedPlatformIds = supportedPlatformIds ?? throw new ArgumentNullException(nameof(supportedPlatformIds));
            ActivePlatformId = string.IsNullOrWhiteSpace(activePlatformId)
                ? throw new ArgumentException("Active platform id must be provided.", nameof(activePlatformId))
                : activePlatformId;
        }

        public IReadOnlyList<string> SupportedPlatformIds { get; }
        public string ActivePlatformId { get; }
    }
}
```

Key `EditorTitleBar` changes:

```csharp
public event Action PlatformsRequested;

IReadOnlyList<ContextMenuItem> BuildBuildMenuItems() {
    return new ContextMenuItem[] {
        new ContextMenuItem("Platforms...", RaisePlatformsRequested),
        new ContextMenuItem("Profiles...", RaiseProfilesRequested),
        new ContextMenuItem("Build...", RaiseBuildRequested),
        new ContextMenuItem("Build Scripts...", RaiseBuildScriptsRequested),
        new ContextMenuItem("Open in IDE...", RaiseOpenInIDERequested)
    };
}
```

Key `PlatformsDialog` save validation:

```csharp
void HandleSaveClicked() {
    List<string> selectedPlatformIds = CollectSelectedPlatformIds();
    if (selectedPlatformIds.Count == 0) {
        StatusText.Text = "Select at least one supported platform.";
        return;
    }

    string activePlatformId = ActivePlatformComboBox.SelectedItem;
    if (string.IsNullOrWhiteSpace(activePlatformId) || !selectedPlatformIds.Contains(activePlatformId, StringComparer.OrdinalIgnoreCase)) {
        StatusText.Text = "Choose an active platform from the enabled platforms.";
        return;
    }

    ConfirmRequested?.Invoke(new PlatformsSelection(selectedPlatformIds, activePlatformId));
}
```

- [ ] **Step 4: Re-run the platform-dialog and title-bar tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~PlatformsDialogTests|FullyQualifiedName~EditorTitleBarBuildMenuTests" -v minimal
```

Expected:
- all platform-dialog and build-menu tests pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/model/PlatformsSelection.cs engine/helengine.editor/components/ui/PlatformsDialog.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/PlatformsDialogTests.cs engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs
rtk git commit -m "feat: add platforms dialog ownership flow"
```

## Task 3: Rewire `EditorSession` to use `settings/platforms.json` and explicit active-platform selection

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Create: `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs`

- [ ] **Step 1: Write the failing editor-session platform-flow tests**

Create `engine/helengine.editor.tests/EditorSessionPlatformsTests.cs` with focused coverage for:

```csharp
/// <summary>
/// Ensures Platforms... opens from the title bar using settings/platforms.json instead of .heproj supportedPlatforms.
/// </summary>
[Fact]
public void HandlePlatformsRequested_WhenInvoked_ShowsPlatformsDialogFromProjectSettingsFile() {
    EditorSession session = CreateSession("windows");
    WritePlatformsSettings(new[] { "windows", "ps2" });

    InvokePrivate(session, "HandlePlatformsRequested");

    PlatformsDialog dialog = GetPrivateField<PlatformsDialog>(session, "platformsDialog");
    Assert.True(dialog.Enabled);
}

/// <summary>
/// Ensures saving Platforms... writes settings/platforms.json and updates the user-local active platform explicitly selected by the dialog.
/// </summary>
[Fact]
public void HandlePlatformsDialogConfirmed_WhenSelectionIsValid_WritesProjectPlatformsAndUserActivePlatform() {
    EditorSession session = CreateSession("windows");
    InvokePrivate(session, "HandlePlatformsDialogConfirmed", new PlatformsSelection(new[] { "windows", "ps2" }, "ps2"));

    string json = File.ReadAllText(Path.Combine(ProjectRootPath, "settings", "platforms.json"));
    Assert.Contains("\"ps2\"", json);
    Assert.Equal("ps2", GetPrivateField<string>(session, "ActiveProjectPlatform"));
}
```

- [ ] **Step 2: Run the editor-session platform-flow tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPlatformsTests" -v minimal
```

Expected:
- `FAIL` because `EditorSession` still uses `BuildSettingsDialog`, `.heproj`, and automatic active-platform fallback.

- [ ] **Step 3: Replace stale session build-settings flow with platforms flow**

Key `EditorSession` constructor field changes:

```csharp
readonly EditorProjectPlatformsService projectPlatformsService;
PlatformsDialog platformsDialog;
```

Key load-time supported-platform source:

```csharp
projectPlatformsService = new EditorProjectPlatformsService(projectPath);
ProjectSupportedPlatforms = projectPlatformsService.Load().SupportedPlatforms.AsReadOnly();
ProjectLocalSettingsService = new EditorProjectLocalSettingsService(projectPath, ProjectSupportedPlatforms);
ActiveProjectPlatform = ProjectLocalSettingsService.LoadActivePlatform();
```

Key handler shape:

```csharp
void HandlePlatformsRequested() {
    EditorProjectPlatformsDocument document = projectPlatformsService.Load();
    IReadOnlyList<string> availablePlatformIds = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion)
        .Select(platform => platform.Id)
        .OrderBy(platformId => platformId, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    platformsDialog.Show(availablePlatformIds, document.SupportedPlatforms, ActiveProjectPlatform);
}

void HandlePlatformsDialogConfirmed(PlatformsSelection selection) {
    projectPlatformsService.Save(new EditorProjectPlatformsDocument {
        SupportedPlatforms = new List<string>(selection.SupportedPlatformIds)
    });

    ProjectSupportedPlatforms = selection.SupportedPlatformIds.ToArray();
    SetActiveProjectPlatform(selection.ActivePlatformId);
    assetImportManager.CurrentPlatformId = ActiveProjectPlatform;
    platformsDialog.Hide();
}
```

Delete the stale build-settings ownership path from `EditorSession` entirely:

- remove the `BuildSettingsDialog buildSettingsDialog;` field and replace all remaining references with `PlatformsDialog platformsDialog;`
- remove `HandleBuildSettingsRequested()` and replace the title-bar subscription with `PlatformsRequested += HandlePlatformsRequested`
- remove `HandleBuildSettingsDialogConfirmed(BuildSettingsSelection selection)` so `EditorSession` no longer treats `Build Settings...` as the owner of project-supported platforms
- delete `SaveProjectSupportedPlatforms(IReadOnlyList<string> supportedPlatforms)` because `settings/platforms.json` is now owned by `EditorProjectPlatformsService`
- delete `ApplySupportedPlatforms(IReadOnlyList<string> supportedPlatforms)` because the dialog must require an explicit active-platform selection instead of auto-repairing local state
- delete `ResolveNextActiveProjectPlatform(IReadOnlyList<string> supportedPlatforms)` because fallback selection is no longer valid behavior under the approved spec

- [ ] **Step 4: Re-run the editor-session platform-flow tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionPlatformsTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected:
- all new platform-flow tests pass.
- local settings tests still pass without auto-fallback behavior being reintroduced elsewhere.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs engine/helengine.editor.tests/EditorSessionPlatformsTests.cs
rtk git commit -m "feat: move project platform ownership into platforms dialog"
```

## Task 4: Split shared per-platform profile settings into `settings/platform.<platform-id>.json`

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Modify: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Modify: `engine/helengine.editor/model/ProfilesDialogSelection.cs`
- Modify: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing per-platform profile persistence tests**

Add tests covering:

```csharp
/// <summary>
/// Ensures saving one platform profile writes only settings/platform.<id>.json.
/// </summary>
[Fact]
public void Save_WhenOnePlatformChanges_WritesOnlyThatPlatformFile() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(ProjectRootPath);
    EditorProfileSettingsDocument document = CreateProfileDocument("windows", "ps2");

    service.Save(document);

    Assert.True(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform.windows.json")));
    Assert.True(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform.ps2.json")));
}

/// <summary>
/// Ensures loading supported+available platforms does not rewrite an unavailable platform file.
/// </summary>
[Fact]
public void Load_WhenSupportedPlatformIsUnavailable_LeavesItsFileUntouched() {
    SeedPlatformProfileFile("windows");
    SeedPlatformProfileFile("ps2");

    EditorProfileSettingsService service = new EditorProfileSettingsService(ProjectRootPath);
    EditorProfileSettingsDocument document = service.Load(new[] { "windows" });

    Assert.Single(document.Platforms);
    Assert.Equal("windows", document.Platforms[0].PlatformId);
    Assert.True(File.Exists(Path.Combine(ProjectRootPath, "settings", "platform.ps2.json")));
}
```

- [ ] **Step 2: Run the profile persistence tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected:
- `FAIL` because `EditorProfileSettingsService` still reads and writes `user_settings/profile_config.json`.

- [ ] **Step 3: Refactor profile settings service to one file per platform**

Key file-path helpers:

```csharp
string SettingsDirectoryPath => Path.Combine(ProjectRootPath, "settings");

string GetPlatformFilePath(string platformId) {
    return Path.Combine(SettingsDirectoryPath, $"platform.{platformId}.json");
}
```

Key load behavior:

```csharp
public EditorProfileSettingsDocument Load(IReadOnlyList<string> supportedPlatforms) {
    EditorProfileSettingsDocument document = new EditorProfileSettingsDocument();
    for (int index = 0; index < supportedPlatforms.Count; index++) {
        string platformId = supportedPlatforms[index];
        document.Platforms.Add(LoadOrCreatePlatformDocument(platformId));
    }

    return document;
}
```

Key save behavior:

```csharp
public void Save(EditorProfileSettingsDocument document) {
    Directory.CreateDirectory(SettingsDirectoryPath);
    for (int index = 0; index < document.Platforms.Count; index++) {
        EditorPlatformProfileSettingsDocument platform = document.Platforms[index];
        string json = JsonSerializer.Serialize(platform, JsonSerializerOptions);
        File.WriteAllText(GetPlatformFilePath(platform.PlatformId), json);
    }
}
```

Update `ProfilesDialog` to keep taking an explicit platform list from `EditorSession`, but assume it is already filtered to enabled-and-available platforms.

- [ ] **Step 4: Re-run the profile persistence tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected:
- all updated profile settings tests pass.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/managers/project/EditorProfileSettingsService.cs engine/helengine.editor/components/ui/ProfilesDialog.cs engine/helengine.editor/model/ProfilesDialogSelection.cs engine/helengine.editor.tests/ProfilesDialogTests.cs engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs
rtk git commit -m "feat: split shared platform profile settings by file"
```

## Task 5: Filter build/profile dialogs to enabled-and-available platforms only

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify: `engine/helengine.editor.tests/BuildDialogTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Write the failing enabled-and-available filtering tests**

Add coverage like:

```csharp
/// <summary>
/// Ensures Profiles... receives only the alphabetical intersection of enabled and available platforms.
/// </summary>
[Fact]
public void HandleProfilesRequested_WhenSomePlatformsAreUnavailable_ShowsOnlyEnabledAndAvailablePlatforms() {
    EditorSession session = CreateSession("windows");
    SetPrivateField(session, "ProjectSupportedPlatforms", new[] { "ps2", "windows" });
    ConfigureAvailablePlatforms(session, new[] { "windows" });

    InvokePrivate(session, "HandleProfilesRequested");

    ProfilesDialog dialog = GetPrivateField<ProfilesDialog>(session, "profilesDialog");
    List<string> supportedPlatformIds = GetPrivateField<List<string>>(dialog, "SupportedPlatformIds");
    Assert.Equal(new[] { "windows" }, supportedPlatformIds);
}

/// <summary>
/// Ensures Build... only shows tabs for enabled platforms that are also currently available.
/// </summary>
[Fact]
public void HandleBuildRequested_WhenProjectContainsUnavailablePlatforms_HidesUnavailableTabs() {
    EditorSession session = CreateSession("windows");
    SetPrivateField(session, "ProjectSupportedPlatforms", new[] { "ps2", "windows" });
    ConfigureAvailablePlatforms(session, new[] { "windows" });

    InvokePrivate(session, "HandleBuildRequested");

    BuildDialog dialog = GetPrivateField<BuildDialog>(session, "buildDialog");
    Assert.Equal("windows", dialog.SelectedPlatformId);
}
```

- [ ] **Step 2: Run the filtering tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~BuildDialogTests" -v minimal
```

Expected:
- `FAIL` because `EditorSession` still passes `SupportedPlatforms` directly into `ProfilesDialog` and `BuildDialog`.

- [ ] **Step 3: Add enabled-and-available filtering in `EditorSession`**

Add one helper:

```csharp
IReadOnlyList<string> ResolveVisibleConfiguredPlatforms() {
    HashSet<string> availablePlatformIds = availablePlatformProviderResolver.LoadPlatforms(RequiredEngineVersion)
        .Select(platform => platform.Id)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    return ProjectSupportedPlatforms
        .Where(platformId => availablePlatformIds.Contains(platformId))
        .OrderBy(platformId => platformId, StringComparer.OrdinalIgnoreCase)
        .ToArray();
}
```

Use it in both dialog entry points:

```csharp
void HandleProfilesRequested() {
    IReadOnlyList<string> visiblePlatforms = ResolveVisibleConfiguredPlatforms();
    EditorProfileSettingsDocument profileSettings = profileSettingsService.Load(visiblePlatforms);
    profilesDialog.Show(profileSettings, visiblePlatforms, ActiveProjectPlatform, ResolvePlatformSelectionModel(ActiveProjectPlatform));
}

void HandleBuildRequested() {
    IReadOnlyList<string> visiblePlatforms = ResolveVisibleConfiguredPlatforms();
    EditorBuildConfigDocument buildConfig = buildConfigService.Load(visiblePlatforms, sceneCatalogService.ResolveSceneId(CurrentScenePath));
    buildDialog.Show(visiblePlatforms, sceneCatalogService.GetSceneIds(), ActiveProjectPlatform, buildConfig, ResolvePlatformSelectionModel(ActiveProjectPlatform));
}
```

Keep `EditorBuildConfigService` local to `user_settings/build_config.json`, but only seed/load entries for the visible platform set passed in by the session.

- [ ] **Step 4: Re-run the filtering tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~ProfilesDialogTests" -v minimal
```

Expected:
- all filtering tests pass.
- unavailable enabled platforms remain hidden from both dialogs.

- [ ] **Step 5: Commit**

```bash
rtk git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/managers/project/EditorBuildConfigService.cs engine/helengine.editor.tests/BuildDialogTests.cs engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs engine/helengine.editor.tests/ProfilesDialogTests.cs
rtk git commit -m "feat: filter build configuration dialogs by available platforms"
```

## Task 6: Final verification

**Files:**
- Verify all files touched in Tasks 1-5

- [ ] **Step 1: Run the focused platforms/build-settings suite**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectPlatformsServiceTests|FullyQualifiedName~PlatformsDialogTests|FullyQualifiedName~EditorSessionPlatformsTests|FullyQualifiedName~ProfilesDialogTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~EditorSessionBuildQueueTests" -v minimal
```

Expected:
- all focused topology/build-settings tests pass.

- [ ] **Step 2: Run editor project builds**

Run:

```bash
rtk dotnet build engine/helengine.editor/helengine.editor.csproj --no-restore
rtk dotnet build engine/helengine.editor.tests/helengine.editor.tests.csproj --no-restore
```

Expected:
- both builds succeed with `0 errors`.

- [ ] **Step 3: Commit any final plan-driven cleanup**

```bash
rtk git add engine/helengine.editor engine/helengine.editor.tests
rtk git commit -m "complete platforms and build settings separation"
```

## Self-Review

- Spec coverage:
  - `Platforms...` ownership of project-supported platforms and active-platform selection is covered by Tasks 1-3.
  - `settings/platforms.json` project storage is covered by Task 1 and Task 3.
  - enabled-and-available filtering for builder-facing configuration is covered by Task 5.
  - per-platform flat shared settings files are covered by Task 4.
  - preservation of unavailable platform files is covered by Task 4 and Task 5 tests.
- Placeholder scan:
  - no `TODO`, `TBD`, or vague “handle appropriately” language remains in the task steps.
  - each code-changing task includes exact files, test targets, and concrete method shapes.
- Type consistency:
  - `PlatformsDialog`, `PlatformsSelection`, `EditorProjectPlatformsDocument`, and `EditorProjectPlatformsService` are used consistently across all tasks.
  - `ResolveVisibleConfiguredPlatforms()` is the single helper name used for enabled-and-available filtering.

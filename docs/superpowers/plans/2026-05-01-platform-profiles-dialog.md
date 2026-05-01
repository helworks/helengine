# Platform Profiles Dialog Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add one `Profiles...` build-menu dialog that edits platform-scoped build and graphics profile defaults, then persists those values in editor-local settings.

**Architecture:** Keep this as a single editor modal with one platform selector and two internal sections: Build Profiles and Graphics Profiles. Store the values in a new editor-local profile settings document separate from build queue state so the profiles can evolve without tangling queue persistence. The dialog should edit the currently active platform's profile record and reopen on whatever platform the session currently considers active.

**Tech Stack:** C#, xUnit, existing editor modal system, existing title-bar menu plumbing, existing editor-local JSON persistence patterns.

---

## File Map

### Profile settings persistence
- Create: `engine/helengine.editor/managers/project/EditorProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorBuildProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs`

### Profiles dialog and menu wiring
- Create: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Create: `engine/helengine.editor/model/ProfilesDialogSelection.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/ProfilesDialogTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionProfilesTests.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs`

## Task 1: Add editor-local profile persistence

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorPlatformProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorBuildProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProfileSettingsService.cs`
- Create: `engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs`

- [ ] **Step 1: Write the failing tests**

Add tests that prove the profile store seeds every supported platform and preserves values per platform:

```csharp
[Fact]
public void Load_WhenProfileFileIsMissing_SeedsDefaultBuildAndGraphicsProfilesForEachSupportedPlatform() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(TempProjectRootPath);

    EditorProfileSettingsDocument document = service.Load(new[] { "windows", "ps2" });

    Assert.Equal(2, document.Platforms.Count);
    Assert.Equal("windows", document.Platforms[0].PlatformId);
    Assert.Equal(100, document.Platforms[0].Build.TextureScalePercent);
    Assert.True(document.Platforms[0].Graphics.VSyncEnabled);
    Assert.Equal("ps2", document.Platforms[1].PlatformId);
}

[Fact]
public void SaveAndReload_PreservesPlatformSpecificBuildAndGraphicsProfileValues() {
    EditorProfileSettingsService service = new EditorProfileSettingsService(TempProjectRootPath);
    EditorProfileSettingsDocument document = new EditorProfileSettingsDocument {
        Platforms = new List<EditorPlatformProfileSettingsDocument> {
            new EditorPlatformProfileSettingsDocument {
                PlatformId = "windows",
                Build = new EditorBuildProfileSettingsDocument {
                    TextureScalePercent = 75,
                    ShaderVariantPruningEnabled = false
                },
                Graphics = new EditorGraphicsProfileSettingsDocument {
                    DefaultWidth = 1920,
                    DefaultHeight = 1080,
                    VSyncEnabled = false,
                    FullscreenEnabled = true
                }
            }
        }
    };

    service.Save(document);

    EditorProfileSettingsDocument reloaded = service.Load(new[] { "windows" });
    Assert.Equal(75, reloaded.Platforms[0].Build.TextureScalePercent);
    Assert.False(reloaded.Platforms[0].Graphics.VSyncEnabled);
    Assert.True(reloaded.Platforms[0].Graphics.FullscreenEnabled);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProfileSettingsServiceTests" -v minimal
```

Expected: the tests fail because the new profile document and service do not exist yet.

- [ ] **Step 3: Write the minimal implementation**

Add the new profile document shape and service:

```csharp
public sealed class EditorProfileSettingsDocument {
    public List<EditorPlatformProfileSettingsDocument> Platforms { get; set; } = [];
}

public sealed class EditorPlatformProfileSettingsDocument {
    public string PlatformId { get; set; } = string.Empty;
    public EditorBuildProfileSettingsDocument Build { get; set; } = new EditorBuildProfileSettingsDocument();
    public EditorGraphicsProfileSettingsDocument Graphics { get; set; } = new EditorGraphicsProfileSettingsDocument();
}

public sealed class EditorBuildProfileSettingsDocument {
    public int TextureScalePercent { get; set; } = 100;
    public bool ShaderVariantPruningEnabled { get; set; } = true;
}

public sealed class EditorGraphicsProfileSettingsDocument {
    public int DefaultWidth { get; set; } = 1280;
    public int DefaultHeight { get; set; } = 720;
    public bool VSyncEnabled { get; set; } = true;
    public bool FullscreenEnabled { get; set; } = false;
}
```

`EditorProfileSettingsService` should mirror the existing local-settings services:

```csharp
public EditorProfileSettingsDocument Load(IReadOnlyList<string> supportedPlatforms);
public void Save(EditorProfileSettingsDocument document);
```

It should persist to `user_settings/profile_config.json`, normalize missing platform entries, and seed new platforms with default build and graphics profile records.

- [ ] **Step 4: Run the test to verify it passes**

Run the same `dotnet test` command again.

Expected: the new profile-settings tests pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/managers/project/EditorProfileSettingsDocument.cs engine/helengine.editor/managers/project/EditorPlatformProfileSettingsDocument.cs engine/helengine.editor/managers/project/EditorBuildProfileSettingsDocument.cs engine/helengine.editor/managers/project/EditorGraphicsProfileSettingsDocument.cs engine/helengine.editor/managers/project/EditorProfileSettingsService.cs engine/helengine.editor.tests/managers/project/EditorProfileSettingsServiceTests.cs
git commit -m "Add editor profile settings persistence"
```

## Task 2: Add the `Profiles...` dialog and title-bar menu entry

**Files:**
- Create: `engine/helengine.editor/components/ui/ProfilesDialog.cs`
- Create: `engine/helengine.editor/model/ProfilesDialogSelection.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs`
- Create: `engine/helengine.editor.tests/ProfilesDialogTests.cs`

- [ ] **Step 1: Write the failing tests**

Add one menu test and one dialog test:

```csharp
[Fact]
public void ToggleBuildMenu_ShowsPlatformsProfilesAndBuildCommands() {
    EditorTitleBar titleBar = new EditorTitleBar(CreateFont(), 1280, 720, "Hel");

    InvokePrivate(titleBar, "ToggleBuildMenu");

    ContextMenu buildMenu = GetPrivateField<ContextMenu>(titleBar, "BuildMenu");
    List<ContextMenuItem> activeItems = GetPrivateField<List<ContextMenuItem>>(buildMenu, "ActiveItems");

    Assert.Collection(
        activeItems,
        item => Assert.Equal("Platforms...", item.Label),
        item => Assert.Equal("Profiles...", item.Label),
        item => Assert.Equal("Build...", item.Label),
        item => Assert.Equal("Build Scripts...", item.Label),
        item => Assert.Equal("Open in IDE...", item.Label));
}

[Fact]
public void ProfilesDialog_WhenSwitchingPlatforms_RestoresEachPlatformsValues() {
    ProfilesDialog dialog = new ProfilesDialog(CreateFont());
    EditorProfileSettingsDocument profiles = new EditorProfileSettingsDocument {
        Platforms = new List<EditorPlatformProfileSettingsDocument> {
            new EditorPlatformProfileSettingsDocument {
                PlatformId = "windows",
                Build = new EditorBuildProfileSettingsDocument { TextureScalePercent = 50 },
                Graphics = new EditorGraphicsProfileSettingsDocument { DefaultWidth = 1920, DefaultHeight = 1080, VSyncEnabled = false }
            },
            new EditorPlatformProfileSettingsDocument {
                PlatformId = "ps2",
                Build = new EditorBuildProfileSettingsDocument { TextureScalePercent = 25 },
                Graphics = new EditorGraphicsProfileSettingsDocument { DefaultWidth = 640, DefaultHeight = 480, VSyncEnabled = true }
            }
        }
    };

    dialog.Show(
        new[] { "windows", "ps2" },
        "windows",
        profiles);

    CheckBoxComponent vsyncCheckBox = GetPrivateField<CheckBoxComponent>(dialog, "VSyncCheckBox");
    Assert.False(vsyncCheckBox.IsChecked);

    InvokePrivate(dialog, "HandlePlatformTabClicked", "ps2");
    Assert.True(vsyncCheckBox.IsChecked);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarBuildMenuTests|FullyQualifiedName~ProfilesDialogTests" -v minimal
```

Expected:
- the title-bar test fails because `Profiles...` is not yet in the Build menu
- the dialog test fails because `ProfilesDialog` and its selection model do not exist yet

- [ ] **Step 3: Write the minimal implementation**

Add the new menu entry and a dialog that mirrors the current-platform tab pattern used elsewhere:

```csharp
new ContextMenuItem("Profiles...", RaiseProfilesRequested)
```

`ProfilesDialog` should:
- accept the active platform id and the loaded profile document
- render one platform tab row
- render a Build Profiles section with fields for `TextureScalePercent` and `ShaderVariantPruningEnabled`
- render a Graphics Profiles section with fields for `DefaultWidth`, `DefaultHeight`, and `VSyncEnabled`
- raise a confirm event carrying a `ProfilesDialogSelection` for the currently selected platform

`ProfilesDialogSelection` should carry the current platform id and the edited profile record so the session can persist the chosen platform's values without guessing.

- [ ] **Step 4: Run the tests to verify they pass**

Run the same `dotnet test` command again.

Expected: the menu test and the dialog test pass.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/components/ui/ProfilesDialog.cs engine/helengine.editor/model/ProfilesDialogSelection.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs engine/helengine.editor.tests/ProfilesDialogTests.cs
git commit -m "Add platform profiles dialog and menu entry"
```

## Task 3: Wire the dialog into the editor session

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorSessionProfilesTests.cs`

- [ ] **Step 1: Write the failing session tests**

Add tests that prove the session opens the dialog on the current platform and persists the edited profile values:

```csharp
[Fact]
public void HandleProfilesRequested_OpensProfilesDialogForCurrentPlatform() {
    EditorSession session = CreateSession();

    InvokePrivate(session, "HandleProfilesRequested");

    ProfilesDialog dialog = GetPrivateField<ProfilesDialog>(session, "profilesDialog");
    Assert.True(dialog.IsVisible);
    Assert.Equal(session.CurrentProjectPlatform, GetPrivateField<string>(dialog, "ActivePlatformId"));
}

[Fact]
public void HandleProfilesDialogConfirmed_PersistsTheEditedPlatformProfileValues() {
    EditorSession session = CreateSession();
    ProfilesDialogSelection selection = new ProfilesDialogSelection(
        "windows",
        new EditorBuildProfileSettingsDocument {
            TextureScalePercent = 80,
            ShaderVariantPruningEnabled = false
        },
        new EditorGraphicsProfileSettingsDocument {
            DefaultWidth = 1600,
            DefaultHeight = 900,
            VSyncEnabled = false,
            FullscreenEnabled = true
        });

    InvokePrivate(session, "HandleProfilesDialogConfirmed", selection);

    EditorProfileSettingsDocument reloaded = session.ProfileSettingsService.Load(new[] { "windows" });
    Assert.Equal(80, reloaded.Platforms[0].Build.TextureScalePercent);
    Assert.True(reloaded.Platforms[0].Graphics.FullscreenEnabled);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```bash
rtk dotnet test /mnt/c/dev/helworks/helengine/engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionProfilesTests" -v minimal
```

Expected: the tests fail because the session does not yet own a profiles dialog or profile settings service.

- [ ] **Step 3: Write the minimal implementation**

Add the new session-owned service and dialog, then wire the menu event:

```csharp
readonly EditorProfileSettingsService profileSettingsService;
readonly ProfilesDialog profilesDialog;
```

and wire the title-bar event:

```csharp
titleBar.ProfilesRequested += HandleProfilesRequested;
```

The session should:
- load the profile document after the project path and supported platforms are known
- open the dialog on the current active platform
- save the edited profile values on confirm
- merge the returned `ProfilesDialogSelection` back into the persisted profile document for the selected platform
- keep the platform invariant: the dialog always edits a real active platform, never an empty placeholder platform

- [ ] **Step 4: Run the tests to verify they pass**

Run the same `dotnet test` command again, plus the menu test slice if needed.

Expected: the session tests pass and the previous dialog/menu tests stay green.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorSessionProfilesTests.cs
git commit -m "Wire profiles dialog into editor session"
```

## Coverage Check

- One dialog with two sections: covered by Task 2.
- Platform-scoped persistence: covered by Task 1 and Task 3.
- Menu entry in Build: covered by Task 2.
- Active platform persistence and restore behavior: covered by Task 3.

The plan intentionally leaves runtime/build consumption of the new profile values out of scope. That should be a follow-up slice once the dialog and persistence shape are in place.

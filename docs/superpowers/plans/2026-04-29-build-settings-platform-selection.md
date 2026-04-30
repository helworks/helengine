# Build Settings Platform Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a `Build` top-level editor menu and a `Build Settings...` modal that lets users change `supportedPlatforms` in `project.heproj` using a machine-available platform list instead of a hardcoded project-only list.

**Architecture:** Keep the title bar presentation-only, add a dedicated build-settings modal in the editor UI layer, and introduce a small shared platform-discovery library that reads launcher-managed install metadata when available and falls back to source-build development defaults when it is not.

**Tech Stack:** C#, xUnit, shared `helengine.projectfile` persistence, new shared platform-discovery project, custom editor UI components, Windows registry-backed launcher install locators, existing editor session modal wiring.

---

## File Map

### Shared platform discovery
- Create: `engine/helengine.platforms/helengine.platforms.csproj`
- Create: `engine/helengine.platforms/AvailablePlatformDescriptor.cs`
- Create: `engine/helengine.platforms/IAvailablePlatformProvider.cs`
- Create: `engine/helengine.platforms/AvailablePlatformProviderResolver.cs`
- Create: `engine/helengine.platforms/DevelopmentPlatformProvider.cs`
- Create: `engine/helengine.platforms/InstalledPlatformProvider.cs`
- Create: `engine/helengine.platforms/PlatformDiscoveryOptions.cs`
- Create: `engine/helengine.platforms/LauncherInstallRoots.cs`
- Create: `engine/helengine.platforms/WindowsLauncherInstallRootLocator.cs`
- Create: `engine/helengine.platforms/InstalledEnginePlatformBinding.cs`
- Create: `engine/helengine.platforms/InstalledBindingManifest.cs`
- Create: `engine/helengine.platforms/InstalledBindingStore.cs`
- Create: `engine/helengine.platforms.tests/helengine.platforms.tests.csproj`
- Create: `engine/helengine.platforms.tests/AvailablePlatformProviderResolverTests.cs`

### Editor build-settings UI and session integration
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Create: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Create: `engine/helengine.editor/model/BuildSettingsSelection.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Modify: `engine/helengine.editor/helengine.editor.csproj`

### Editor regression coverage
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`
- Create: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`
- Modify: `engine/helengine.editor.tests/helengine.editor.tests.csproj`

### Solution wiring
- Modify: `helengine.ui/helengine.sln`

## Task 1: Add shared platform-discovery library

**Files:**
- Create: `engine/helengine.platforms/helengine.platforms.csproj`
- Create: `engine/helengine.platforms/AvailablePlatformDescriptor.cs`
- Create: `engine/helengine.platforms/IAvailablePlatformProvider.cs`
- Create: `engine/helengine.platforms/AvailablePlatformProviderResolver.cs`
- Create: `engine/helengine.platforms/DevelopmentPlatformProvider.cs`
- Create: `engine/helengine.platforms/InstalledPlatformProvider.cs`
- Create: `engine/helengine.platforms/PlatformDiscoveryOptions.cs`
- Create: `engine/helengine.platforms/LauncherInstallRoots.cs`
- Create: `engine/helengine.platforms/WindowsLauncherInstallRootLocator.cs`
- Create: `engine/helengine.platforms/InstalledEnginePlatformBinding.cs`
- Create: `engine/helengine.platforms/InstalledBindingManifest.cs`
- Create: `engine/helengine.platforms/InstalledBindingStore.cs`
- Create: `engine/helengine.platforms.tests/helengine.platforms.tests.csproj`
- Create: `engine/helengine.platforms.tests/AvailablePlatformProviderResolverTests.cs`
- Modify: `helengine.ui/helengine.sln`

- [ ] **Step 1: Write the failing provider-resolution tests**

Create `engine/helengine.platforms.tests/AvailablePlatformProviderResolverTests.cs` with focused cases for:
- launcher-managed installed bindings returning the platform ids for a required engine version
- a development override root taking precedence over launcher registry data
- missing launcher data falling back to a built-in `windows` platform
- an engine version with no matching installed bindings returning an empty list instead of all known bindings

- [ ] **Step 2: Run the new shared-platform tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.platforms.tests/helengine.platforms.tests.csproj -v minimal
```

Expected:
- the test project does not exist yet, so this first run fails until the new shared library and tests are added

- [ ] **Step 3: Create the shared project and file-backed manifest readers**

Add `engine/helengine.platforms/helengine.platforms.csproj` and implement:
- `AvailablePlatformDescriptor` as the normalized editor-facing platform record
- `IAvailablePlatformProvider` as the discovery contract
- `InstalledPlatformProvider` to read launcher-style `installed-bindings.json` under the resolved shared toolchain root
- `DevelopmentPlatformProvider` to return an override-backed list or a built-in `windows` fallback
- `AvailablePlatformProviderResolver` to apply the required resolution order:
  1. development override
  2. launcher registry/manifests
  3. built-in fallback

Keep this project launcher-UI-free. It can reuse the persisted JSON shape, but it must not reference `helengine.launcher`.

- [ ] **Step 4: Re-run the shared-platform tests**

Run:

```bash
rtk dotnet test engine/helengine.platforms.tests/helengine.platforms.tests.csproj -v minimal
```

Expected:
- all `AvailablePlatformProviderResolverTests` pass

- [ ] **Step 5: Commit the shared platform-discovery slice**

```bash
git add engine/helengine.platforms engine/helengine.platforms.tests helengine.ui/helengine.sln
git commit -m "Add shared platform discovery library"
```

## Task 2: Add Build menu dispatch in the editor title bar

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarTests.cs`

- [ ] **Step 1: Write the failing title-bar tests**

Extend `engine/helengine.editor.tests/EditorTitleBarTests.cs` with focused coverage that proves:
- a `Build` top-level button appears to the right of `Add`
- clicking `Build` opens a context menu containing `Build Settings...`
- selecting `Build Settings...` raises a new `BuildSettingsRequested` event

- [ ] **Step 2: Run the focused title-bar tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarTests" -v minimal
```

Expected:
- the new build-menu assertions fail because the title bar currently only exposes `File` and `Add`

- [ ] **Step 3: Implement the Build menu and event**

Update `engine/helengine.editor/components/ui/EditorTitleBar.cs` to:
- add a `Build` menu button using the existing top-left menu layout pattern
- create `Build Settings...` as the initial build-menu item
- expose a `BuildSettingsRequested` event without embedding project mutation logic into the title bar

Keep the title bar aligned with the existing `File` and `Add` menu construction.

- [ ] **Step 4: Re-run the focused title-bar tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarTests" -v minimal
```

Expected:
- all title-bar tests pass, including the new build-menu coverage

- [ ] **Step 5: Commit the title-bar slice**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarTests.cs
git commit -m "Add build menu to editor title bar"
```

## Task 3: Add the Build Settings modal UI

**Files:**
- Create: `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- Create: `engine/helengine.editor/model/BuildSettingsSelection.cs`
- Create: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`

- [ ] **Step 1: Write the failing modal tests**

Create `engine/helengine.editor.tests/BuildSettingsDialogTests.cs` with focused tests that verify:
- one checkbox row is rendered for each available platform
- the initial checked state comes from the current `supportedPlatforms`
- the dialog rejects save when all platforms are unchecked
- confirming the dialog returns the selected platform ids in a stable order

Use the existing modal-style tests from `OpenFileDialogTests` and `ReparentEntityDialog` flows as the behavioral reference.

- [ ] **Step 2: Run the focused dialog tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests" -v minimal
```

Expected:
- the new dialog tests fail because the dialog and selection model do not exist yet

- [ ] **Step 3: Implement the build-settings modal**

Add:
- `BuildSettingsDialog` as an editor-managed modal with title, platform checkbox rows, save/cancel buttons, and inline validation
- `BuildSettingsSelection` as the dialog result model used by `EditorSession`

Behavior:
- initialize from the available platform list plus current `supportedPlatforms`
- keep Save disabled or reject confirmation when no platforms are selected
- surface a clear error label when the provider returns no platforms
- keep the UI presentation inside the dialog and leave persistence to `EditorSession`

- [ ] **Step 4: Re-run the focused dialog tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildSettingsDialogTests" -v minimal
```

Expected:
- all `BuildSettingsDialogTests` pass

- [ ] **Step 5: Commit the modal slice**

```bash
git add engine/helengine.editor/components/ui/BuildSettingsDialog.cs engine/helengine.editor/model/BuildSettingsSelection.cs engine/helengine.editor.tests/BuildSettingsDialogTests.cs
git commit -m "Add build settings dialog"
```

## Task 4: Wire Build Settings into the editor session and project file

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Modify: `engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

- [ ] **Step 1: Write the failing session-integration tests**

Create `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs` with focused coverage for:
- `BuildSettingsRequested` showing the modal from the title bar
- saving a new platform selection rewriting `project.heproj` `supportedPlatforms`
- removing the current active platform rewriting `settings/project.json` to the first remaining supported platform
- saving a selection that still contains the active platform preserving that local setting
- the session refreshing its in-memory `SupportedPlatforms` and active import platform after save

Reuse the existing reflection-based modal/session test style already present in `EditorSessionSceneOpenTests`, `EditorSessionSceneHierarchyReparentTests`, and `EditorSessionAssetImportSettingsTests`.

- [ ] **Step 2: Run the focused session tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected:
- the new session tests fail because the title bar event is not wired, the modal does not exist in session state, and `.heproj` is not mutated from editor UI yet

- [ ] **Step 3: Implement build-settings session flow**

Update `engine/helengine.editor/EditorSession.cs` to:
- create and own `BuildSettingsDialog`
- resolve available platforms using the new shared platform-discovery library and the current project’s `RequiredEngineVersion`
- show the modal when `titleBar.BuildSettingsRequested` fires
- read `project.heproj` via `ProjectFileReader`
- write the updated document via `ProjectFileWriter`
- refresh in-memory `ProjectSupportedPlatforms`
- keep or replace the active local platform through `EditorProjectLocalSettingsService`
- update any session-owned consumers that rely on `CurrentProjectPlatform`, including asset import settings

Update `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs` only as needed to support an explicit “replace unsupported active platform with first supported platform” flow without introducing best-effort behavior.

- [ ] **Step 4: Re-run the focused session tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildSettingsTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected:
- all new build-settings session tests pass
- existing local-settings tests continue to pass

- [ ] **Step 5: Commit the session integration slice**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs engine/helengine.editor/helengine.editor.csproj engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs engine/helengine.editor.tests/EditorSessionAssetImportSettingsTests.cs engine/helengine.editor.tests/helengine.editor.tests.csproj
git commit -m "Wire build settings into editor session"
```

## Task 5: Final verification and cleanup

**Files:**
- Verify all files touched in Tasks 1-4

- [ ] **Step 1: Run the focused platform/build-settings suites**

Run:

```bash
rtk dotnet test engine/helengine.platforms.tests/helengine.platforms.tests.csproj -v minimal
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected:
- all shared-platform and editor build-settings tests pass

- [ ] **Step 2: Run final editor build verification**

Run:

```bash
rtk dotnet build engine/helengine.platforms/helengine.platforms.csproj -v minimal
rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal
```

Expected:
- both builds succeed with `0 errors`

- [ ] **Step 3: Record completion and integration outcome**

Summarize:
- the new shared platform-discovery boundary
- the new `Build` menu and `Build Settings...` modal
- the `.heproj` `supportedPlatforms` mutation behavior
- the active-platform fallback behavior in `settings/project.json`

If the branch is ready for integration, use superpowers:finishing-a-development-branch before merging or handing off.

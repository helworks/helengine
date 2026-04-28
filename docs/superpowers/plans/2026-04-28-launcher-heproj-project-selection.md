# Launcher `.heproj` Project Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor the launcher so it treats the `.heproj` file as the canonical project identity for browsing, recents, dedupe, validation, and display.

**Architecture:** Move project-file loading into a dedicated launcher service, keep recent-project persistence file-based, and simplify `LauncherShell` so it orchestrates page flow and status while delegating file picking and `.heproj` interpretation to focused services. This keeps the launcher aligned with the actual product model and preserves the existing header refactor direction.

**Tech Stack:** C#, Avalonia 11, xUnit, `Avalonia.Headless`, launcher desktop app, existing launcher services and models

---

## File Map

### Existing files to modify

- `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
  - Replace folder-based project browse/load logic with `.heproj` file selection and service-based project loading.
- `helengine.ui/helengine.launcher/Services/RecentProjectsService.cs`
  - Validate recents with `File.Exists(...)` and keep dedupe keyed on the full `.heproj` path.
- `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
  - Verify the recent-project card continues to show the full stored path, which will now be the `.heproj` path.
- `helengine.ui/helengine.sln`
  - Ensure any new launcher test files and launcher service files are included where needed.

### New files to create

- `helengine.ui/helengine.launcher/Services/ILauncherStoragePicker.cs`
  - Interface for launcher-specific file and folder picking so `LauncherShell` stops depending directly on Avalonia storage APIs.
- `helengine.ui/helengine.launcher/Services/LauncherStoragePicker.cs`
  - Production Avalonia implementation for selecting `.heproj` files and installation folders.
- `helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs`
  - Service that validates a selected `.heproj` file and builds a `RecentProject` model from project metadata plus fallbacks.
- `helengine.ui/helengine.launcher.tests/RecentProjectsServiceTests.cs`
  - Focused tests for file-based recent-project validation and dedupe behavior.
- `helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs`
  - Focused tests for `.heproj` validation, metadata loading, and fallback naming/path behavior.
- `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`
  - Headless launcher tests for browse-project file picking, invalid-file error reporting, and create-flow canonical recents.

## Implementation Notes

- Treat the full `.heproj` path as the only canonical project identity in launcher code.
- Do not preserve folder-based recent entries with implicit migration logic in this phase.
- Keep engine-install selection folder-based; only project selection changes to file-based.
- Keep UI rendering logic in views and project interpretation logic in services.
- Follow TDD: add failing tests before each implementation slice.
- Reuse the existing launcher test harness already added in `helengine.ui/helengine.launcher.tests`.

### Task 1: Convert Recent Projects Persistence To File Semantics

**Files:**
- Modify: `helengine.ui/helengine.launcher/Services/RecentProjectsService.cs`
- Create: `helengine.ui/helengine.launcher.tests/RecentProjectsServiceTests.cs`

- [ ] **Step 1: Write failing tests for file-based recent-project validity**

Create `RecentProjectsServiceTests.cs` with coverage for:
- loading only entries whose `Path` points to an existing file,
- dropping old folder-based entries because `File.Exists(...)` is false for directories,
- deduping with case-insensitive full file paths,
- keeping the newest updated entry when the same `.heproj` path is added again.

Suggested test cases:
- `LoadAsync_WhenRecentPathIsDirectory_OmitsTheEntry`
- `LoadAsync_WhenRecentProjectFileExists_ReturnsTheEntry`
- `AddOrUpdateAsync_WhenFilePathMatchesExisting_ReplacesExistingEntry`

- [ ] **Step 2: Run the focused recents tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~RecentProjectsServiceTests" -v minimal
```

Expected: FAIL because the service still uses `Directory.Exists(...)`.

- [ ] **Step 3: Update `RecentProjectsService` to use `.heproj` file semantics**

Change `RecentProjectsService.cs` so that:
- `LoadAsync()` filters with `File.Exists(project.Path)`,
- `AddOrUpdateAsync(...)` continues deduping by `Path` but now treats that `Path` as the full `.heproj` file,
- XML comments explain that the service persists launcher recents by project file path.

- [ ] **Step 4: Re-run the focused recents tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~RecentProjectsServiceTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the recents service change**

```bash
rtk git add helengine.ui/helengine.launcher/Services/RecentProjectsService.cs helengine.ui/helengine.launcher.tests/RecentProjectsServiceTests.cs
rtk git commit -m "Store launcher recents by project file path"
```

### Task 2: Extract `.heproj` Interpretation Into A Dedicated Loader Service

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs`
- Create: `helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs`

- [ ] **Step 1: Write failing tests for `.heproj` loading and fallback behavior**

Create `ProjectFileLoaderTests.cs` with coverage for:
- rejecting missing files,
- rejecting files without the `.heproj` extension,
- loading the selected `.heproj` path into `RecentProject.Path`,
- preferring metadata-derived name/description/version when present,
- falling back to the project file stem or parent directory name when metadata is incomplete.

Suggested test cases:
- `LoadAsync_WhenFileDoesNotExist_ThrowsInvalidOperationException`
- `LoadAsync_WhenExtensionIsNotHeproj_ThrowsInvalidOperationException`
- `LoadAsync_WhenMetadataIsMissing_UsesProjectFileStemForName`
- `LoadAsync_WhenMetadataExists_PopulatesRecentProjectFromProjectFile`

- [ ] **Step 2: Run the focused loader tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectFileLoaderTests" -v minimal
```

Expected: FAIL because the loader service does not exist yet.

- [ ] **Step 3: Implement `ProjectFileLoader`**

Create `ProjectFileLoader.cs` that:
- accepts a `.heproj` file path,
- validates existence and extension,
- reads project metadata from the selected `.heproj` first,
- optionally consults sibling settings data only for additional metadata,
- returns a fully populated `RecentProject` whose `Path` is the selected `.heproj` file.

Implementation constraints:
- do not let folder identity leak back into the returned model,
- keep helper logic on the service type rather than local functions,
- add XML comments to the service and its public methods.

- [ ] **Step 4: Re-run the focused loader tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~ProjectFileLoaderTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the loader service**

```bash
rtk git add helengine.ui/helengine.launcher/Services/ProjectFileLoader.cs helengine.ui/helengine.launcher.tests/ProjectFileLoaderTests.cs
rtk git commit -m "Add launcher project file loader"
```

### Task 3: Add A Launcher Storage Picker Abstraction And Refactor Browse Flow

**Files:**
- Create: `helengine.ui/helengine.launcher/Services/ILauncherStoragePicker.cs`
- Create: `helengine.ui/helengine.launcher/Services/LauncherStoragePicker.cs`
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Create: `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`

- [ ] **Step 1: Write failing shell tests for `.heproj` browse behavior**

Create `LauncherShellProjectSelectionTests.cs` with coverage for:
- `Browse project` using file selection rather than folder selection,
- invalid project-file selections surfacing `Selected file is not a helengine project.`,
- successful project-file selection adding the loaded `RecentProject` to recents,
- browse cancellation leaving state unchanged.

Suggested test cases:
- `BrowseExistingProjectAsync_WhenPickerReturnsInvalidFile_ShowsProjectError`
- `BrowseExistingProjectAsync_WhenPickerReturnsHeproj_AddsRecentProjectWithFilePath`
- `BrowseExistingProjectAsync_WhenPickerReturnsNull_DoesNotMutateRecentProjects`

- [ ] **Step 2: Run the focused shell-selection tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherShellProjectSelectionTests" -v minimal
```

Expected: FAIL because `LauncherShell` still uses a folder picker and inline folder-based loading.

- [ ] **Step 3: Add the picker abstraction**

Create:
- `ILauncherStoragePicker.cs` with methods for picking a `.heproj` file and an install folder,
- `LauncherStoragePicker.cs` with the Avalonia storage-provider implementation.

Behavior requirements:
- `.heproj` selection uses a file picker restricted to `*.heproj`,
- engine install continues to use folder selection,
- platform capability errors remain visible through the shell status text.

- [ ] **Step 4: Refactor `LauncherShell` to use picker and loader services**

Update `LauncherShell.cs` to:
- inject or construct `ILauncherStoragePicker` and `ProjectFileLoader`,
- replace `BrowseExistingProjectAsync()` folder logic with `.heproj` file picking,
- remove `BuildProjectFromFolderAsync(...)` in favor of loader service calls,
- preserve existing footer/header status behavior,
- keep browse-project actions in the header refactor intact.

- [ ] **Step 5: Re-run the focused shell-selection tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~LauncherShellProjectSelectionTests" -v minimal
```

Expected: PASS.

- [ ] **Step 6: Commit the browse-flow refactor**

```bash
rtk git add helengine.ui/helengine.launcher/Services/ILauncherStoragePicker.cs helengine.ui/helengine.launcher/Services/LauncherStoragePicker.cs helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs
rtk git commit -m "Browse launcher projects by heproj file"
```

### Task 4: Canonicalize Newly Created Projects And Verify UI Display

**Files:**
- Modify: `helengine.ui/helengine.launcher/Views/LauncherShell.cs`
- Modify: `helengine.ui/helengine.launcher/Views/Pages/HomeView.cs`
- Modify: `helengine.ui/helengine.sln` (if test project file inclusion changes)
- Extend: `helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs`
- Extend: `helengine.ui/helengine.launcher.tests/LauncherPageBodyTests.cs`

- [ ] **Step 1: Add failing tests for create-flow canonical recents and path display**

Extend existing launcher tests so they verify:
- newly created projects are added to recents using `Path.Combine(projectDirectory, "project.heproj")`,
- the home-page recent-project card shows the full `.heproj` path line,
- create and browse now produce the same path format in recents.

Suggested test cases:
- `CreateProjectAsync_WhenProjectIsCreated_AddsRecentProjectUsingHeprojFilePath`
- `HomeView_WhenRecentProjectIsRendered_ShowsFullHeprojPath`

- [ ] **Step 2: Run the focused create/display tests and verify they fail**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~CreateProjectAsync_WhenProjectIsCreated_AddsRecentProjectUsingHeprojFilePath|FullyQualifiedName~HomeView_WhenRecentProjectIsRendered_ShowsFullHeprojPath" -v minimal
```

Expected: FAIL because the create flow still stores the project directory.

- [ ] **Step 3: Update the create flow and verify path rendering**

Change `LauncherShell.cs` so `BuildRecentProjectFromCreate(...)` stores the `.heproj` file path instead of the directory path. Keep `HomeView.cs` rendering the stored `Path` value without shortening it.

- [ ] **Step 4: Re-run the focused create/display tests**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj --filter "FullyQualifiedName~CreateProjectAsync_WhenProjectIsCreated_AddsRecentProjectUsingHeprojFilePath|FullyQualifiedName~HomeView_WhenRecentProjectIsRendered_ShowsFullHeprojPath" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the create-path canonicalization**

```bash
rtk git add helengine.ui/helengine.launcher/Views/LauncherShell.cs helengine.ui/helengine.launcher/Views/Pages/HomeView.cs helengine.ui/helengine.launcher.tests/LauncherShellProjectSelectionTests.cs helengine.ui/helengine.launcher.tests/LauncherPageBodyTests.cs
rtk git commit -m "Use heproj paths for created launcher projects"
```

### Task 5: Final Verification

**Files:**
- Verify all launcher changes across the modified launcher app and test files

- [ ] **Step 1: Run the full launcher test project**

Run:

```bash
rtk dotnet test helengine.ui/helengine.launcher.tests/helengine.launcher.tests.csproj -v minimal
```

Expected: PASS with all launcher headless tests green.

- [ ] **Step 2: Run a launcher build**

Run:

```bash
rtk dotnet build helengine.ui/helengine.launcher/helengine.launcher.csproj -v minimal
```

Expected: PASS with `0 errors`.

- [ ] **Step 3: Review the worktree**

Run:

```bash
rtk git status --short
```

Expected: only the intended launcher changes and the existing unrelated `.codex/` directory.

- [ ] **Step 4: Commit the final verification or integration cleanups if needed**

```bash
rtk git add helengine.ui/helengine.launcher helengine.ui/helengine.launcher.tests helengine.ui/helengine.sln
rtk git commit -m "Align launcher project selection with heproj files"
```

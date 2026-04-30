# Build Queue Platform Map Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a second `Build...` command under the editor's existing `Build` menu that opens a local build-planning modal with per-platform map selection, per-platform output folders, a persisted build queue, and sequential queue execution for pending items.

**Architecture:** Keep `Build Platforms...` as the shared `.heproj` platform-enablement workflow, and add a separate local build workflow built from a new `BuildDialog`, a new `EditorBuildConfigService`, a queue runner service, and a migration of editor-local platform state from `settings/project.json` to `user_settings/project.json`.

**Tech Stack:** C#, xUnit, shared `helengine.projectfile` project metadata, existing editor modal/title-bar UI components, JSON-backed local settings/config documents, existing `EditorSession` orchestration patterns.

---

## File Map

### Menu and session wiring
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/helengine.editor.csproj`

### Local project settings migration
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Create: `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs` or extend existing file

### Build configuration persistence
- Create: `engine/helengine.editor/managers/build/EditorBuildConfigDocument.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildPlatformConfiguration.cs`
- Create: `engine/helengine.editor/managers/build/EditorQueuedBuildItem.cs`
- Create: `engine/helengine.editor/managers/build/EditorQueuedBuildStatus.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildConfigService.cs`
- Create: `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`

### Build dialog UI
- Create: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Create: `engine/helengine.editor/model/BuildDialogSelection.cs`
- Create: `engine/helengine.editor/model/BuildDialogPlatformState.cs`
- Create: `engine/helengine.editor/tests` (none; test files below)
- Create: `engine/helengine.editor.tests/BuildDialogTests.cs`

### Queue execution
- Create: `engine/helengine.editor/managers/build/IEditorBuildRunner.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildQueueService.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildRunResult.cs`
- Create: `engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs`

### Session integration coverage
- Modify: `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs`
- Create: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

## Task 1: Expand the Build menu for the new workflow

**Files:**
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs`

- [ ] **Step 1: Write the failing title-bar tests**

Extend `engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs` to prove:
- the `Build` menu now contains both `Build Platforms...` and `Build...`
- selecting `Build...` raises a new `BuildRequested` event
- the existing `Build Platforms...` event continues to work unchanged

- [ ] **Step 2: Run the focused title-bar tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarBuildMenuTests" -v minimal
```

Expected:
- the new `Build...` menu assertions fail because the title bar currently exposes only `Build Platforms...`

- [ ] **Step 3: Implement the second Build menu item**

Update `engine/helengine.editor/components/ui/EditorTitleBar.cs` to:
- add `Build...` as a second item under the existing `Build` menu
- expose a `BuildRequested` event
- keep the title bar presentation-only with no queue/config logic inside it

- [ ] **Step 4: Re-run the focused title-bar tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarBuildMenuTests" -v minimal
```

Expected:
- all build-menu tests pass

- [ ] **Step 5: Commit the menu slice**

```bash
git add engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests/EditorTitleBarBuildMenuTests.cs
git commit -m "Add build menu queue entry"
```

## Task 2: Migrate project-local platform settings to `user_settings`

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
- Modify: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Modify: `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs`

- [ ] **Step 1: Write the failing local-settings migration tests**

Extend `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs` with coverage for:
- loading active platform from `user_settings/project.json`
- migrating silently from `settings/project.json` when the new file does not exist
- writing only to `user_settings/project.json` after migration
- ignoring the old `settings/project.json` once the new path exists

- [ ] **Step 2: Run the focused migration tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected:
- the new migration/path tests fail because the service still targets `settings/project.json`

- [ ] **Step 3: Implement the path move and migration**

Update the local-settings document comments and service so that:
- active platform lives in `user_settings/project.json`
- the service migrates from `settings/project.json` only when needed
- the old location is no longer used after migration completes

Keep this service limited to active-platform state only.

- [ ] **Step 4: Re-run the focused migration tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected:
- all local-settings migration tests pass

- [ ] **Step 5: Commit the migration slice**

```bash
git add engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs
git commit -m "Move editor local settings to user settings"
```

## Task 3: Add local build-config persistence

**Files:**
- Create: `engine/helengine.editor/managers/build/EditorBuildConfigDocument.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildPlatformConfiguration.cs`
- Create: `engine/helengine.editor/managers/build/EditorQueuedBuildItem.cs`
- Create: `engine/helengine.editor/managers/build/EditorQueuedBuildStatus.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildConfigService.cs`
- Create: `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`

- [ ] **Step 1: Write the failing build-config persistence tests**

Create `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs` with focused cases for:
- persisting per-platform selected maps
- persisting per-platform default output folders
- persisting queued items and statuses in order
- regenerating a clean config when `user_settings/build_config.json` is missing or malformed

- [ ] **Step 2: Run the focused build-config tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal
```

Expected:
- the new tests fail because the build-config types and service do not exist yet

- [ ] **Step 3: Implement the build-config document and service**

Add the new build-config model/service types so that `user_settings/build_config.json` stores:
- per-platform selected map ids
- per-platform default output folder
- ordered queued build items
- queued item statuses and optional diagnostic messages

The service should:
- load/create the file
- rewrite malformed files to a clean default document
- keep queue ordering stable

- [ ] **Step 4: Re-run the focused build-config tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildConfigServiceTests" -v minimal
```

Expected:
- all build-config persistence tests pass

- [ ] **Step 5: Commit the build-config slice**

```bash
git add engine/helengine.editor/managers/build engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs
git commit -m "Add editor build queue config persistence"
```

## Task 4: Add the Build dialog UI

**Files:**
- Create: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Create: `engine/helengine.editor/model/BuildDialogSelection.cs`
- Create: `engine/helengine.editor/model/BuildDialogPlatformState.cs`
- Create: `engine/helengine.editor.tests/BuildDialogTests.cs`

- [ ] **Step 1: Write the failing build-dialog tests**

Create `engine/helengine.editor.tests/BuildDialogTests.cs` with focused coverage that proves:
- tabs are rendered from enabled project platforms
- first open defaults the current scene checked when a platform has no saved selection
- saved selections win on subsequent opens
- `Copy Map List From...` copies only selected maps
- `Add to Build` queues only the currently visible platform tab
- queued items render platform, map count, folder, and status
- validation rejects queueing when no map is selected or output folder is blank

- [ ] **Step 2: Run the focused build-dialog tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests" -v minimal
```

Expected:
- the new dialog tests fail because the modal and dialog models do not exist yet

- [ ] **Step 3: Implement the build dialog**

Add `BuildDialog` and its UI models so the modal:
- shows one tab per enabled platform
- shows a map checklist for the selected tab
- supports `Copy Map List From...`
- shows the per-platform output-folder field near the bottom
- shows `Add to Build` on the left and the persisted queue on the right
- keeps UI logic inside the dialog and leaves persistence/execution to services/session code

Use the existing editor modal pattern from `BuildSettingsDialog`, `ReparentEntityDialog`, and `UnsavedChangesDialog`.

- [ ] **Step 4: Re-run the focused build-dialog tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BuildDialogTests" -v minimal
```

Expected:
- all build-dialog tests pass

- [ ] **Step 5: Commit the build-dialog slice**

```bash
git add engine/helengine.editor/components/ui/BuildDialog.cs engine/helengine.editor/model/BuildDialogSelection.cs engine/helengine.editor/model/BuildDialogPlatformState.cs engine/helengine.editor.tests/BuildDialogTests.cs
git commit -m "Add editor build dialog"
```

## Task 5: Add queue execution services and placeholder runner boundary

**Files:**
- Create: `engine/helengine.editor/managers/build/IEditorBuildRunner.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildRunResult.cs`
- Create: `engine/helengine.editor/managers/build/EditorBuildQueueService.cs`
- Create: `engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs`

- [ ] **Step 1: Write the failing queue-service tests**

Create `engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs` with focused cases for:
- running only `Pending` items
- running pending items sequentially in queue order
- marking an item `Failed` and stopping later pending items when the runner reports failure
- marking an item `Failed` when its platform is no longer enabled in `.heproj`
- persisting queue status transitions through `EditorBuildConfigService`

- [ ] **Step 2: Run the focused queue-service tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueServiceTests" -v minimal
```

Expected:
- the new queue-service tests fail because the queue service and runner boundary do not exist yet

- [ ] **Step 3: Implement the queue service**

Add the queue execution layer so it:
- scans queue items in order
- runs only `Pending` items
- updates status transitions (`Pending` -> `Running` -> `Done` / `Failed`)
- persists each transition immediately
- stops at the first failure

Use a placeholder `IEditorBuildRunner` implementation boundary for now. The real platform-builder integration can plug into that later.

- [ ] **Step 4: Re-run the focused queue-service tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueServiceTests" -v minimal
```

Expected:
- all queue-service tests pass

- [ ] **Step 5: Commit the queue-service slice**

```bash
git add engine/helengine.editor/managers/build engine/helengine.editor.tests/EditorBuildQueueServiceTests.cs
git commit -m "Add editor build queue service"
```

## Task 6: Wire `EditorSession` to the new build workflow

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/helengine.editor.csproj`
- Create: `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] **Step 1: Write the failing session integration tests**

Create `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs` with focused coverage for:
- opening `BuildDialog` from the new `Build...` title-bar action
- seeding first-open map selection from the currently open scene
- loading existing per-platform map selections and output folders from `build_config.json`
- appending one queued item from the currently visible platform tab
- reloading the persisted queue into the dialog
- running pending items when `Build Queue` is clicked and updating persisted statuses

- [ ] **Step 2: Run the focused session tests to confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests" -v minimal
```

Expected:
- the new session tests fail because `EditorSession` does not yet know about the build dialog or queue service

- [ ] **Step 3: Implement session orchestration**

Update `engine/helengine.editor/EditorSession.cs` to:
- subscribe to the new `BuildRequested` title-bar event
- gather enabled project platforms from `.heproj`
- gather available scenes/maps from the current project
- seed first-open local platform state from the current scene when needed
- load/save build config through `EditorBuildConfigService`
- create queued items from the currently visible platform tab
- run pending queue items through `EditorBuildQueueService`
- keep `Build Platforms...` and active-platform behavior intact

Keep orchestration in `EditorSession`, not in the dialog.

- [ ] **Step 4: Re-run the focused session tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~EditorBuildQueueServiceTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests|FullyQualifiedName~EditorTitleBarBuildMenuTests" -v minimal
```

Expected:
- all new build-workflow tests pass together

- [ ] **Step 5: Commit the session-integration slice**

```bash
git add engine/helengine.editor/EditorSession.cs engine/helengine.editor/helengine.editor.csproj engine/helengine.editor.tests
git commit -m "Wire editor build queue workflow"
```

## Task 7: Final verification

**Files:**
- Review all files touched in Tasks 1-6

- [ ] **Step 1: Run the focused build-workflow regression slice**

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorTitleBarBuildMenuTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests|FullyQualifiedName~EditorBuildConfigServiceTests|FullyQualifiedName~BuildDialogTests|FullyQualifiedName~EditorBuildQueueServiceTests|FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~BuildSettingsDialogTests|FullyQualifiedName~EditorSessionBuildSettingsTests" -v minimal
```

Expected:
- the entire build-platform/build-queue slice passes cleanly

- [ ] **Step 2: Run the editor build**

```bash
rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal
```

Expected:
- `0 errors`

- [ ] **Step 3: Commit the final verification pass if needed**

```bash
git status
```

Expected:
- no unexpected modified files remain after the final planned commits

## Notes For Implementers

- Keep `Build Platforms...` and `Build...` as separate workflows; do not fold queueing into `BuildSettingsDialog`.
- `project.heproj` remains the owner of enabled platforms only.
- `user_settings/project.json` owns active platform only.
- `user_settings/build_config.json` owns local map selections, output folders, and queue state.
- Queue items must be fully copied snapshots of the currently visible platform tab when added; later tab edits must not mutate already queued items.
- Fail fast during queue execution and persist failure diagnostics instead of silently skipping invalid work.

# Build Queue Platform Map Selection Design

## Goal

Add a second `Build...` command under the editor's existing `Build` menu.

`Build Platforms...` continues to control which platforms are enabled for the project. The new `Build...` workflow handles local build preparation:

- choosing which maps should be included for each enabled platform
- remembering a per-platform output folder
- queueing build jobs one at a time from the currently visible platform tab
- persisting the queue and each job status across editor restarts
- running all pending queue items sequentially when the user clicks `Build Queue`

This is a local user workflow, not shared project contract data.

## Current State

- `EditorTitleBar` already has a top-level `Build` menu.
- `Build Platforms...` already exists and edits `.heproj` `supportedPlatforms` through `BuildSettingsDialog`.
- Project-local active platform state is currently stored in `settings/project.json` through `EditorProjectLocalSettingsService`.
- There is no persisted build queue, no per-platform map-selection config, and no editor-side build modal.

## Recommended Approach

Keep `Build Platforms...` and `Build...` as separate workflows.

- `Build Platforms...`
  - shared project configuration
  - edits `project.heproj`
  - controls which platforms are enabled for the project
- `Build...`
  - local user workflow
  - edits local build configuration only
  - owns per-platform selected maps, per-platform output folders, and the persisted queue

This keeps team-shared project settings separate from per-user build preparation and queue state.

## Storage Model

The project now needs three files with different ownership rules.

### 1. `project.heproj`

Shared team-wide project contract.

This continues to own:

- `supportedPlatforms`
- all existing canonical shared project metadata

`Build...` must not mutate this file.

### 2. `user_settings/project.json`

Local per-user project settings.

This file replaces the current `settings/project.json` location for editor-local platform state.

It owns:

- active editor platform

Migration rule:

- if `settings/project.json` exists and `user_settings/project.json` does not, migrate the active platform silently
- after migration, the editor should read and write only `user_settings/project.json`

### 3. `user_settings/build_config.json`

Local per-user build configuration and queue state.

It owns:

- per-platform selected map ids
- per-platform default output folder
- queued build items
- queued build statuses

Suggested shape:

- `platforms`
  - keyed by platform id
  - stores selected maps and default output folder
- `queue`
  - ordered list of queued build items

## Menu Structure

The `Build` menu should contain two items:

- `Build Platforms...`
- `Build...`

`EditorTitleBar` should remain presentation-only and raise separate events for each command.

Suggested events:

- `BuildSettingsRequested`
- `BuildRequested`

## Build Modal

Add a new build modal, separate from `BuildSettingsDialog`.

### Layout

Left side:

- one tab per enabled platform from `.heproj`
- map checklist for the selected platform
- `Copy Map List From...` action
- output-folder field near the bottom
- `Add to Build` button at the bottom

Right side:

- queued build list
- each item shows:
  - platform
  - selected map count
  - output folder
  - status
- `Build Queue` button at the bottom

### Behavior

- platform tabs come only from `.heproj` `supportedPlatforms`
- map selections are local and come from `user_settings/build_config.json`
- output folder is local and comes from `user_settings/build_config.json`
- `Add to Build` queues only the currently visible platform tab
- `Build Queue` runs all `Pending` items sequentially, in queue order

## Map Selection Behavior

The modal needs one local map selection per enabled platform.

Defaulting behavior:

- if a platform has no saved map selection yet, default to the currently open scene checked
- once a local selection exists for that platform, the saved selection wins on subsequent opens

The builder should not discover scenes itself. The editor owns the list of scenes/maps the user can choose from.

## Output Folder Behavior

Each platform remembers one default output folder in local build config.

When the user clicks `Add to Build`, the queued item copies the currently visible platform tab's:

- platform id
- selected maps
- output folder

The queue item should not remain linked live to later tab edits after it has been queued.

## Copy Map List Behavior

The user can copy the selected map list from another enabled platform into the currently visible platform tab.

This action should copy only the selected-map list.

It should not copy:

- output folder
- queued items
- statuses

## Queue Model

Queued builds are persisted in `user_settings/build_config.json`. They are not session-only.

Each queue item should store enough data to run independently later:

- queue item id
- platform id
- selected map ids
- output folder
- status
- optional diagnostic/status message
- created timestamp

Suggested initial statuses:

- `Pending`
- `Running`
- `Done`
- `Failed`

### Queue Execution

`Build Queue` should:

- scan queue items in order
- run only `Pending` items
- process them sequentially
- update persisted status after each state transition

The first implementation does not need to produce final platform files yet. It should stop at the editor-side build orchestration boundary and integrate with a placeholder build service that can later call the real platform builder pipeline.

## Services And Boundaries

### `EditorProjectLocalSettingsService`

Responsibilities:

- migrate from `settings/project.json` to `user_settings/project.json`
- load/save active platform only

It should not own build queue or map-selection state.

### `EditorBuildConfigService`

New service.

Responsibilities:

- load/save `user_settings/build_config.json`
- persist per-platform selected maps
- persist per-platform default output folders
- persist queued build items and statuses

### `BuildDialog`

New modal UI.

Responsibilities:

- render platform tabs
- render map checklist
- render copy-map-list UI
- render output folder field
- render queue list and actions
- raise UI events only

It should not own queue execution or file persistence logic directly.

### `EditorBuildQueueService`

New service.

Responsibilities:

- run pending queue items sequentially
- update statuses
- persist queue transitions
- stop on failure and mark the failed item

### `EditorSession`

Responsibilities:

- open the dialog from the `Build...` menu action
- gather available maps/scenes for the project
- pass current scene information for first-open defaulting
- wire dialog events to persistence and queue services
- keep build-platform and active-platform behavior consistent with the rest of the editor

## Validation Rules

### Add To Build

Reject queueing when:

- no map is selected
- output folder is blank

If validation fails, keep the modal open and show a clear local error message.

### Build Queue

- skip non-pending items
- run pending items in queue order
- if a queued item targets a platform no longer enabled in `.heproj`, mark it `Failed` with a diagnostic message instead of silently dropping it

## Error Handling

### Local Config Load Failure

If `user_settings/build_config.json` is missing or malformed:

- regenerate a clean local config
- seed per-platform selection from the current scene where applicable
- do not mutate `.heproj`

### Queue Execution Failure

If a queued item fails:

- mark that item `Failed`
- persist the failure status and diagnostic message
- stop further queue execution for the current run

### Platform Removal After Queueing

If a queued item references a platform that is no longer enabled in `.heproj`:

- keep the item in the queue for transparency
- mark it `Failed` when execution reaches it
- include a diagnostic explaining that the platform is no longer enabled for the project

## Testing

Add focused coverage for:

### Title Bar

- `Build` menu contains both `Build Platforms...` and `Build...`
- `Build...` raises the correct event

### Local Settings Migration

- active platform migrates from `settings/project.json` to `user_settings/project.json`
- once migrated, only `user_settings/project.json` is used

### Build Config Persistence

- per-platform selected maps persist
- per-platform output folders persist
- queue items and statuses persist

### Build Dialog

- tabs come from enabled project platforms
- first open defaults the current scene checked for platforms without saved selection
- copy-map-list copies map selection from another platform
- `Add to Build` queues the currently visible tab only
- queued items appear with platform, map count, folder, and status

### Queue Execution

- `Build Queue` runs only `Pending` items
- pending items run sequentially in queue order
- failure marks the item `Failed` and stops later pending items from running

## Why This Approach

This keeps the boundaries clean:

- `.heproj` remains shared project contract
- `user_settings/project.json` remains local editor-platform state
- `user_settings/build_config.json` owns local build preparation and queue data
- `Build Platforms...` remains a platform-enablement workflow
- `Build...` becomes a local build-planning and queueing workflow

That separation gives the editor a DaVinci-style build queue without confusing shared project configuration with local build jobs.

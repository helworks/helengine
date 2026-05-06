# Platforms And Build Settings Separation Design

## Problem

The older `build-settings-platform-selection` branch mixed three concerns that should not live in the same workflow:

- choosing which platforms the project supports
- choosing the current active platform for one user/workspace
- editing builder-owned per-platform build settings

That branch also assumed the wrong storage and the wrong responsibility boundaries:

- it treated `Build Settings...` as the owner of project `supportedPlatforms`
- it treated project-supported platforms as canonical in `.heproj`
- it introduced new platform-discovery work even though available-platform resolution already exists through installed builder/player settings
- it silently repaired the active platform when it became invalid

The intended system is different:

- project-supported platforms are project data
- active platform is user-local state
- `Platforms...` owns project platform enablement and active-platform selection
- `Build Settings...` only edits settings for platforms that are both enabled for the project and available on the current machine

## Goals

- Move project-supported platform ownership to one dedicated project settings file.
- Keep active-platform selection user-local and separate from project-shared settings.
- Make `Platforms...` the only workflow that can enable or disable project platforms.
- Make `Platforms...` also own active-platform selection so invalid active platforms are resolved explicitly there.
- Keep `Build Settings...` focused on builder-owned configuration only.
- Show `Build Settings...` tabs only for platforms that are both enabled for the project and currently available from installed builder/player metadata.
- Preserve unavailable platform configuration without rewriting or deleting it.
- Reduce Git conflicts by storing per-platform build settings in separate flat files.
- Leave room for a future cross-platform section in `Build Settings...` even if there is little or no shared content today.

## Non-Goals

- No support for editing project-supported platforms from `Build Settings...`.
- No disabled or placeholder tabs for unavailable platforms in `Build Settings...`.
- No automatic active-platform fallback outside the `Platforms...` flow.
- No new platform-discovery subsystem in this slice.
- No forced migration of every existing build/profile concept into one giant combined document.

## Ownership Model

### Platforms...

`Platforms...` owns project platform topology:

- which platforms are enabled for the project
- which enabled platform is the current active platform for the current user

It is the only workflow that should mutate project supported-platform state.

It must also prevent invalid active-platform state from being saved. If the user disables the current active platform, the same modal must require selecting a replacement active platform before save can complete.

### Build Settings...

`Build Settings...` owns builder-facing configuration only.

It should already know:

- which platforms are enabled for the project
- which platforms are available on the current machine from installed builder/player settings

It should not expose unsupported or unavailable platforms as editable rows or tabs. The modal should derive its visible platform set from the intersection of project-enabled and machine-available platforms.

## Persistence Model

### Project-Shared Supported Platforms

Project-supported platforms should live in:

- `settings/platforms.json`

This file is project-scoped and should be committed as part of the project.

It should contain the canonical enabled platform list for the project. This replaces the older assumption that `supportedPlatforms` should be edited through `.heproj` or through `Build Settings...`.

### User-Local Active Platform

The active platform remains user-local state.

It should not be stored in `settings/platforms.json`. It remains separate from project-shared platform enablement so different users can work on the same project with different active platforms without producing team-wide churn.

### Per-Platform Build Settings

Per-platform builder-owned settings should be stored one file per platform in a flat shape under `settings`, for example:

- `settings/platform.windows.json`
- `settings/platform.ps2.json`

Each file should contain all builder-owned settings for that platform for now.

This format is preferred because:

- it preserves unavailable platform configuration without touching it
- it reduces Git conflicts between team members working on different platforms
- it keeps builder-owned configuration isolated to the platform that owns it

### Cross-Platform Build Settings

`Build Settings...` may keep room for a future cross-platform section.

There may be little or no shared content today, but the design should not prevent adding it later. Cross-platform values should be kept separate from per-platform builder files when they appear.

## Available Platform Resolution

Available platforms already come from editor-known installed player/builder availability.

This slice should reuse that availability source instead of introducing a new shared discovery layer just for `Build Settings...`.

The important distinction is:

- `supportedPlatforms`: project wants these platforms
- `availablePlatforms`: this editor install can actually configure/build these platforms right now

Those are different sets and should stay separate.

## Platforms... UI Behavior

`Platforms...` should include:

- a checklist of project-supported platforms
- an active-platform dropdown

Rules:

- the dropdown only offers enabled platforms
- save must be blocked if no platforms are enabled
- save must be blocked if the active platform is not one of the enabled platforms
- if the current active platform is disabled during the edit, the user must choose a replacement before saving

This makes `Platforms...` the single place where project topology and active-platform validity are resolved.

## Build Settings... UI Behavior

`Build Settings...` should not include a supported-platform checklist.

Instead, it should:

- read enabled platforms from `settings/platforms.json`
- read available platforms from installed builder/player metadata already known to the editor
- compute the visible set as the intersection of those two lists

### Tab Visibility

Only platforms that are both enabled and available should appear as tabs.

If a platform is enabled for the project but currently unavailable on this machine:

- it should be hidden entirely
- it should not appear disabled
- its saved settings file must remain untouched

Hiding is preferred because many settings are builder-owned. Without the builder/player metadata, the editor does not know how to present or validate that platform's configuration safely.

### Tab Ordering

Visible platform tabs should be ordered alphabetically.

Alphabetical ordering is simple, stable, and avoids coupling the UI order to either project insertion order or installer discovery order.

### Cross-Platform Section

The modal may include a cross-platform section, but it does not need to invent one if no shared settings exist yet.

The key requirement is to preserve the layout and ownership boundary so shared settings can be added later without collapsing per-platform files back into one merged blob.

## Data Preservation Rules

If a platform is enabled in the project but unavailable on this machine:

- `Build Settings...` must not rewrite that platform's file
- `Build Settings...` must not delete that platform's file
- `Build Settings...` must not normalize or "repair" missing builder-owned values for that platform

This is important for distributed teams where one machine may not have another platform's builder installed.

## Error Handling

### Platforms...

- block save if the enabled platform list is empty
- block save if the chosen active platform is not enabled
- surface explicit validation in the modal instead of silently repairing state

### Build Settings...

- if no enabled-and-available platforms exist, the modal should remain usable but show an empty-state message rather than inventing placeholder tabs
- if one visible platform file cannot be read or written, surface that failure directly
- do not use failures on one unavailable platform file as a reason to mutate or drop its saved state

## Migration Direction

The existing `BuildSettingsDialog` / build-settings branch work should be treated as salvage-only.

Useful pieces may still exist:

- title-bar entry points
- modal scaffolding
- some tests

But the original workflow assumptions are wrong for the intended design. Any merge should be a forward-port toward:

- `Platforms...` as the owner of supported-platform enablement and active-platform selection
- `Build Settings...` as the owner of enabled-and-available builder configuration only
- `settings/platforms.json` as project-supported platform storage
- `settings/platform.<platform-id>.json` as per-platform build settings storage

## Testing

Add or adapt focused coverage for:

### Platforms...

- saving enabled platforms writes `settings/platforms.json`
- active-platform dropdown only allows enabled platforms
- disabling the current active platform requires selecting a replacement before save
- active-platform replacement is persisted only to user-local settings

### Build Settings...

- visible tabs are the alphabetical intersection of enabled and available platforms
- unavailable enabled platforms are hidden
- hidden unavailable platforms do not have their files rewritten
- per-platform settings save to `settings/platform.<platform-id>.json`

### Preservation

- opening `Build Settings...` on a machine missing one enabled platform does not delete or rewrite that missing platform's file
- one platform's settings change does not rewrite another platform's file

## Why This Design

This split matches the intended ownership boundaries:

- project topology is not builder configuration
- user-local active selection is not project-shared state
- unavailable builder-owned configuration should not be guessed at

It also scales better for real team use:

- fewer Git conflicts
- better preservation of platform-specific work
- clearer workflows for both project-wide platform enablement and per-platform configuration

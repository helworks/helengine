# Build Settings Platform Selection Design

## Goal

Add a new `Build` top-level menu to the editor title bar and a `Build Settings...` modal that lets the user change the project's `supportedPlatforms` in `project.heproj`.

The modal must not invent its own platform list. It should show platforms that are actually available on the current machine, while still working in both of these cases:

- the editor was installed and managed through the launcher
- the editor is running as a source/debug build without launcher coupling

## Current State

- The top-left editor menus are custom UI in `engine/helengine.editor/components/ui/EditorTitleBar.cs`, not a WinForms `MenuStrip`.
- The editor already loads project-supported platforms from `.heproj`.
- The editor already persists the current active platform in `settings/project.json` through `EditorProjectLocalSettingsService`.
- The launcher already owns install-root discovery and manifest persistence for installed engines and shared toolchains:
  - registry stores only root locators
  - manifests under those roots store installed engines, shared artifacts, and engine-platform bindings

The missing piece is a shared platform-availability source that the editor can query without referencing launcher UI code.

## Recommended Approach

Add a small shared platform-discovery layer and make the editor consume that layer when opening `Build Settings...`.

This gives the editor one clear question to ask:

> Which platforms are available for the current engine version on this machine?

That answer should come from shared persisted state when possible, and from a controlled source-build fallback when not.

## Architecture

### 1. Build Menu In Editor Title Bar

`EditorTitleBar` gains a third top-level menu button, `Build`, beside `File` and `Add`.

That menu initially contains one command:

- `Build Settings...`

`EditorTitleBar` should only raise an event such as `BuildSettingsRequested`. It should not own any build-settings logic or project-file mutation.

### 2. Build Settings Modal

Add a dedicated editor modal for build settings.

Responsibilities:

- load available platforms from a shared provider
- load the current checked state from the project's `.heproj` `supportedPlatforms`
- show one platform row per available platform
- let the user check or uncheck supported platforms
- save the updated supported platform list back to `.heproj`

UI shape:

- title: `Build Settings`
- content: list of platform rows
- each row: platform name/id on the left, checkbox on the right
- footer: `Save` and `Cancel`

The modal should follow the same editor-managed modal pattern already used by existing dialogs.

### 3. Shared Platform Discovery Layer

The editor must not reference `helengine.launcher` directly.

Instead, add a shared library or shared-domain slice that owns platform availability discovery. That shared layer should read the same persisted launcher-managed state without depending on launcher UI code.

Suggested surface:

- `IAvailablePlatformProvider`
- `AvailablePlatformDescriptor`
- `InstalledPlatformProvider`
- `DevelopmentPlatformProvider`
- `AvailablePlatformProviderResolver`

### 4. Availability Resolution Order

Available platforms should be resolved in this order:

1. development override source, if explicitly configured
2. launcher-managed registry/manifests, if present
3. built-in source-build fallback

This keeps source builds practical while still preferring real installed-toolchain data whenever available.

## Data Sources

### Launcher-Managed Install Data

Reuse the launcher's persisted model conceptually:

- registry locator values identify:
  - engine install root
  - shared toolchain root
- manifests under those roots describe:
  - installed engines
  - installed shared artifacts
  - installed engine-platform bindings

The shared platform provider should use those manifests to answer:

- which engine version is currently running
- which platform bindings exist for that engine version
- therefore which platforms are available for build selection

The editor should read this data through the new shared discovery layer, not by constructing launcher views or services.

### Source/Debug Build Fallback

Source builds need a non-launcher path.

Recommended fallback behavior:

1. if a development override root or manifest path is configured, use it
2. otherwise expose a built-in fallback list containing `windows`

That keeps local debugging unblocked and avoids requiring the launcher to be built, installed, or running.

The fallback is intentionally minimal. It is not meant to simulate the full launcher install graph.

## Project File Behavior

`Build Settings...` edits canonical project state:

- file: `project.heproj`
- field: `supportedPlatforms`

Saving must go through the shared `helengine.projectfile` library so the editor and launcher keep using the same canonical project-file contract.

The modal should preserve the rest of the project document:

- `projectFormatVersion`
- `name`
- `version`
- `requiredEngineVersion`
- timestamps
- description

Only `supportedPlatforms` should change in this feature.

## Active Platform Behavior

The editor already stores the current active platform in `settings/project.json`.

When supported platforms are changed:

- if the current active platform is still supported, keep it
- if it is no longer supported, switch it to the first supported platform and persist that change through `EditorProjectLocalSettingsService`

The editor session should refresh its in-memory supported-platform list after save so the rest of the editor sees the new project configuration immediately.

## Validation Rules

- The user must not be able to save with zero selected platforms.
- Available platforms should come only from the provider result, not from arbitrary free-form text.
- If platform discovery returns no platforms, the modal should remain open and show a clear error state instead of silently saving an empty list.
- If `.heproj` cannot be read or written, the editor should surface that failure rather than silently ignoring it.

## Error Handling

### Platform Discovery Failure

If discovery fails:

- prefer a clear modal-level error message
- do not mutate `.heproj`
- allow the user to close the modal safely

### Unsupported Current Active Platform

If the existing local active platform is no longer valid after save:

- rewrite local settings to the first supported platform
- refresh the live session value

### Missing Launcher Data

Missing registry keys or missing launcher manifests should not be treated as hard failures for source builds. They should simply cause the resolver to proceed to the next fallback source.

## Testing

Add focused coverage for:

### Shared Platform Discovery

- resolves launcher-managed platforms from persisted manifests
- uses development override when present
- falls back to `windows` when launcher state is unavailable

### Build Settings Modal

- shows one row per available platform
- initializes checkbox state from `.heproj`
- rejects save when no platforms are selected
- saves updated `supportedPlatforms` back to `.heproj`

### Editor Session Integration

- `Build SettingsRequested` opens the modal
- saving refreshes the session's supported-platform list
- removing the current active platform rewrites `settings/project.json` to a valid fallback platform

### Title Bar

- `Build` appears beside the existing top-left menus
- `Build Settings...` dispatches the correct editor event

## Why This Approach

This design keeps the boundaries clean:

- `EditorTitleBar` owns menu presentation
- the modal owns build-settings interaction
- `helengine.projectfile` owns canonical project persistence
- a shared discovery layer owns platform availability
- launcher UI stays separate from editor UI

It also solves the source-build problem directly instead of pretending every editor run comes from a launcher-managed install.

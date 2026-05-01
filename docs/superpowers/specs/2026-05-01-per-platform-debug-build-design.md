# Per-Platform Debug Build Design

## Problem

The editor can already queue and execute Windows builds per platform, but every build currently behaves as a single undifferentiated configuration. We need a persistent `Debug build` toggle that is saved per platform, restored when that platform is reopened in the Build modal, and snapshotted into each queued build item so reruns stay stable.

This setting is a build artifact choice, not a platform capability. It must remain separate from supported platforms, active platform selection, and project scene selection.

## Goals

- Persist a `Debug build` flag per platform in `user_settings/build_config.json`.
- Show and edit the flag in the Build modal for the currently active platform.
- Preserve the last chosen value when the user switches platforms and returns later.
- Snapshot the flag into queued build items so a queued build does not change if the user edits the default later.
- Pass the snapshot to the Windows executor so it selects Debug or Release for the native player build.
- Keep the existing user-chosen output directory flow intact.

## Non-Goals

- No platform-level manifest changes.
- No change to supported-platform selection behavior.
- No automatic switching of the active platform when the debug flag changes.
- No generalized multi-configuration build matrix in this slice.

## Data Model

`EditorBuildPlatformConfigDocument` gains one persisted boolean:

- `DebugBuild`: whether this platform should default to a debug player build.

`EditorBuildQueueItemDocument` also gains one persisted boolean:

- `DebugBuild`: the build mode snapshot captured when the queue item is created.

The queue item copy is the authoritative value for execution. The platform config remains the editor default for new queue items and for the Build modal.

## UI Flow

The Build modal shows the debug flag only for the active platform. The checkbox belongs with the existing platform-specific build settings rather than the global dialog chrome.

When the user changes platforms in the modal:

- the dialog saves the current platform's `DebugBuild` back into its per-platform config
- the newly selected platform loads its own saved `DebugBuild`
- the checkbox state updates immediately to match that platform

When the user clicks Add to Build:

- the dialog syncs the active platform config
- the resulting queue item includes the active platform's `DebugBuild` value

## Build Execution

The Windows build executor reads `DebugBuild` from the queue item, not from live dialog state.

Execution rules:

- `true` means the native player build runs as Debug.
- `false` means the native player build runs as Release.
- the selected output directory remains user-controlled, but the executor stages the build into a configuration-specific subdirectory so Debug and Release artifacts do not collide.

The executor should not infer mode from the presence of a PDB or from host process configuration. The queue item must carry the explicit build mode.

## Persistence and Migration

Existing build-config files that do not contain `DebugBuild` must continue to load. The service should seed the new property with a default value when the entry is missing so older projects keep working without manual migration.

The persistence contract stays backward compatible:

- old configs load
- new configs save the added field
- queue items created before the feature can still run with a default debug mode

## Error Handling

If the active platform is missing from the persisted config, the editor should continue to repair the config the same way it already does for other per-platform settings.

If a queued item is missing a valid `DebugBuild` value due to malformed persisted data, the build-config load path should normalize it to the platform default rather than failing the entire project load.

## Tests

Add or update tests to cover:

- a platform config retains its own `DebugBuild` value across save/load
- switching platforms in the Build modal restores each platform's saved value
- queue item creation snapshots the current platform's `DebugBuild`
- the Windows build executor receives the queued debug mode and maps it to Debug or Release

## Files In Scope

- `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- `engine/helengine.editor/components/ui/BuildDialog.cs`
- `engine/helengine.editor/managers/project/EditorWindowsBuildExecutor.cs`
- `engine/helengine.editor.tests/EditorBuildConfigServiceTests.cs`
- `engine/helengine.editor.tests/BuildDialogTests.cs`
- `engine/helengine.editor.tests/EditorWindowsBuildExecutorTests.cs`

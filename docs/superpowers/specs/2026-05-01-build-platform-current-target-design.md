# Build Settings Active Platform Design

**Goal:** Let the editor persist and change the current active platform for a project while still keeping the runtime host backend fixed to the platform it is running on.

**Why this exists:** `supportedPlatforms` answers "what can this project build for?" while the active platform answers "which platform settings should the editor use right now for cooking and import behavior?" Those are related but different choices, and they should be persisted separately.

## Scope

This change extends the existing Build Settings workflow in the editor. It does not redesign platform discovery, the Windows build host, or the host renderer. The editor will continue to build Windows through the current runtime backend, but asset import and cooking will use the currently selected platform's settings.

The active platform is persistent. The editor should reopen using the last saved active platform, and if that platform is no longer valid it should fall back to the first remaining supported platform and persist that fallback.

## Design

### 1. Persistent active-platform source of truth

The existing `EditorProjectLocalSettingsService` remains the persistence layer for the current platform. The service already loads and saves `settings/project.json` and already exposes `CurrentProjectPlatform` through `EditorSession`.

The Build Settings workflow will treat that value as the editor's active cook target. The project file still owns `supportedPlatforms`, but local settings own the active platform.

Rules:
- `supportedPlatforms` stays the editable project build list.
- `CurrentProjectPlatform` stays the persisted active platform.
- The active platform must always be one of the supported platforms that is also installed on the current machine.
- If the saved active platform is removed from `supportedPlatforms` or is no longer installed, the editor must fall back to the first remaining supported and installed platform and persist that fallback immediately.
- If no supported and installed platform exists, the editor keeps the last persisted value in storage but does not use it for cooking until the user selects a valid platform.

### 2. Build Settings modal behavior

The Build Platforms modal will keep the fixed three-column table already added:
- `Platform Name`
- `Status`
- `Enabled`

The new active-platform control sits above the table and uses the same available-platform list. It should only offer installed and enabled platforms as valid active choices. Missing platforms remain visible in the table, but they cannot become the active platform until they are installed.

UI rules:
- The table still edits `supportedPlatforms`.
- The active-platform control edits `CurrentProjectPlatform`.
- The dialog must show the current active platform when opened.
- Confirming the dialog must save both the active platform and the supported-platform list as one atomic user action.

### 3. Session flow and persistence

`EditorSession` becomes the orchestrator for both values.

When the dialog opens:
- `EditorSession` passes the available platforms, the current `supportedPlatforms`, and the current active platform into the dialog.

When the dialog confirms:
- `EditorSession` writes the updated `supportedPlatforms` back to `project.heproj`.
- `EditorSession` writes the active platform back to `settings/project.json`.
- `EditorSession` updates its in-memory `CurrentProjectPlatform`.
- `EditorSession` updates `AssetImportManager.CurrentPlatformId` so import and processor settings immediately use the new active platform.

When the active platform is no longer valid:
- `EditorSession` chooses the first remaining supported and installed platform.
- `EditorSession` persists that fallback to local settings.
- `EditorSession` keeps the import manager and any other platform-aware services in sync with the fallback.

### 4. Build and cooking behavior

The Windows host still builds for the runtime backend it is running on. This change does not alter the DirectX or Vulkan host selection. Instead, the active platform changes the asset settings used during cooking.

That means:
- host backend remains Windows-specific and unchanged
- imported assets can use platform-specific processor settings for the selected active platform
- build output for the selected platform may differ in texture size, compression, or other processor-controlled settings

The build pipeline should therefore read the active platform from the editor session or local settings before invoking any asset cooking or regeneration step.

## Implementation Boundaries

The main files are expected to be:
- `engine/helengine.editor/components/ui/BuildSettingsDialog.cs`
- `engine/helengine.editor/model/BuildSettingsSelection.cs`
- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- `engine/helengine.editor.tests/EditorSessionBuildSettingsTests.cs`

## Testing

Add or update tests to prove:
- the dialog opens with the current active platform selected
- the current platform persists after a confirm/save
- changing `supportedPlatforms` does not erase the active platform unless it becomes invalid
- removing the active platform causes a fallback to the first remaining supported platform
- the editor session updates `AssetImportManager.CurrentPlatformId` after the active platform changes

The tests should use the existing reflection-based editor session style already used elsewhere in the repository. They should assert behavior, not private implementation details, except where the current editor test style already needs reflection to reach the dialog/session state.

# Profiles Dialog Design

## Goal

Add one editor dialog that groups platform-scoped profile settings in a single place. The dialog must cover both asset-cooking behavior and runtime presentation defaults without splitting them into separate top-level Build menu items.

## Scope

This change adds one new Build menu action, `Profiles...`, which opens a single dialog with two sections:

- Build Profiles
- Graphics Profiles

The dialog is platform-aware. It edits settings for the currently active platform, and it must reopen on the last platform the user selected.

## Recommended Structure

### 1. One dialog, two sections

The dialog stays a single editor modal instead of becoming two separate commands.

- `Build Profiles` owns asset cooking and export behavior.
- `Graphics Profiles` owns runtime presentation defaults.

That keeps the menu surface small while still separating concerns inside the dialog.

### 2. Platform-scoped selection

The dialog operates on the active platform already persisted by the editor.

- When the user changes platform, the dialog loads that platform's profile values.
- When the dialog closes with confirmation, the values are persisted for that platform.
- The active platform remains the source of truth for which profile set is being edited.

### 3. Build Profiles content

The Build Profiles section should start with the settings that are most useful for actual export and asset cooking:

- texture resolution scaling
- model import quality or LOD policy
- audio compression or quality policy
- shader variant pruning behavior
- any per-platform asset packing overrides

These are platform-specific because Windows, PS2, and other targets will not want the same cooked asset shape.

### 4. Graphics Profiles content

The Graphics Profiles section should cover runtime presentation defaults:

- default backbuffer resolution
- default fullscreen or windowed mode
- default vsync behavior
- optional MSAA or similar presentation quality knobs
- platform-specific renderer defaults when the runtime exposes more than one valid choice

These values are also platform-specific because the host rendering backend is fixed by platform, but the output profile still varies by target and by renderer family.

## Menu Placement

The Build menu should expose a single new item:

- `Profiles...`

This keeps the menu from growing into separate profile entry points that mostly duplicate the same platform-selection workflow.

## Data Flow

- `EditorSession` opens the dialog from the Build menu.
- The dialog reads the current active platform from editor-local build settings.
- The dialog edits platform-scoped profile values in the build settings store.
- Confirming the dialog persists the current platform's settings immediately.
- The next build queue item snapshots the platform profile values that were active at queue time.

## Error Handling

The dialog should fail clearly rather than silently inventing defaults.

- If the active platform cannot be resolved, opening the dialog should fail.
- If a profile value is missing for a platform, the dialog should show the current platform defaults explicitly instead of creating a hidden fallback record.
- If a platform entry is missing from discovery, it should be visible elsewhere in the platform UI as missing, but the profile dialog should not allow editing a non-existent active platform.

## Testing

Add focused coverage for:

- the Build menu exposing `Profiles...`
- the dialog opening with the current active platform selected
- switching platforms inside the dialog loading the correct profile values
- confirming the dialog persisting the selected platform's Build and Graphics profile values
- keeping the existing `Platforms...` workflow separate from profile editing

## Out of Scope

This dialog does not change the runtime renderer itself.

- It does not remove vsync by default.
- It does not alter the Windows swap-chain behavior.
- It does not implement the actual profile consumers yet.

Those behaviors should be wired after the dialog and persistence shape are in place.

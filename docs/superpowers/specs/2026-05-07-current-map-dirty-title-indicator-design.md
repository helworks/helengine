# Current Map Dirty Title Indicator Design

## Goal

Show a `*` in the editor title bar when the currently open map has any unsaved changes.

This indicator must reflect unsaved changes across the full current map document set, not just the base scene file.

## Requirements

- Show `MapName*` when the current map has any unsaved changes.
- Show `MapName` when the current map is fully saved.
- Treat all current-map scene-authored documents as part of the same dirty state.
- Dirty means unsaved changes in any currently loaded map document, including:
  - base scene document
  - loaded platform sidecar scene documents
  - any other scene-owned save surface already treated as part of the current map save flow
- Clear the `*` only after all dirty documents for the current map have been saved successfully.
- Recompute immediately when:
  - the current map changes
  - the current map becomes dirty
  - the current map becomes fully clean again

## Non-Goals

- No runtime or packaging changes.
- No new scene persistence format.
- No title-bar-specific dirty tracking logic.
- No separate partial indicators per platform tab in this slice.

## Architecture

The dirty-state source of truth lives in the editor session or scene document-state layer.

The title bar remains presentation-only.

### Responsibilities

- `EditorSession` or the current scene document-state owner:
  - determines whether the current map is dirty
  - listens to scene mutation and save-completion events
  - recomputes the current map title state
  - pushes the resulting display title into the title bar
- `EditorTitleBar`:
  - renders the already-computed current map title
  - does not inspect save state directly

## Dirty-State Semantics

The current map is dirty when any loaded document belonging to that map is unsaved.

Examples:

- Base scene changed, no sidecars changed: dirty
- Base scene clean, one platform sidecar dirty: dirty
- Base scene dirty and one sidecar dirty: dirty
- Base scene clean and all sidecars clean: clean

This keeps the title bar aligned with the editor’s real save obligations.

## UI Behavior

Examples:

- Open clean map: `DemoDiscMainMenu`
- Edit base scene: `DemoDiscMainMenu*`
- Edit PS2 sidecar only: `DemoDiscMainMenu*`
- Save all current-map documents: `DemoDiscMainMenu`
- Switch to another clean map: show that map name without carrying the prior `*`

## Data Flow

1. Current map opens.
2. Editor session loads the base scene and all discovered sidecars for that map.
3. Document state computes whether any loaded current-map document is dirty.
4. Editor session composes the visible map title:
   - clean: `MapName`
   - dirty: `MapName*`
5. Editor session updates the title bar.
6. Any scene mutation or save completion repeats the dirty-state evaluation.

## Testing

Add coverage for:

- opening a clean map shows no `*`
- mutating the current base scene adds `*`
- mutating a loaded platform sidecar adds `*`
- saving all dirty current-map documents removes `*`
- switching to a different clean map removes the prior map’s dirty indicator

## Recommendation

Implement this through one unified `IsCurrentMapDirty` computation in the editor-session or scene document-state layer and keep `EditorTitleBar` presentation-only.

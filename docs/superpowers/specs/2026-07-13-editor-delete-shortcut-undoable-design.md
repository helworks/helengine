# Editor Delete Shortcut Undoable Design

## Goal

Add `Delete` support for removing the currently selected scene entity in the editor when editor-global shortcuts are allowed, and make that removal participate fully in the existing undo/redo history pipeline.

## Scope

This design covers:

- Global `Delete` keyboard handling in the editor.
- Deletion of the currently selected authored scene entity.
- Undo/redo support for entity deletion.
- Selection behavior during delete, undo, and redo.
- Focus and modal blocking behavior consistent with the existing save/undo/redo shortcuts.

This design does not cover:

- Component deletion through the same shortcut.
- Multi-selection deletion.
- Confirmation dialogs.
- Non-entity assets or hierarchy rows without a live selected entity.

## Requirements

1. Pressing `Delete` should remove the currently selected entity only when editor-global shortcuts are not blocked.
2. The shortcut must do nothing while dialogs or other editor-global blockers are active.
3. The shortcut must only delete authored scene entities, not internal editor entities or disposed entities.
4. The deletion must be undoable with `Ctrl+Z` and redoable with `Ctrl+Y` or `Ctrl+Shift+Z`.
5. Undo must restore the deleted entity in the same parent/location/state using the existing serialized entity history path.
6. Undo must restore selection to the recreated entity.
7. Redo must delete that entity again and clear the selection.

## Recommended Approach

Use the same architecture as the existing global save/undo/redo shortcuts:

- `EditorKeyboardFocusUpdateComponent` detects `Keys.Delete` and raises a dedicated callback.
- `EditorSession` handles the callback through a guarded global shortcut handler.
- Deletion is recorded as a dedicated history operation backed by `SerializedEditorEntityState`.

This approach matches the current input model, keeps shortcut blocking centralized, and avoids special-case delete logic in the hierarchy or viewport.

## Architecture

### Input Routing

`EditorKeyboardFocusUpdateComponent` will gain a `DeleteShortcutRequested` callback. When the user presses `Delete` without a control-modified undo/redo chord, the component will route that request through the callback in the same update loop that currently handles `Ctrl+S`, `Ctrl+Z`, and `Ctrl+Y`.

### Session Handling

`EditorSession` will gain a `HandleGlobalDeleteShortcut()` method that mirrors the existing global shortcut handlers:

- Return immediately when `IsEditorGlobalShortcutBlocked()` is true.
- Resolve the currently selected entity from `EditorSelectionService`.
- Ignore null, disposed, internal, or non-scene entities.
- Capture its serialized state through the existing history capture service.
- Delete the live entity through the same session-owned delete path used by history replay.
- Record a new deletion history operation.

### History Model

Add a dedicated delete operation rather than overloading creation semantics. The operation will hold one detached `SerializedEditorEntityState`.

- `Undo()` restores the serialized entity and reselects it.
- `Redo()` deletes the entity by stable scene id and clears selection.

This keeps history descriptions readable and makes delete semantics explicit.

### Selection Behavior

Selection should be predictable and simple:

- After immediate delete: selection is cleared.
- After undo: selection is restored to the recreated entity.
- After redo: selection is cleared again.

No previous-selection restoration is required for delete in this scope.

## File-Level Changes

### `engine/helengine.editor/components/EditorKeyboardFocusUpdateComponent.cs`

- Add `DeleteShortcutRequested`.
- Route `Keys.Delete` into that callback.
- Preserve the existing priority of undo/redo/save chords.

### `engine/helengine.editor/EditorSession.cs`

- Wire the new shortcut callback during session setup.
- Add `HandleGlobalDeleteShortcut()`.
- Add one helper that validates whether the current selection is a deletable authored scene entity.
- Add one helper that deletes the selected entity and records history through `EditorMutationService`.

### `engine/helengine.editor/history/EditorMutationService.cs`

- Add one explicit `RecordDeletedEntity(EditorEntity entity)` entry point.
- Capture the current serialized entity state before removal.
- Record a new delete history operation.
- Mark the scene mutated through the existing tracked-history path.

### `engine/helengine.editor/history/EntityDeletionHistoryOperation.cs`

- New history operation class for delete/restore replay.

### `engine/helengine.editor.tests/EditorSessionUndoRedoIntegrationTests.cs`

- Add integration tests for:
  - immediate delete of selected scene entity
  - undo restoring deleted entity
  - redo deleting it again
  - blocked delete while modal dialog is visible
  - keyboard-focus component routing `Keys.Delete`
  - ignoring delete when no valid scene entity is selected

## Data Flow

1. User presses `Delete`.
2. `EditorKeyboardFocusUpdateComponent` raises `DeleteShortcutRequested`.
3. `EditorSession.HandleGlobalDeleteShortcut()` checks editor-global blockers.
4. Session validates the current selection.
5. `EditorMutationService.RecordDeletedEntity(...)` captures detached serialized state and records `EntityDeletionHistoryOperation`.
6. Session clears selection and deletes the live entity.
7. Undo restores the serialized entity through `EditorHistoryContext.RestoreEntity(...)` and reselects it.
8. Redo deletes the recreated entity through `EditorHistoryContext.DeleteEntityById(...)` and clears selection.

## Error Handling

- Ignore the shortcut when there is no current selection.
- Ignore the shortcut when the selected entity is disposed.
- Ignore the shortcut when the selected entity is an internal editor entity.
- Ignore the shortcut when the selected entity is not an authored scene entity.
- Preserve existing failure behavior for unexpected history or restore/delete exceptions; do not add broad catch-and-hide behavior.

## Testing Strategy

Use TDD with the existing `EditorSessionUndoRedoIntegrationTests` fixture because it already exercises the real editor-session shortcut and history pipeline.

Primary assertions:

- Entity count decreases immediately after delete.
- Scene dirty state becomes dirty after delete when starting from a clean baseline.
- Undo restores entity count and selection.
- Redo removes the entity again.
- Blocked dialogs prevent deletion.
- `Delete` through `EditorKeyboardFocusUpdateComponent` routes into the same behavior.

## Risks

### Shortcut Conflicts

`Delete` must not interfere with text editing or focused controls. Reusing `IsEditorGlobalShortcutBlocked()` and the existing global shortcut routing minimizes this risk, though it intentionally keeps the current editor-global shortcut model rather than introducing per-control opt-out behavior.

### History Ordering

The entity state must be captured before live deletion. If deletion happens first, the serialized restore payload is lost and undo cannot restore the entity correctly.

### Selection Lifetime

The delete path must clear selection before or during deletion so disposed entity references are not left behind in selection-driven UI paths.

## Success Criteria

- Pressing `Delete` on a selected scene entity removes it.
- `Ctrl+Z` restores the deleted entity with the same stable scene id and serialized state.
- `Ctrl+Y` and `Ctrl+Shift+Z` delete it again.
- The shortcut does nothing while open dialogs block global shortcuts.
- No crashes are introduced from disposed selection references during delete/undo/redo.

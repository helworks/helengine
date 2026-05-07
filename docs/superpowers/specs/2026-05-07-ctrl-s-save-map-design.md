# Ctrl+S Save Map Design

## Goal

Support `Ctrl+S` as an editor-global shortcut that saves the current map through the existing `Save Map` flow.

## Requirements

- Pressing `Ctrl+S` saves the current map.
- If the current map already has a saved path, `Ctrl+S` saves directly to that path.
- If the current map does not have a saved path yet, `Ctrl+S` opens the existing save dialog.
- `Ctrl+S` must use the same save behavior as the existing `Save Map` title-bar command.
- `Ctrl+S` must not save the map when a blocking modal dialog is open.
- Existing plain `S` behavior for focused controls must remain unchanged.

## Non-Goals

- No new save flow or save service.
- No changes to runtime or packaging.
- No title-bar-specific keyboard handling.
- No additional shortcuts in this slice.

## Architecture

Keep shortcut detection in the editor-wide keyboard update path and keep save behavior in `EditorSession`.

### Responsibilities

- `EditorKeyboardFocusUpdateComponent`
  - detects `Ctrl+S`
  - respects modal/editor-global input blocking rules
  - invokes the existing editor-session save command route
- `EditorSession`
  - continues to own `HandleSaveMapRequested()`
  - decides whether to save directly or show the save dialog

This keeps the shortcut as a global editor command while avoiding a second save path.

## Input Behavior

`Ctrl+S` is an editor-global shortcut.

Behavior:

- normal editor interaction active:
  - invoke the existing save-map command
- current map has a path:
  - save directly
- current map has no path:
  - show save dialog
- blocking modal dialog visible:
  - do nothing

Plain `S` remains available for existing focused-control activation paths such as the viewport tool mode behavior.

## Data Flow

1. Keyboard update runs for the frame.
2. The editor-wide keyboard update component detects `Ctrl+S`.
3. If editor-global input is not blocked by a modal dialog, it routes the command to the existing save-map handler.
4. `EditorSession.HandleSaveMapRequested()` executes the current save behavior:
   - show save dialog if the scene has no path
   - otherwise save directly

## Testing

Add coverage for:

- `Ctrl+S` with an existing scene path triggers the same save path as `Save Map`
- `Ctrl+S` with no scene path shows the save dialog
- `Ctrl+S` is ignored while a blocking modal dialog is open
- existing plain `S` behavior for focused controls is unchanged

## Recommendation

Implement `Ctrl+S` in `EditorKeyboardFocusUpdateComponent` and route it into the existing `EditorSession.HandleSaveMapRequested()` flow without adding any new save logic.

# Logger Panel Multi-Select And Copy Design

## Goal

Add row selection, keyboard-driven multi-selection, right-click copy, and `Ctrl+C` copy to the editor `LoggerPanel`.

The logger remains an editor-only dock panel. This change affects only log-panel presentation and interaction behavior. It must not change the global logger pipeline or runtime logging systems.

## Scope

This slice adds:

- single-row selection by mouse click
- multi-row selection by `Ctrl+click`
- contiguous range selection by `Shift+click`
- focused-row keyboard navigation
- keyboard multi-selection with `Shift+Up` / `Shift+Down`
- focus movement without clearing selection with `Ctrl+Up` / `Ctrl+Down`
- toggle of the focused row with `Ctrl+Space`
- `Ctrl+C` copy
- right-click context menu with `Copy`
- copy payload built from all selected visible row strings
- auto-scroll to keep the focused row visible during keyboard navigation

This slice does not add:

- log filtering
- per-level commands
- clear-log actions
- arbitrary text selection inside one row
- runtime clipboard support

## Interaction Model

`LoggerPanel` owns three pieces of row interaction state:

- `FocusedRowIndex`
- `AnchorRowIndex`
- `SelectedRowIndices`

These are panel-local state values. They are not stored in `LogEntry`, the logger service, or global editor selection systems.

### Mouse behavior

- plain left click:
  - focuses the clicked row
  - sets anchor to that row
  - clears prior selection
  - selects only that row
- `Ctrl+left click`:
  - focuses the clicked row
  - sets anchor to that row
  - toggles that row inside the selected set
- `Shift+left click`:
  - if anchor exists, selects the inclusive range from anchor to clicked row
  - if anchor does not exist, behaves like plain click
- right click:
  - if the clicked row is not already selected, selection becomes only that row
  - focuses that row
  - opens a small context menu with one item: `Copy`

### Keyboard behavior

The logger panel must participate in the existing editor keyboard focus system so these actions only apply while the panel has focus.

- `Up` / `Down`:
  - moves focus one row
  - clears previous multi-selection
  - selects only the newly focused row
  - updates anchor to the newly focused row
- `Shift+Up` / `Shift+Down`:
  - moves focus one row
  - preserves anchor
  - updates the selected range from anchor to focused row
- `Ctrl+Up` / `Ctrl+Down`:
  - moves focus one row
  - keeps existing selection unchanged
  - does not reset anchor
- `Ctrl+Space`:
  - toggles the focused row in the selected set
  - sets anchor to the focused row
- `Ctrl+C`:
  - copies all selected rows
  - if no rows are selected but a focused row exists, copies the focused row

### Copy payload

Copy uses the visible formatted row text already shown by the panel, in the same top-to-bottom order the rows appear in the logger.

Payload format:

- one formatted row per line
- joined with `Environment.NewLine`

No extra metadata is added beyond the current visible line format.

## UI Structure

`LoggerPanel` stays the owner of all new behavior.

### LoggerPanel responsibilities

- maintain selection, focus, and anchor state
- build and show the row context menu
- handle keyboard navigation and copy shortcuts while focused
- ensure focused row visibility inside the scroll viewport
- build copy payload text
- send text to the editor clipboard service

### LoggerPanelRow responsibilities

`LoggerPanelRow` should remain a presentational container, but it will need enough structure to support interaction:

- row root entity
- background visual
- label host
- label text
- row interactable

It should not own selection logic itself. Pointer events should forward to `LoggerPanel`.

## Visual Behavior

Rows need explicit visual states:

- normal
- selected
- focused
- selected + focused

Recommended rule:

- selected rows use a clear accent background tint
- focused row uses a stronger outline or stronger tint within the selected state
- alternate-striping can remain for unselected rows

The focused row must still be visually identifiable when multiple rows are selected.

## Scrolling

The logger already uses a vertically stacked row layout. This design assumes the panel will gain or reuse a scrolling seam that allows the focused row to remain visible.

Behavior:

- after any keyboard focus move, if focused row is above the visible window, scroll upward
- if focused row is below the visible window, scroll downward
- pointer selection does not need aggressive auto-scroll beyond existing pointer behavior

If the current logger has no `ScrollComponent`, the implementation should add one generically rather than invent per-row scroll math ad hoc.

## Entry Trimming And Stability

`LoggerPanel` trims old entries from the top when `entries.Count > MaxEntries`.

Selection state must remain consistent when this happens:

- selection is tracked against current visible row indices
- when top rows are trimmed, focused index, anchor index, and selected indices shift downward by the trim count
- any rows that fall out of range are removed from the selected set
- if focus falls out of range completely and entries remain, focus should clamp to the nearest valid row

This keeps append-and-trim behavior deterministic without changing `LogEntry`.

## Testing

Add tests in `engine/helengine.editor.tests/LoggerPanelTests.cs` for:

- plain click selects one row
- `Ctrl+click` toggles multiple rows
- `Shift+click` selects a contiguous range
- keyboard `Up/Down` moves focus and selection
- `Shift+Up/Down` extends the range
- `Ctrl+Up/Down` moves focus without clearing selection
- `Ctrl+Space` toggles the focused row
- `Ctrl+C` copies selected rows joined together
- right-click `Copy` uses the same payload as keyboard copy
- focus auto-scrolls into view
- selection/focus are re-normalized correctly when old entries are trimmed

## Files Expected To Change

### Modified

- `engine/helengine.editor/components/ui/LoggerPanel.cs`
- `engine/helengine.editor/components/ui/LoggerPanelRow.cs`
- `engine/helengine.editor/components/ui/LoggerPanelUpdater.cs`
- `engine/helengine.editor.tests/LoggerPanelTests.cs`

### New

- possibly one small logger context-menu helper or dialog type if the existing context-menu utilities are not reusable cleanly

## Notes

This design intentionally keeps the feature local:

- no changes to runtime logging
- no coupling to scene/entity selection
- no freeform text selection model

The logger becomes a row-selection surface with copy support, which is the correct abstraction for the requested behavior.

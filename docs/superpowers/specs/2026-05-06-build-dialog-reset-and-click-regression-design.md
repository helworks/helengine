# Build Dialog Reset And Click Regression Design

## Summary

Fix two regressions in the editor Build dialog:

1. Adding one build item or removing one queue item must not reset the dialog position.
2. Pointer clicks on the dialog close button and queued-item remove buttons must work before the user drags the dialog.

## Current Behavior

`EditorSession` refreshes the visible Build dialog after queue mutations by calling `BuildDialog.Show(...)`.
`BuildDialog.Show(...)` currently resets dialog positioning state before rebuilding the platform rows, queue rows, and build logs.
That causes the panel to lose its manual position and snap back to the centered default after queue actions.

The clickability regression appears in the initial shown state: close and queue-item remove actions become clickable only after the panel has been moved once.
That indicates a mismatch between the dialog's initial layout state and the pointer interaction state that targets dialog-owned controls.

## Goals

- Preserve manual Build dialog position across queue refreshes.
- Keep first-open behavior unchanged: opening the dialog from the editor may still center it.
- Ensure close and queue-item remove actions are clickable immediately after the dialog is shown.
- Add regression tests that fail before the fix and pass after the fix.

## Non-Goals

- No unrelated dialog refactor.
- No changes to build queue persistence format.
- No new UI behavior beyond fixing the reset and click regressions.

## Design

### Dialog Refresh Behavior

Split the Build dialog lifecycle into two behaviors:

- `Show(...)` remains the first-open entry point and can keep reset-and-center semantics.
- A new refresh path rebinds the current config, scenes, platform selection model, queue rows, and build logs without clearing manual positioning state.

`EditorSession` will use the refresh path after:

- `HandleBuildDialogAddRequested(...)`
- `HandleBuildDialogRemoveQueueItemRequested(...)`
- any other queue-state update that currently re-shows the same visible dialog instance

This keeps the queue and form state current without recentering the dialog.

### Pointer Interaction Behavior

Add regression coverage for pointer clicks against the Build dialog in its initial visible state.
The fix must ensure that:

- the title-bar close button receives pointer interaction without requiring a prior drag
- queue-item remove buttons receive pointer interaction without requiring a prior drag

The implementation should correct the underlying initial-state routing or layout issue rather than adding a one-off workaround tied only to Build dialog buttons.

## Testing

Add failing tests first for:

- preserving a moved dialog position after add-to-build refresh
- preserving a moved dialog position after queue-item removal refresh
- clicking the Build dialog close button before moving the panel
- clicking a queue-item remove button before moving the panel

After implementation, run the focused Build dialog and editor-session build-queue test coverage.

## Risks

- If the clickability regression comes from shared modal-shell state in `EditorDialogBase`, the final fix may land in shared dialog infrastructure instead of `BuildDialog`.
- If `Show(...)` currently performs hidden initialization relied on by queue refreshes, the refresh path must preserve those bindings explicitly.

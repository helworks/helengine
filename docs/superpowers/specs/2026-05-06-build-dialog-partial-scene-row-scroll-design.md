# Build Dialog Partial Scene Row Scroll Design

## Goal

Make the Build dialog scene list feel like a clipped scrolling document instead of a whole-row pager. When the scene-list viewport ends partway through the next row, that partially visible row must still render inside the clipped area and remain interactable within its visible portion.

## Current Problem

The scene list in `BuildDialog` is virtualized. Its pooled row count is derived from `GetSceneListVisibleRowCount()`, which currently uses integer division on the available content height divided by the scene row height. That rounds down.

Because the pool is sized from that rounded-down value, the last partially visible row is never instantiated for the viewport. The clip boundary works, but only on rows that exist. The result is that the list appears to stop at full rows instead of showing the next row clipped at the viewport edge.

## Chosen Approach

Keep the current virtualization and clipping architecture, but change the visible-row capacity calculation from floor semantics to ceil semantics.

This means:

- if the viewport fits an exact number of rows, behavior stays the same
- if the viewport fits `N` full rows plus part of the next row, the pool allocates `N + 1` rows
- the existing clip region still trims the extra row at the viewport edge
- the extra row remains a normal pooled row, so existing row input behavior continues to apply

## Rejected Approaches

### Disable virtualization for the scene list

This would make the list behave more like a plain document flow, but it throws away the existing pooled-row pattern already used in the dialog and adds unnecessary rendering/input cost for large scene lists.

### Add a special-case partial "peek row"

This would duplicate row layout and input handling just to render one clipped row. It is more complex than the underlying problem and creates a second rendering path for the same scene-row UI.

## Implementation Design

### Scene Row Capacity

Update the Build dialog helper that computes visible scene-row capacity so it rounds up whenever the available scroll content height is not an exact multiple of the scaled scene row height.

The helper should still:

- return at least `1`
- use the existing scaled scene-list padding and scaled row height
- avoid changing queue-list or build-log virtualization behavior

The new behavior is specifically for the scene-list visible-row count contract.

### Scene Row Layout

No changes are needed to the row binding path beyond consuming the updated visible-row count.

`UpdateSceneListRowsLayout()` should continue to:

- size the scroll component viewport the same way
- ensure the pooled scene-row count matches the visible-row capacity
- bind rows from the current scene-list scroll offset
- disable only rows whose bound scene index falls outside `DisplayedSceneIds`

Because the extra partially visible row is now part of the row pool, the current clip boundary should naturally trim it at the bottom edge.

### Input Behavior

Do not add special-case hit testing or partial-row input gating.

The desired behavior is:

- the partially visible row is a real pooled row
- its visible clipped portion remains clickable through the existing row components
- there is no separate "render-only" row mode

This keeps the scene list consistent with the rest of the dialog and avoids extra input-state complexity.

## Testing

Add regression coverage in `BuildDialogTests` that exercises a scene-list viewport whose inner content height is not evenly divisible by the scaled scene row height.

The regression should verify:

- `SceneListScrollComponent.VisibleItemCount` includes the extra partially visible row
- the visible scene-row collections include that trailing clipped row
- the first scrolled viewport still updates correctly after scroll offset changes

If the existing Build dialog input harness can target the partially visible row reliably without adding brittle coordinate coupling, add one assertion proving the visible portion remains clickable. If not, the required regression scope is the visible-row allocation and binding contract, because that is the root cause.

## Non-Goals

- changing queue-list virtualization
- changing build-log virtualization
- removing scene-list virtualization
- changing modal clipping or camera ordering
- redesigning scroll affordances or adding visible scrollbars

## Success Criteria

The Build dialog scene list shows a clipped trailing row whenever the viewport extends into the next row, instead of stopping at the previous full row boundary. Scrolling should feel continuous and document-like, while still using the existing virtualized row pool.

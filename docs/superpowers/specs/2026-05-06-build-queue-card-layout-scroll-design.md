# Build Queue Card Layout And Scroll Design

## Summary

The `BuildDialog` queue column already virtualizes queued builds with `BuildDialogQueueRow` instances and a `QueueScrollComponent`, but the current cards are too short and try to show too much text in too little space. The row summary and status detail can extend into the remove-button lane, which makes the `X` hard or impossible to hit. The queue also still behaves like a compact single-line list even though each queued build carries enough metadata to need a clearer multiline presentation.

This change keeps the existing queued-build virtualization model, but redesigns each queue card as a taller fixed-height card with a reserved remove-button strip and a compact three-line summary. The queue cards will show only straightforward build state, while verbose failure details stay in the build log section. The queue column continues to scroll through fixed-size cards using the existing `QueueScrollComponent`.

## Goals

- Make each queued build card taller and easier to scan.
- Ensure queue text never overlaps the remove `X` button.
- Show queued build information across multiple fixed lines instead of dense clipped status text.
- Keep queue scrolling working when the dialog has more queued builds than fit in the visible queue viewport.
- Keep detailed error text out of queue cards and in the build log area.

## Non-Goals

- Do not switch queue cards to variable per-item heights.
- Do not move detailed error messages back into the queue cards.
- Do not replace the existing queue virtualization model with a document-style pixel scroller.
- Do not redesign the build log section or queue execution flow.

## Current Structure

`BuildDialog` already owns:

- one bordered queue section on the right side of the dialog,
- one `QueueItemsRoot` under the queue header,
- one `QueueScrollComponent` attached to `QueueItemsRoot`, and
- one pooled set of `BuildDialogQueueRow` instances that are rebound against the current scroll offset.

Each row currently renders:

- one fixed-height background,
- one remove button anchored near the top-right,
- one text block containing a compact summary line and one clipped status-message line.

This has two problems:

1. The fixed row height is too small for the amount of text being rendered.
2. The row text builder includes `StatusMessage`, which is often the longest and noisiest field, and that pushes visual density into the same narrow region that needs to stay clear for the remove button.

## Recommended Approach

Keep the existing row-based `QueueScrollComponent` and pooled `BuildDialogQueueRow` instances, but redesign the queue cards around a taller fixed-height multiline layout.

This is the smallest change that solves the actual bug. The queue already scrolls by row index, which matches fixed-height cards well. That means the implementation can stay focused on:

- card geometry,
- text composition,
- reserved remove-button space,
- visible-row math derived from the new scaled card height.

## Queue Card Content

Each queued build card will render at most three compact lines:

### Line 1

`platform` and `status`

Examples:

- `windows | Pending`
- `ps2 | Running`
- `linux | Failed`

### Line 2

`scene count` and build flavor

Examples:

- `3 scene(s) | Debug`
- `1 scene(s) | Release`

### Line 3

One clipped compact capability summary assembled only from values that are actually present:

- `build <id>`
- `gfx <id>`
- `codegen <id>`
- `modules <count>`

When the third line would exceed the available text width, it is clipped to fit. Empty segments are omitted instead of reserving placeholder text.

## Content Exclusions

Queue cards will no longer render `StatusMessage`.

Detailed error text, long build output, and verbose state explanation remain in the build log section below the main dialog content. This keeps queue cards readable and removes the main source of overlap and clutter.

## Layout Model

### Fixed Taller Card Height

Each queued build card keeps one fixed height for all items, but that height increases enough to fit:

- top padding,
- three readable text lines,
- a dedicated remove-button strip,
- bottom separator spacing.

The height must be derived through `DialogMetrics` scaling so the queue remains consistent under non-default editor UI scale.

### Reserved Remove Button Lane

The remove `X` button keeps a dedicated top-right lane that the text block cannot enter.

The row text width must be computed from:

- full card width,
- left text padding,
- right text padding,
- remove-button width,
- gap between text and button.

The text layout should not rely on clipping after overlap. The button lane must be excluded from text width up front.

### Text Placement

The text block remains one `TextComponent`, but its height must match the taller card and its content must be intentionally composed as three lines at most.

The text block origin stays aligned to the left padding and top padding of the card. The remove button remains visually aligned near the top of the card so it stays easy to target and consistent across rows.

## Scrolling Model

The queue should continue using the existing `QueueScrollComponent`.

Required behavior:

- recompute queue viewport size using the scaled taller card height,
- recompute visible-row count from the current queue viewport height and the scaled card height,
- clamp the scroll offset whenever queue length or viewport size changes,
- keep row virtualization bound to queued item index instead of rendering all items at once.

This preserves the current row-scrolling model while making it correctly reflect the new card geometry.

## Viewport And Row Math

All queue-row calculations must use the scaled card height instead of the unscaled constant.

That includes:

- row background height,
- row `Y` placement,
- visible-row count,
- queue viewport capacity,
- separator placement,
- text height bounds.

Using mixed scaled and unscaled values here would produce partial overlap, incorrect scroll ranges, or cards being clipped unexpectedly at non-default UI scales.

## Testing Strategy

Add regression coverage in `BuildDialogTests` for the queue card layout and scroll behavior.

Required coverage:

1. Queue card text excludes `StatusMessage`.
2. Queue card text uses the expected multiline summary structure.
3. Queue row text width stays constrained to the reserved button-safe width.
4. Enough queued builds produce a positive `QueueScrollComponent.MaximumScrollOffset`.
5. Scrolling the queue changes which queue item appears in the first visible pooled row.

The tests should continue to use the existing dialog test style with reflected access to private queue fields.

## Risks

### Scaled Height Drift

If the queue still uses unscaled `QueueRowHeight` for row positioning or visible-row count while card visuals use scaled dimensions, scrolling will desynchronize from rendered rows.

Mitigation:

- route all queue-row geometry through scaled helper methods,
- avoid direct use of the raw constant in layout code.

### Summary Line Bloat

If the third line includes too many optional segments, the card becomes noisy again.

Mitigation:

- keep the first two lines mandatory and simple,
- keep the third line compact and clipped,
- omit missing segments entirely.

### Remove Button Regression

If the row text width is not reduced before assignment, the card can still visually or interactively crowd the `X`.

Mitigation:

- compute a dedicated text-safe width from the reserved button lane,
- cover that width calculation in tests.

## Implementation Direction

Implementation should stay localized to:

- `BuildDialogQueueRow.cs` for the row visual bounds,
- `BuildDialog.cs` for queue summary composition, row layout, scaled-height math, and scroll state,
- `BuildDialogTests.cs` for regressions around card content and queue scrolling.

The build log section should remain unchanged except for continuing to serve as the place where detailed queue-item errors are visible.

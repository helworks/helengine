# Fitted Canvas Anchor Space Design

## Summary

Fix the demo menu layout contract so `1280x720` and `853x480` produce the same visual result.

Both resolutions are `16:9`, so the menu should resolve to the same normalized layout, with the same framing and relative placements, only at different pixel densities.

The current bug exists because reference-canvas scaling and anchoring are still partly resolved against the raw window instead of a single fitted canvas rect. This spec makes the fitted canvas rect the shared layout space for both scaling and anchoring.

## Problem

The current reference-canvas fit path applies a uniform scale, but the menu subtree still mixes:

- authored reference-space positions and sizes
- anchor distances
- live raw window dimensions

That split is acceptable for visibly different aspect ratios like `4:3`, but it is wrong for same-aspect targets.

For `1280x720` and `853x480`:

- the aspect ratio is the same
- the fitted layout rect should also be the same in normalized terms
- no menu element should move outside the fitted canvas
- no menu element should need a different placement decision

Today that does not hold, and reducing to `853x480` leaks parts of the menu outside the screen.

## Goals

- Make `1280x720` and `853x480` render the demo menu identically in normalized layout terms.
- Introduce one fitted canvas rect that becomes the single runtime layout space for reference-canvas UI.
- Make anchor resolution consume that fitted canvas rect instead of the raw window.
- Preserve the existing responsive behavior for non-matching aspect ratios like `640x480`.
- Keep the change generic so future UI scenes can reuse it.

## Non-Goals

- No full replacement of the engine UI layout system in this change.
- No removal of `AnchorComponent` across the engine.
- No city-specific menu hacks.
- No second authored menu scene per resolution.

## Root Cause

Reference-canvas scaling currently answers only one question:

- how much should authored reference-space values scale?

It does not fully answer the second question:

- what rectangle is the scaled content being laid out inside?

Because anchoring still reasons against live window bounds directly, same-aspect windows can produce different final placements even when the scale is mathematically correct.

That is the contract bug. The system has two layout spaces:

- reference content space
- raw window space

and no first-class fitted canvas space in between.

## Proposed Runtime Contract

### 1. Fitted Canvas Rect

Reference-canvas UI resolves a fitted canvas rect from:

- authored reference canvas width and height
- live window width and height

That rect contains:

- fitted origin `X`
- fitted origin `Y`
- fitted width
- fitted height
- uniform fit scale

For same-aspect resolutions like `1280x720` and `853x480`, the fitted rect covers the full live window and has zero padding on both axes.

For aspect-mismatched targets like `640x480`, the fitted rect remains centered inside the live window and carries the expected horizontal or vertical padding.

### 2. Single Layout Space

The fitted canvas rect becomes the single runtime layout space for reference-canvas UI.

That means:

- authored positions scale into fitted-canvas coordinates
- authored sizes scale into fitted-canvas sizes
- anchors resolve against fitted-canvas bounds
- pointer and interactable math must agree with the same fitted-canvas result

Nothing in this flow should anchor reference-canvas UI directly against raw window size anymore.

### 3. Same-Aspect Invariance

If two window sizes have the same aspect ratio as the authored reference canvas, layout must be invariant in normalized terms.

For `1280x720` and `853x480`, this means:

- the fitted canvas origin is `(0, 0)` in both cases
- the fitted canvas fills the window in both cases
- every scaled position is just the authored normalized position multiplied by the new pixel size
- no additional anchoring drift is allowed

### 4. Anchor Compatibility

`AnchorComponent` remains the current placement mechanism, but its effective parent layout space changes.

Instead of treating the raw live window as the anchor space, anchored reference-canvas UI should treat the fitted canvas rect as the anchor space.

This keeps the existing component model intact while unifying the math behind:

- scale
- offset
- anchor placement

## Architecture

### ReferenceCanvasFitComponent

`ReferenceCanvasFitComponent` should no longer publish only a scale implicitly through direct subtree mutation.

It should resolve and apply a first-class fitted canvas result:

- fitted origin
- fitted size
- scale

The component can still own subtree snapshotting and mutation, but the fitted canvas result must drive all later layout decisions.

### ReferenceCanvasFitSnapshot

Snapshot application should scale authored values into fitted-canvas space, not raw window space.

If anchor distances are stored in authored reference units, they should be scaled and then resolved relative to the fitted canvas bounds.

### Anchor Resolution

`AnchorComponent` needs a unified parent layout-space input for this flow.

This does not require deleting anchor components. It requires making them resolve against the correct rectangle.

For this change, the correct rectangle is:

- the fitted canvas rect for reference-canvas UI
- the existing parent or viewport space for everything else

### Input and Interactable Alignment

Any pointer hit testing or interactable bounds that participate in reference-canvas UI must remain aligned with the same fitted-canvas result.

The menu cannot visually scale into one rect while interactables still assume another.

## Implementation Boundaries

This change should stay focused on the shared runtime layout contract:

- reference-canvas fit computation
- fitted canvas rect propagation
- anchor resolution against fitted canvas space
- preserving pointer/interactable alignment

It should not broaden into:

- replacing all UI layout with a new container system
- reauthoring the demo menu scene
- changing unrelated editor panel layout

## Testing

Add targeted regressions that prove:

- `1280x720` and `853x480` produce equivalent normalized bounds for the demo menu root
- `1280x720` and `853x480` produce equivalent normalized bounds for the first panel and first menu item
- `853x480` keeps the menu inside the fitted canvas bounds
- `640x480` still adapts correctly for `4:3`
- interactable bounds remain aligned with scaled visuals after the fitted canvas rect is introduced

## Success Criteria

The change is complete when:

- changing from `1280x720` to `853x480` produces the same menu composition and framing
- no part of the menu leaks outside the screen at `853x480`
- `4:3` targets still adapt through the intended fitted-canvas behavior
- the fix lives in the shared reference-canvas and anchor contract, not in demo-menu special cases

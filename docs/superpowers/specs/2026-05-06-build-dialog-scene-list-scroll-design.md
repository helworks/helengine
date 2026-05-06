# Build Dialog Scene List Scroll Design

## Summary

The `BuildDialog` already bounds the queue column and build log area with dedicated `ScrollComponent` viewports, but the left-side scene list still instantiates one order field, label, and checkbox triplet for every scene directly under `SceneListRoot`. When a project contains enough scenes, those controls continue past the bordered scene-list area and visually collide with the lower-left controls. The dialog needs the same row-based scrolling model it already uses elsewhere so scenes remain accessible without expanding the panel or letting content escape its container.

This change keeps the current bordered scene-list presentation and selection behavior, but converts the scene list to a scrollable viewport with pooled visible rows driven by a new scene-list `ScrollComponent`.

## Goals

- Keep the scene list visually bounded inside its existing bordered area.
- Allow projects with many scenes to access every scene entry through wheel scrolling inside the scene-list area.
- Match the dialog's existing queue and build-log scroll behavior instead of introducing a different interaction model.
- Preserve the existing scene selection and scene order editing flows.
- Keep footer and lower-left controls inside the dialog bounds regardless of scene count.

## Non-Goals

- Do not add a visible scrollbar widget.
- Do not redesign the left column layout or move the lower-left controls.
- Do not change the semantics of scene ordering, scene selection, or platform configuration persistence.
- Do not introduce a reusable global scroll-view abstraction.

## Current Problem

`BuildDialog` currently:

- sizes `SceneListBackground` to a bounded height,
- positions `SceneListRoot` inside that bounded region, and
- appends every scene row directly as child entities of `SceneListRoot`.

That means the background rectangle stops at the intended viewport boundary, but the actual row controls do not. Large scene sets cause:

1. scene rows to extend below the bordered area,
2. footer controls to share space with scene controls, and
3. no interaction path to reach hidden scenes if the footer happens to cover them.

The queue and build-log sections already solved this class of problem with local `ScrollComponent` ownership and visible-slice rendering. The scene list should use the same model.

## Recommended Approach

Add a scene-list-local scroll viewport inside `BuildDialog` with these pieces:

1. A `SceneListItemsRoot` parented under `SceneListRoot`.
2. A `SceneListScrollComponent` attached to `SceneListItemsRoot`.
3. A pooled set of visible scene-row entities under `SceneListItemsRoot`.
4. One refresh path that binds the pooled rows against `DisplayedSceneIds` using the current scroll offset.

This preserves the existing dialog structure while making the scene list behave like the queue: fixed-height rows, item-count-based scrolling, and bounded pointer-wheel interaction.

## Why This Approach

### Option 1: Reuse `ScrollComponent` with visible-row pooling

This is the recommended option.

Benefits:

- Matches the dialog's existing queue and build-log interaction model.
- Fixes the overflow at the layout boundary instead of hiding symptoms.
- Scales cleanly for larger scene counts because only the visible slice is instantiated.
- Keeps the change localized to `BuildDialog` without requiring new engine primitives.

Tradeoffs:

- Requires splitting current one-shot scene-row creation into scroll state plus row refresh.
- Adds one more scroll controller and pooled row set to the dialog.

### Option 2: Keep all rows alive and shift a content root

Benefits:

- Smaller code diff than full virtualization.
- Uses the same scrolling input controller.

Tradeoffs:

- Diverges from the queue pattern already established in this dialog.
- Keeps potentially large numbers of text boxes and checkboxes alive unnecessarily.
- Still requires careful enablement and pointer behavior to avoid hidden controls receiving input.

### Option 3: Only clamp layout and push footer controls downward

Benefits:

- Minimal structural change.

Tradeoffs:

- Does not provide access to hidden scenes.
- Treats overflow as spacing pressure instead of a scrollable content problem.
- Breaks once the scene list exceeds the remaining column height again.

## Architecture

### BuildDialog responsibilities

`BuildDialog` remains responsible for:

- deriving the ordered scene id list for the active platform,
- binding scene order fields and selection checkboxes to the mutable platform config,
- computing the bounded scene-list viewport height from the surrounding layout, and
- updating visible rows whenever the active platform, scene list, or scroll offset changes.

### New scene-list structure

The left column scene list will be split into:

- `SceneListRoot` for the background and shake animation offset,
- `SceneListItemsRoot` for the visible pooled row entities, and
- `SceneListScrollComponent` for wheel-driven item scrolling within the bounded viewport.

The background remains on `SceneListRoot` so invalid-state border coloring and shake feedback still move the container as one unit. The visible row entities live beneath the content root so they can be rebound independently from the border shell.

### Row model

Each visible pooled row needs:

- one order-field host with one `TextBoxComponent`,
- one label host with one `TextComponent`, and
- one checkbox host with one `CheckBoxComponent`.

Rows remain fixed-height using the existing scene row sizing constants. The scroll offset is measured in row units, not pixels, so the existing `ScrollComponent` stays appropriate.

## Data Flow

`Show` and platform changes should continue to:

1. resolve the active `EditorBuildPlatformConfigDocument`,
2. build `DisplayedSceneIds` in the correct order,
3. update output-directory and other lower controls, and
4. refresh the scene-list viewport state.

The scene-list refresh path should then:

- set `SceneListScrollComponent.ItemCount` from `DisplayedSceneIds.Count`,
- compute `VisibleItemCount` from the current scene-list viewport height and scaled row height,
- clamp the scroll offset when item count or viewport height changes, and
- bind each pooled row to `DisplayedSceneIds[scrollOffset + rowIndex]`.

Binding a row means:

- writing the displayed order number into the row's text box,
- setting the row label text to the scene id,
- setting the row checkbox state from the active platform config, and
- enabling or disabling the row when it is inside or outside the visible slice.

The existing handlers for order changes and checkbox changes should remain the persistence path. The new scroll model should not change how scene edits are stored, only which row controls are currently visible.

## Layout And Interaction

The bordered scene-list area remains the visual viewport. Its height should still be computed from the available space above the lower-left controls.

Required layout behavior:

- `SceneListBackground` keeps the same bounded width and computed height.
- `SceneListItemsRoot` stays inset by the existing `SceneListPadding`.
- The scene-list scroll viewport width excludes the left and right list padding.
- The scene-list visible row count uses the bounded viewport height rather than the total number of scenes.

Required interaction behavior:

- Wheel scrolling only activates while the pointer is inside the scene-list viewport.
- The scene-list scroll offset resets or clamps appropriately when switching platforms or changing scene count.
- The invalid-scene shake and border highlight continue to apply to the whole scene-list container.
- Lower-left controls must stay below the bordered scene list rather than competing with unbounded scene rows.

## Error Handling And Constraints

- If the scroll component reference is unexpectedly null during refresh, the dialog should fail loudly rather than silently falling back to overflow behavior.
- Visible row count must clamp to at least one row when the viewport is very small.
- Scroll offset must clamp whenever the active platform changes or the viewport height changes.
- The implementation should avoid constructing synthetic default platform data; it must continue to use the existing active config resolution path and throw where the current dialog already requires valid data.

## Testing Strategy

Add regression coverage in `BuildDialogTests`.

Required coverage:

1. Many scenes produce a positive `SceneListScrollComponent.MaximumScrollOffset`.
2. The number of instantiated visible scene rows matches `VisibleItemCount`, not total scene count.
3. Scrolling the scene list changes which scene id appears in the first visible pooled row.
4. The bounded scene-list background still has positive height and the lower-left controls remain within dialog bounds when many scenes are present.
5. Existing scene selection/order editing tests continue to pass against the pooled-row model.

The tests should mirror the current queue scroll tests: inspect the dialog through reflected access to the private scene-list fields, assert positive scroll range, perform `ScrollTo`, and verify the rendered row content changes accordingly.

## Risks

### Row binding event churn

If row controls update their text or checked state while their change handlers are live, rebinding visible rows could write unintended values back into the active platform config.

Mitigation:

- guard row-rebinding paths against treating programmatic updates as user edits, or temporarily suppress row event handling while a row is being rebound.

### Scaled row math drift

If the scene-list viewport height and visible-row count mix scaled and unscaled constants, rows can overlap or the scroll range can desynchronize from what is drawn.

Mitigation:

- route row height and viewport calculations through scaled helper methods, matching the queue and build-log layout pattern.

### Footer overlap regression

If the scene-list viewport height is computed correctly but child rows are not disabled or rebound correctly, stale rows could still appear beneath the intended viewport.

Mitigation:

- keep the visible row pool explicitly enabled only for in-range entries,
- cover scroll-range and row-slice behavior in tests.

## Implementation Direction

Implementation should stay localized to:

- `engine/helengine.editor/components/ui/BuildDialog.cs` for scene-list scroll state, row pooling, layout, and row binding,
- `engine/helengine.editor.tests/BuildDialogTests.cs` for scene-list overflow and scroll regressions.

No other dialog or shared UI control needs to change for this fix.

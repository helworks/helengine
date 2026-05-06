# Properties Panel Scroll Design

## Summary

The `PropertiesPanel` currently lays out all visible content directly under one shared `contentRoot` without any scroll owner. When the selected asset or entity exposes more fields, component sections, or controls than fit inside the dock body, the lower content renders outside the panel bounds and becomes inaccessible.

This change makes the entire panel body behave like one scrollable document. Asset metadata, import settings, transform controls, the add-component button, and component property sections all remain part of the same vertical flow, but the visible region is constrained to the dock body below the title bar and a single panel-owned `ScrollComponent` controls the scroll offset.

## Goals

- Prevent properties content from rendering beyond the visible bounds of the `PropertiesPanel`.
- Make the entire panel body scroll as one continuous document.
- Keep one consistent scrolling behavior for both asset-property mode and entity/component mode.
- Reuse existing editor scroll patterns instead of introducing a panel-specific scrolling model.

## Non-Goals

- Do not split the panel into independently scrolling subsections.
- Do not move modal dialogs into the scroll tree.
- Do not redesign the existing property layout hierarchy beyond what is needed to support scrolling.
- Do not introduce a clipping-only fix that still leaves lower controls inaccessible.

## Current Structure

`PropertiesPanel` is a `DockableEntity` that owns one `contentRoot` positioned below the title bar. The panel then attaches:

- metadata text rows for asset selection,
- `AssetImportSettingsView`,
- `MaterialAssetView`,
- transform editing controls,
- `ComponentPropertiesView`,
- the add-component button, and
- externally hosted modals through `ModalHost`.

The panel updates child positions during its layout/update flow, but there is no scroll owner and no content-height accounting. As soon as the computed content height exceeds the dock body height, content continues below the visible panel.

## Recommended Approach

Add one `ScrollComponent` to the shared `contentRoot` and treat the entire properties body as one vertically scrolling document.

This is the smallest change that satisfies the user-facing requirement. It keeps the existing single-column layout model, avoids branching by selection mode, and matches scroll behavior already used elsewhere in the editor.

## Scrolling Model

### Single Scroll Owner

`PropertiesPanel` will own one `ScrollComponent` attached to the shared content host used for all panel body content.

Responsibilities:

- track the total scrollable content height,
- track the visible panel-body height,
- clamp the scroll offset when the visible area or content size changes,
- notify the panel when the offset changes so layout can be reapplied.

### Viewport

The scroll viewport is the full dock body below `DockableEntity.TitleBarHeightPixels`.

The title bar remains fixed. Only the content body scrolls.

### Content Offset

The panel keeps computing its normal vertical layout for all child sections, but applies the current scroll offset to the content body positioning so visible children move upward as the user scrolls downward.

The layout remains document-based:

1. Compute the natural Y positions of all body sections.
2. Compute the total content height.
3. Update the `ScrollComponent` with the visible viewport size and content size.
4. Apply the current scroll offset when positioning the content root or child sections.

### Reset Rules

The scroll offset resets to zero when the panel context changes meaningfully, including:

- clearing the current selection,
- showing properties for a different entity,
- showing properties for a different asset entry,
- switching between asset-property mode and entity-property mode.

The offset does not reset for ordinary layout refreshes of the same selection.

## Layout Changes

### Content Root

The existing `contentRoot` stays the shared parent for the panel body. The key change is that it now owns a `ScrollComponent` and participates in scroll-aware positioning.

### Content Height Accounting

The panel must compute one final document height after laying out whichever sections are currently visible.

That height includes:

- top margin and content padding,
- visible metadata text lines,
- import settings or material settings views when applicable,
- transform rows when applicable,
- add-component button spacing,
- component section heights from `ComponentPropertiesView`,
- bottom padding needed to keep the last visible control reachable without rendering outside the panel.

### Width and Height Updates

Whenever the panel size changes, the scroll viewport height is recomputed from the dock body height. Whenever visible content changes, the content height is recomputed and the scroll offset is clamped.

The panel must preserve existing width propagation into nested views such as `AssetImportSettingsView`, `MaterialAssetView`, and `ComponentPropertiesView`.

## Nested Views

`ComponentPropertiesView`, `AssetImportSettingsView`, and `MaterialAssetView` continue to render inside the panel body as they do today. They do not gain independent scroll owners.

The panel remains responsible for:

- assigning their layout width,
- including their rendered height in total document height,
- positioning them relative to the current document flow.

This keeps scrolling behavior simple and avoids conflicting wheel ownership.

## Input Behavior

Mouse-wheel scrolling over the visible panel body should move the panel document when there is overflow.

The scroll owner should only respond within the panel body bounds, following the existing `ScrollComponent` pattern used elsewhere in the editor.

Modal dialogs hosted under `ModalHost` remain outside the dock body scroll tree and keep their current input-capture behavior.

## Testing Strategy

Add regression coverage to `PropertiesPanel` tests with a focus on scroll ownership and overflow behavior.

Required coverage:

1. `PropertiesPanel` creates and owns one `ScrollComponent`.
2. Enough visible panel content produces a positive scroll range instead of overflowing silently.
3. Scrolling changes the visible content position so lower component sections move into the viewport.
4. Switching selection contexts resets the scroll offset to zero.
5. Existing mutation and component-shell behavior continues to work with the new shared scroll owner in place.

Because the current repository has unrelated build failures, the tests should still be written first and verified as soon as the workspace compiles again.

## Risks

### Height Calculation Drift

If the panel computes total content height differently from how it positions nested views, the scroll range can be too short or too long.

Mitigation:

- derive content height from the same layout pass that assigns child positions,
- keep one clear final "document bottom" calculation instead of duplicating per-section rules.

### Selection Change Edge Cases

If the scroll offset is not reset when the panel shows a different selection, the user can land mid-document on unrelated content.

Mitigation:

- reset scroll state at all selection-mode transition entry points,
- clamp again after every content rebuild.

### Nested Modal Interaction

If scrolling logic accidentally reaches into `ModalHost`, modal dialogs could move with the panel or lose input behavior.

Mitigation:

- keep all modal entities outside the scroll tree,
- limit the change to the panel body content root and its direct layout flow.

## Implementation Direction

Implementation should follow the existing scroll patterns already used by:

- `SceneHierarchyPanel`,
- `ComponentAddDialog`, and
- the scrollable sections inside `BuildDialog`.

The panel should adopt the same `ScrollComponent` lifecycle pattern:

- create the component during construction,
- set its update order,
- subscribe to scroll-offset change notifications,
- update viewport/content sizing during layout,
- clamp/reset offset when context changes.

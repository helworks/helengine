# Properties Panel Scroll Viewport Design

## Summary

Convert the `PropertiesPanel` body into a real scroll viewport so property content can extend past the visible panel height without rendering or receiving input outside the panel body.

The fix should use the engine's existing bounded viewport approach instead of leaving property sections directly attached to the dock shell. The panel body should scroll as one continuous document while modal dialogs launched from the panel remain outside the clipped area.

## Goals

- Add vertical scrolling to the `PropertiesPanel` body.
- Prevent property content from rendering outside the panel body.
- Prevent clipped-off property content from being interactable until it is scrolled into view.
- Preserve the current stacked document layout for transform controls, add-component controls, component properties, import settings, and material settings.
- Keep panel-owned modal dialogs outside the clipped viewport.

## Non-Goals

- Do not virtualize individual property rows or component sections.
- Do not redesign the `PropertiesPanel` content model or rewrite `ComponentPropertiesView`.
- Do not clip or scroll the shared modal host.
- Do not introduce theme or camera behavior changes unrelated to panel clipping.

## Current Context

`PropertiesPanel` currently attaches its full property body under `contentRoot`, which is positioned directly below the dock title bar. The body is laid out as one long stacked column, but there is no `ScrollComponent` and no bounded viewport or clipped camera for the panel body. As a result:

- long property content extends beyond the panel body
- content can render outside the visible panel area
- off-panel content can remain reachable by pointer interaction

Other editor surfaces already use bounded scrolling patterns:

- `BuildDialog` scene list and queue sections use `ScrollComponent`
- `SceneHierarchyPanel` uses a dedicated content camera and viewport for clipped panel-owned content
- `ComponentAddDialog` uses a scroll component for its long list body

The `PropertiesPanel` should adopt the same underlying model for clipped panel content rather than trying to fake clipping by selectively disabling children.

## Architecture

### Panel Body Viewport

Add one dedicated properties-body viewport that occupies the panel interior below the dock title bar.

That viewport becomes the only visible region for:

- metadata text lines
- import settings content
- material settings content
- transform controls
- add-component button
- component property content

The viewport should move and resize with the panel exactly like the rest of the dock shell.

### Scroll Ownership

Add one `ScrollComponent` owned by `PropertiesPanel`.

The scroll component should:

- use the properties-body viewport bounds
- drive one vertical scroll offset for the whole document
- clamp to the current content height
- only respond to wheel input while the pointer is inside the properties-body viewport

This keeps the panel behaving like one continuous document instead of several independently scrolling sections.

### Content Root Placement

Keep the existing `contentRoot` concept, but move it behind the new clipped viewport contract.

`contentRoot` should become the scrollable document root inside the viewport rather than a direct free-floating dock child. Existing layout methods can continue computing top offsets in document space. The scroll system should then translate the full body by the active scroll offset.

This preserves current layout logic and reduces the amount of code churn inside:

- metadata line layout
- transform layout
- add-component button layout
- component view layout
- import/material subview layout

### Modal Separation

`AddComponentDialog`, `RemoveComponentDialog`, and any other panel-owned modal UI must remain on `ModalHost`.

They must not be reparented into the new scroll viewport or clipped content root. The modal host remains screen-wide and independent from the dock panel body.

## Data Flow And Layout

### Content Height

`PropertiesPanel` already computes section positions through `LayoutLines()`, `UpdateTransformLayout(...)`, `LayoutAddComponentButton(...)`, and nested view layout calls.

This change should formalize one total document height calculation after layout so the `ScrollComponent` can derive:

- visible viewport height
- total content height
- visible row/body size for clamping and wheel scrolling

The panel should treat the body as one vertical document measured in pixels.

### Applying Scroll Offset

After document layout is computed in local content coordinates, apply the active scroll offset by moving the body root upward inside the clipped viewport.

That means:

- section layout remains document-relative
- scrolling is expressed as one translation on the document root
- no child section should need its own scroll math

### Panel Resizing

When the panel size or UI metrics change:

- recompute the viewport bounds
- recompute document height
- update `ScrollComponent` range
- clamp any prior scroll offset
- reapply the translated content root position

This should happen through the existing panel layout/update flow rather than a separate ad hoc pass.

## Rendering And Input

### Rendering Contract

The properties body should render only inside the viewport bounds. The dock shell and title bar remain outside that clipping path.

The implementation should use the engine's existing clipped panel-content pattern so the body is bounded by viewport state, not by assumptions about child positions.

### Input Contract

Pointer wheel scrolling should only apply when the pointer is inside the viewport.

Pointer hit resolution for property controls should also respect the viewport bounds. Controls that lie below or above the clipped region must not remain interactable until scrolling brings them into view.

## Testing

### Properties Panel Regressions

Add focused tests in a properties-panel suite to cover:

- long properties content produces a positive `MaximumScrollOffset`
- the panel body uses a dedicated clipped viewport/camera path instead of rendering directly through the dock shell
- content below the visible viewport is not hit or interactable before scrolling
- scrolling the viewport exposes later content and updates the body offset correctly

### Session-Level Safety

Run neighboring editor regressions that touch dock layout and metrics to confirm the new panel viewport does not break:

- properties panel metric application
- modal host layout
- dock input blocking behavior

## Implementation Constraints

- Reuse the engine's existing scroll and clipped panel-content patterns rather than inventing a new clipping mechanism.
- Keep the change localized to `PropertiesPanel` and directly related tests unless a shared helper is clearly justified by the existing codebase.
- Preserve the current body layout order and editing behavior.
- Keep modal dialogs out of the clipped body tree.

## Rollout Notes

This is a structural panel-body fix, not a content redesign. The intended result is that `PropertiesPanel` behaves like a normal scrollable document area: content can be taller than the panel, wheel scrolling works inside the body, and no property controls draw or receive input outside the visible panel viewport.

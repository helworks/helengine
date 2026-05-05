# Scene Hierarchy Scroll Viewport Design

## Goal

The Scene Hierarchy panel must own a real scroll viewport boundary. Hierarchy content that extends past the panel body must not render outside the panel and must not rely on sibling dock render ordering to appear hidden.

This change is intentionally scoped to the Scene Hierarchy first. It does not introduce a global reusable scroll-view control yet.

## Problem

Today the Scene Hierarchy panel lays out row entities under a content root inside the dock body, but the UI stack does not provide a generic 2D clip container. When the hierarchy grows beyond the visible panel body, rows can still draw outside the intended content region unless they are manually culled or covered by sibling panels. That is the wrong abstraction. The panel needs a local viewport boundary that defines what part of the hierarchy is visible.

## Chosen Approach

Add a Scene-Hierarchy-local scroll viewport implementation with these pieces:

1. A visible content rectangle directly below the dock title bar.
2. A scroll-content root that owns all hierarchy row entities.
3. A dedicated hierarchy content camera that renders only the content rectangle viewport.
4. A dedicated layer mask for hierarchy row visuals so only the hierarchy content camera renders them.
5. Scroll offset and hit testing that both clamp against the same visible content rectangle.

This creates a real rendering boundary for the panel without requiring a new global scroll-view framework.

## Why This Approach

### Option 1: Local viewport camera for Scene Hierarchy

This is the recommended option.

Benefits:
- Solves the real rendering problem instead of masking it.
- Keeps the change scoped to one panel.
- Uses existing engine primitives that already exist: camera viewport, layer masks, and panel-local entities.
- Creates a concrete pattern that can later be generalized if it proves sound.

Tradeoffs:
- Adds one extra UI camera and one new layer-mask split for this panel.
- Requires the panel to manage viewport/input synchronization explicitly.

### Option 2: Row culling only

Benefits:
- Smaller code change.
- Reuses the current row pool without extra rendering primitives.

Tradeoffs:
- Not true clipping.
- Future child visuals can still leak if they are not manually culled.
- Couples correctness to each row layout path.

### Option 3: Dock sibling render-order masking

Benefits:
- Very cheap to wire.

Tradeoffs:
- Does not create a boundary.
- Breaks as soon as panel arrangement changes.
- Solves symptoms, not the panel’s rendering model.

## Architecture

### SceneHierarchyPanel responsibilities

`SceneHierarchyPanel` will remain responsible for:
- Building the flattened visible node list.
- Managing expanded state.
- Managing scroll offset and visible-row layout.
- Updating the hierarchy content camera viewport to match the panel body rectangle.
- Routing pointer and keyboard interaction only inside the visible hierarchy viewport.

### New panel-local rendering structure

The panel will be split into:
- Dock chrome owned by the existing `DockableEntity` path.
- Hierarchy viewport host owned by `SceneHierarchyPanel`.
- Scroll content root beneath that viewport host.
- Row entities parented under the scroll content root.

Rows will render on a hierarchy-specific UI layer mask instead of the main editor UI layer mask. A dedicated hierarchy content camera will target only that layer and only the Scene Hierarchy content rectangle.

### Camera and layer-mask model

The hierarchy content camera will:
- Use an editor-UI-only camera path.
- Use a dedicated layer mask such as `EditorLayerMasks.SceneHierarchyContent`.
- Set its viewport every layout pass to the exact panel body rectangle below the title bar.

Hierarchy rows and any future child visuals inside the scene hierarchy body will use that same layer mask.

Dock chrome, title bars, separators, and overlays will stay on the existing editor UI layer.

## Layout and Scrolling

The panel body rectangle is the only visible hierarchy region.

Row layout rules:
- The scroll content root origin represents the top of the logical hierarchy list.
- Row entities are positioned relative to that scroll origin.
- Scroll offset shifts the content root instead of relying only on row enable/disable state.
- Row pooling may still keep a visible-slice optimization, but clipping correctness must come from the viewport boundary.

The scroll component will remain item-based. It will clamp based on:
- Total node count.
- Visible row count computed from the panel body height.

## Input Model

Pointer behavior must match what is visible:
- Hover, click, and wheel scroll must only activate when the pointer is inside the hierarchy viewport rectangle.
- Row hit testing must use viewport-relative coordinates rather than unconstrained world-space content.
- Context-menu activation must require the pointer to be inside the viewport and on a visible row.

Keyboard behavior remains unchanged except for one guarantee:
- Focus movement must auto-scroll the focused row into the visible viewport if needed.

## Error Handling and Constraints

- If the content camera cannot be created, panel initialization should fail loudly rather than silently falling back to broken overflow behavior.
- The viewport rectangle must clamp to non-negative width and height.
- The panel must handle very small dock sizes by showing zero or one visible row region without allowing out-of-bounds rendering.

## Testing

### Required regressions

1. Scene hierarchy content outside the panel body is not rendered by the main editor UI camera.
2. Scene hierarchy rows remain interactable only when their visible portion is inside the viewport.
3. Focus navigation auto-scrolls hidden rows into view.
4. Resizing the dock updates the hierarchy camera viewport and visible row count consistently.
5. The properties panel no longer needs sibling render-order tricks to cover hierarchy overflow.

### Test strategy

Initial tests will stay scoped to panel behavior:
- Add tests around the hierarchy viewport rectangle and row-layer assignment.
- Add tests that verify off-viewport rows are either clipped or not discoverable through pointer hit testing.
- Keep the existing right-side dock layout regression, but update its expectation so correctness comes from the hierarchy viewport path rather than dock overdraw.

## Out of Scope

- A generic reusable `ScrollViewComponent`.
- A global clip-container abstraction for all 2D UI.
- Migration of logger, asset browser, properties, or dialogs to the same viewport pattern.

Those can come later if this Scene Hierarchy implementation proves clean and stable.

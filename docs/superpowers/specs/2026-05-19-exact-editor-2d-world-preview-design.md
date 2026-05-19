# Exact Editor 2D World Preview Design

## Summary

The editor scene view already has the first half of the new 2D-in-3D model:

- `SpriteComponent` can render as a world-space proxy.
- internal editor UI stays on the old screen-space path.
- authored 2D content can be selected through editor-owned proxy entities.

What is still missing is exact world-space preview for the remaining authored 2D components that matter in normal scenes:

- `TextComponent`
- `RoundedRectComponent`

The previous proxy experiment treated those components like simple textured sprites. That is the wrong abstraction. `TextComponent` is not just a font atlas sample, and `RoundedRectComponent` is not just a white quad. The result was incorrect black or garbage-looking output and an unreliable scene-view model.

This design adds **editor-only exact per-component preview rendering** for those components, while preserving true 3D placement, authored transform, and per-entity depth.

## Goals

- Render authored `TextComponent` and `RoundedRectComponent` in the editor scene view as exact world-space previews.
- Preserve each component's real scene transform and Z ordering.
- Keep the existing editor rule that internal editor UI stays screen-space and does not leak into the world preview.
- Keep selection behavior intuitive:
  - clicking a world-space preview selects the authored 2D source entity
  - 2D selection still wins over 3D fallback selection
- Keep the implementation editor-only.

## Non-Goals

- No `helengine.core` changes.
- No runtime/game rendering behavior changes.
- No approximation shaders for text or rounded rectangles.
- No batching of many 2D components into one shared texture atlas or one shared viewport capture.

## Constraints

- The user explicitly wants Unity-like scene-view behavior: authored 2D content appears in the 3D world at its actual transform.
- A shared baked viewport texture is not acceptable because authored 2D components still have meaningful relative Z and independent scene placement.
- The scene view must remain robust. The refresh strategy should not rely on continuous full-frame rebuilding when component data has not changed.

## Approaches Considered

### 1. Approximate world preview using simple textured quads

This is what the first proxy pass effectively did for text and rounded rectangles.

Pros:

- Low implementation cost
- Reuses the sprite preview material path

Cons:

- Incorrect for text
- Incorrect for rounded rectangles
- Produced visibly broken output
- Violates the requirement for trustworthy scene-view editing

Rejected.

### 2. Shared viewport or subtree render-to-texture

Render a whole 2D viewport or subtree to one texture and project it in 3D.

Pros:

- Can be visually exact for that captured subtree
- Fewer runtime render targets than per-component capture

Cons:

- Breaks the desired per-component depth model
- Makes independent authored Z and overlap behavior ambiguous
- Complicates picking and proxy-to-source mapping

Rejected.

### 3. Exact per-component editor render targets with dirty-driven refresh

Each authored `TextComponent` or `RoundedRectComponent` receives its own editor-only preview texture, displayed on a world-space quad at the authored transform.

Pros:

- Exact visual result
- Preserves per-component transform and depth
- Matches the existing sprite world-preview architecture better
- Dirty-driven refresh can keep the cost bounded

Cons:

- More editor-side machinery
- Requires explicit preview resource ownership and refresh rules

Recommended.

## High-Level Design

### World-space preview model

The editor world-preview system will support three authored 2D component categories:

- `SpriteComponent`
  - continues using the current direct world-space textured-quad path
- `TextComponent`
  - uses an exact editor-only preview texture
- `RoundedRectComponent`
  - uses an exact editor-only preview texture

All three categories resolve back to the same authored source entity for selection.

### Editor-only preview resource model

Each exact preview component owns editor-only preview resources:

- one runtime render target texture
- one minimal editor preview capture path for the source 2D component
- one world-space quad preview entity/component that displays the captured texture

These resources are internal editor infrastructure and must never appear in authored scene data, runtime builds, or normal scene selection.

### Refresh model

Exact preview textures refresh on demand, not every frame.

The editor preview component marks itself dirty when:

- the source component's visible data changes
- the effective preview size changes
- the component is first created
- the preview resource is invalidated or recreated

For `TextComponent`, dirty inputs include:

- text string
- font
- font size
- text color
- size
- alignment
- padding
- any other property that affects the final rendered glyph output

For `RoundedRectComponent`, dirty inputs include:

- size
- fill color
- border color
- border thickness
- radius and corner settings
- any other property that affects the final rendered shape

Transform-only changes do **not** require recapturing the preview texture. They only require updating the world-space proxy transform.

### Transform model

The preview quad uses the same positive-XY corner-origin contract already established for the sprite preview fix and the viewport border gizmo:

- local origin at `(0, 0, 0)`
- local rectangle extends into positive `X` and positive `Y`
- local scale is `(width, height, 1)`
- local orientation matches the authored source entity orientation directly

This keeps the world-preview contract consistent across:

- sprite previews
- exact text previews
- exact rounded-rectangle previews
- authored viewport border gizmos

## Component and Service Boundaries

### `EditorWorldSpace2DPreviewMapper`

Responsibility:

- identify which authored 2D components are world-preview supported
- resolve the correct preview source type
- preserve the existing editor/internal filtering rules

Change:

- expand support from sprite-only to:
  - `SpriteComponent`
  - `TextComponent`
  - `RoundedRectComponent`

### `EditorWorldSpace2DPreviewSyncComponent`

Responsibility:

- create, update, and remove preview proxies
- maintain source-to-preview registry mapping

Change:

- continue owning proxy lifecycle
- instantiate the correct exact-preview component type for text and rounded rectangles

### `EditorSpriteWorldPreviewComponent`

Responsibility:

- unchanged in architecture
- remains the direct textured-quad path

### `EditorTextWorldPreviewComponent`

Responsibility:

- own exact preview capture resources for one authored `TextComponent`
- determine when the preview texture is dirty
- rebuild the texture only when required
- synchronize the world-space quad transform separately from texture capture

### `EditorRoundedRectWorldPreviewComponent`

Responsibility:

- own exact preview capture resources for one authored `RoundedRectComponent`
- determine when the preview texture is dirty
- rebuild the texture only when required
- synchronize the world-space quad transform separately from texture capture

### Exact preview capture service(s)

Responsibility:

- create and release editor-only preview render targets
- render one text or rounded-rectangle component into its preview texture
- keep the capture path isolated from authored scene/runtime rendering

The exact class split can remain flexible during implementation, but the responsibilities must stay separated:

- resource ownership
- dirty-state evaluation
- texture capture execution
- world-space proxy display

## Rendering Contract

- Authored `SpriteComponent`, `TextComponent`, and `RoundedRectComponent` that pass the editor filter render in the 3D scene view through world-space proxies.
- Internal editor viewport infrastructure remains on the screen-space path.
- Unsupported authored 2D component types, if any remain later, stay on the existing 2D path until they get a dedicated world-preview implementation.
- The scene camera 2D queue filtering must continue removing authored 2D drawables that already have a world-space preview path, so they do not render twice.

## Selection Contract

- Clicking a world-space preview proxy selects the authored source entity.
- 2D preview selection still resolves before 3D fallback selection.
- Internal editor preview entities remain non-selectable directly.

## Error Handling

- Missing required preview resources should fail loudly in editor code instead of silently falling back to broken output.
- If a source component has invalid required input, the preview should disable cleanly for that component instance rather than emitting a malformed proxy.
- Preview resource disposal must be explicit and deterministic when the source entity disappears or the sync component is removed.

## Testing Strategy

Add focused editor tests that prove:

1. `TextComponent` now creates a world-space preview proxy.
2. `RoundedRectComponent` now creates a world-space preview proxy.
3. Exact preview proxies use the positive-XY corner-origin mesh contract.
4. Transform-only changes do not require texture recapture.
5. Visible-property changes do require texture recapture.
6. Scene-camera 2D queue filtering removes authored text/rounded-rect drawables that now have world-space previews.
7. Internal editor UI descendants still do not create world-space preview proxies.
8. Proxy selection still resolves back to the authored source entity.

## Risks

### Preview churn

If dirty detection is too broad, the editor will rebuild preview textures too often and become noisy or expensive.

Mitigation:

- keep dirty-state tracking explicit
- separate texture-affecting changes from transform-only changes

### Resource leaks

Per-component preview render targets create lifetime pressure.

Mitigation:

- explicit ownership by the preview component
- deterministic release during proxy removal/component removal
- focused tests around resource teardown where feasible

### Incorrect visual parity

If the exact preview capture path does not use the same rendering rules as the editor/runtime 2D path, the preview could still drift visually.

Mitigation:

- prefer reusing existing 2D rendering logic instead of inventing a parallel approximation path
- verify text and rounded-rectangle output with targeted tests and manual scene-view validation

## Success Criteria

This work is complete when:

- authored text renders in 3D scene view at the correct transform
- authored rounded rectangles render in 3D scene view at the correct transform
- the previews are visually exact enough to trust for editing
- authored 2D components no longer disappear or render as garbage when moved to the world-preview path
- internal editor UI still stays out of the world preview
- no `helengine.core` changes were required

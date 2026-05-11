# Demo Menu Reference Canvas Fit Scaling Design

## Summary

Keep the demo-disc main menu authored against a `1280x720` reference canvas, but make runtime treat that canvas as reference content that is uniformly fit into the live window.

At `640x480`, the menu should:

- render through the real window-sized camera viewport
- scale the authored `1280x720` menu shell down uniformly
- reposition the scaled menu inside the live `4:3` bounds through anchors
- preserve the authored scene as the single source of truth

This extends the earlier responsive-anchor design with one additional contract: menu element sizes must scale down with the window instead of staying at authored 720p dimensions.

## Problem

The current player behavior has split responsibilities:

- the OS window is now correctly `640x480`
- the menu scene camera is still authored as a fixed `1280x720` viewport
- the generated menu shell is authored with fixed 720p positions and sizes
- the current anchor usage is too limited to produce the intended `480p 4:3` presentation by itself

That means the menu still behaves like a 720p UI scene rendered inside a smaller player window, instead of adapting from the 720p reference into the live display shape.

## Goals

- Keep `1280x720` as the authored demo-menu reference canvas.
- Bind the runtime menu camera viewport to the live window size.
- Uniformly scale menu shell positions and sizes from the authored reference canvas into the live window.
- Preserve anchor-driven placement so the scaled layout sits correctly inside `4:3` and `16:9` bounds.
- Keep menu navigation, selection, and scroll behavior unchanged.
- Keep the solution generic enough for other authored reference-canvas UI scenes.

## Non-Goals

- No second authored `480p` menu scene.
- No player-only hack that special-cases `640x480`.
- No requirement that the whole engine adopt a new global UI layout system in this change.
- No change to scene authoring reference dimensions.

## Proposed Runtime Contract

### 1. Reference Canvas

The menu scene remains authored in `1280x720`.

That reference canvas is content authoring space only. It is not the runtime viewport size.

### 2. Live Viewport

The scene camera must follow the actual player window size.

For a `640x480` player window, the menu camera viewport becomes `640x480`, not `1280x720`.

### 3. Uniform Fit Scale

Runtime resolves one uniform scale factor from:

- reference canvas size: `1280x720`
- live window size: for example `640x480`

The scale factor is:

- `min(liveWidth / referenceWidth, liveHeight / referenceHeight)`

For `640x480`, that yields:

- `min(640 / 1280, 480 / 720) = min(0.5, 0.666...) = 0.5`

So menu shell sizes and positions scale by `0.5`.

### 4. Anchor-Aware Placement

After scaling, the menu shell still needs to be positioned inside the live window bounds.

Anchors remain responsible for that placement step:

- top-left pinned elements stay pinned after scale
- edge-aligned regions continue to resolve against the live window
- the scaled layout can shift for `4:3` without needing a second authored scene

This keeps anchors as the placement system and the fit scale as the size conversion system.

### 5. Generic Menu/UI Surface

The scaling rule should not be city-specific.

The generic contract should be:

- authored UI scenes may define a reference canvas
- runtime may bind a camera to the live window
- runtime may resolve a uniform fit transform from authored reference space to live viewport space

The city demo menu is just the current consumer of that contract.

## Architecture

### Runtime Camera Binding

The menu camera viewport should no longer be a fixed baked `1280x720` rectangle at runtime.

It should resolve from the live render window dimensions.

### Reference Canvas Layout Transform

Add one shared runtime layout transform that converts authored reference-space UI coordinates into live viewport-space UI coordinates.

That transform must apply to:

- root menu shell positions
- shell visual sizes
- item row sizes
- scroll viewport sizes
- text container sizes where the authored scene expects menu-shell scaling

### Menu Generator Contract

The generated menu scene should continue to emit authored reference-canvas dimensions and authored layout numbers.

It should not pre-bake `640x480` or any other target size.

### Anchor Composition

Anchors should compose with the reference-canvas fit transform instead of competing with it.

That means:

- authored distances are still defined in reference-canvas units
- runtime scales those distances
- anchor resolution then places the scaled element inside the live bounds

## Testing

- runtime menu camera viewport follows the live player window instead of fixed `1280x720`
- `640x480` runtime produces a `0.5` reference-canvas fit scale from `1280x720`
- generated menu shell entities shrink in size at `640x480`
- anchored menu placement still remains correct after scaling
- menu selection and scroll behavior remain stable after the layout transform is applied
- editor/runtime loading of the authored scene still round-trips without rewriting the authored reference canvas

## Success Criteria

The change is complete when:

- the player window is `640x480`
- the menu camera viewport is also `640x480`
- the menu shell no longer renders at authored 720p size
- the main menu visibly scales down from the `1280x720` authored reference
- anchors still control final placement within the live `4:3` window
- the implementation is generic enough to reuse for other reference-canvas UI scenes

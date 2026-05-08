# Demo Menu Responsive Anchor Layout Design

## Summary

Keep the demo-disc main menu authored against a `1280x720` reference canvas, but make the runtime scene bind to the actual window resolution and use the engine's anchor system for positioning.

The menu should no longer assume a fixed 720p viewport at runtime. Instead, it should:

- author the scene in 720p coordinates
- bind the menu viewport to the current resolution
- use anchors for layout-critical entities
- allow the composition to differ slightly across `4:3`, `16:9`, and `480p` without letterboxing
- keep the editor able to preview, save, and rebuild that authored scene correctly

## Problem

The current generated demo menu is effectively a fixed-resolution layout:

- the baked camera viewport uses `1280x720`
- background and panel positions are authored as absolute pixels
- menu item rows are positioned with hard-coded offsets
- the current composition does not rely on the engine's anchor system

That works for a single authored canvas, but it does not satisfy runtime behavior across different display sizes.

The target behavior is different:

- the authored scene remains 720p
- runtime should bind to the active window size
- the menu should adapt its structure through anchors and layout recomputation
- the scene should remain usable at `480p`, `4:3`, and `16:9`
- no letterboxing should be introduced as the primary solution

## Goals

- Keep `1280x720` as the authored menu baseline.
- Bind the runtime menu viewport to the current resolution instead of a fixed baked size.
- Use engine anchors for menu placement and edge alignment.
- Let the menu reflow slightly when the aspect ratio changes.
- Keep the selected-item description and panel navigation behavior intact.
- Make the editor aware of this layout model so authored scenes preview and deserialize correctly.
- Preserve existing menu generation and regeneration flows where possible.

## Non-Goals

- No letterbox or pillarbox-only solution.
- No per-resolution duplicate authored scenes.
- No one-off runtime hacks that manually correct specific screen sizes.
- No rewrite of menu navigation logic unless required by layout binding.
- No attempt to turn every menu element into an anchor if a smaller responsive layout helper is enough.

## Current Behavior

The current menu generator authors a 720p scene directly:

- camera viewport is written as `1280x720`
- background is authored at `1280x720`
- panels and item rows are placed with fixed coordinates
- item list scrolling is driven by fixed row dimensions

The engine already has anchor support:

- `AnchorComponent` resolves against a parent bounds provider
- `IAnchorBoundsProvider` exposes the available layout space
- UI dialogs already use this pattern for responsive shells

However, the demo menu scene does not currently use these capabilities.

## Proposed Architecture

### 1. Authored Canvas Remains 720p

The generated menu scene stays authored in 720p space.

This means:

- authored positions continue to use `1280x720` as the design reference
- scene content and generator math remain stable for editing and review
- the authored data stays familiar to content creators

The authored canvas is a reference frame, not the runtime viewport contract.

### 2. Runtime Viewport Binds to Current Resolution

The scene camera for the menu should use the current runtime resolution.

That means:

- the viewport is derived from the active window size
- the menu renders into the actual available display area
- resize events propagate through the same layout path used by other anchor-driven UI

This is the key change that allows the same authored scene to run at `480p`, `4:3`, or `16:9`.

### 3. Menu Root Becomes a Layout Host

Introduce a menu layout host entity that exposes the viewport bounds to anchored children.

The host is responsible for:

- providing the anchor bounds for the generated menu tree
- notifying children when the viewport changes
- acting as the stable parent for anchor-driven positioning

The generated menu tree should attach anchors relative to this host instead of computing every position from fixed screen coordinates.

### 4. Anchors Replace Fixed Positioning For Shell Elements

Use anchors for the menu shell and major regions:

- title block
- main panel container
- panel headings
- selected-description region
- bottom or side spacing that must track the viewport edge

For the item list, the anchor system should pin the list container to its logical region, then the scroll math can continue to manage item offsets inside that region.

This keeps the shell responsive without forcing the item renderer to become a separate layout system.

### 5. Runtime Layout Recomputes For Size-Sensitive Elements

Anchors solve position and edge pinning, but some elements still need explicit size recomputation.

Examples:

- list viewport height when the active panel changes
- text surfaces that should expand or contract with available bounds
- any container that needs to remain readable at `480p`

This should be done as a layout recomputation pass, not as ad hoc post-fixup code.

## Data Flow

1. The editor generator authors the demo menu scene in 720p reference coordinates.
2. The runtime scene loads the menu and binds the camera viewport to the current window resolution.
3. The menu layout host exposes the current viewport as anchor bounds.
4. Child entities with `AnchorComponent` resolve their positions from that layout host.
5. On window resize, the bounds provider notifies the anchored children.
6. The menu recomputes its responsive shell and keeps the active selection state intact.

## Editor Behavior

The editor should understand the scene in the same terms as runtime:

- the authored scene should still open and save as a 720p design
- the editor preview should use the live viewport contract rather than assuming a fixed baked size
- scene loading and regeneration should preserve anchor metadata
- the generated scene should round-trip through existing scene serialization without dropping the anchor-driven layout intent

The editor does not need a special menu-only layout language. It needs the existing anchor system to be applied consistently in the generated menu scene and preview path.

## Failure Behavior

The implementation should fail clearly when the layout contract cannot be resolved.

Required failures:

- missing menu layout host when the generated menu expects one
- invalid or zero viewport dimensions
- invalid anchor bounds provider state
- unsupported generated layout configuration that cannot be serialized or loaded

Do not add fallback behavior that silently reconstructs a default layout host or silently reverts to a fixed 720p viewport.

## Testing

Add focused coverage around the new behavior.

### Generator And Scene Tests

- generated menu scenes still author against `1280x720`
- generated menu scenes include anchor metadata for layout-critical regions
- generated menu scenes no longer depend on a fixed baked runtime viewport
- scene serialization round-trips the anchor-driven menu tree

### Runtime Layout Tests

- the menu binds to the current window resolution
- anchored menu elements move correctly on viewport resize
- `480p` layouts still render the menu without corrupting selection state
- `4:3` and `16:9` layouts preserve the intended composition within the responsive rules

### Editor Tests

- the editor preview path honors the current viewport
- the generated menu scene can be loaded, saved, and regenerated with anchor metadata intact
- menu-specific preview or rebuild flows do not regress when the viewport size changes

## Implementation Notes

- Keep the authored baseline at `1280x720`; do not change the scene authoring reference just to make runtime adaptation easier.
- Prefer engine anchor semantics over menu-specific position correction helpers.
- Keep scroll behavior and selection behavior separate from shell layout behavior.
- If a container must stretch, make that explicit in the layout host or a related manager rather than spreading size math into many menu entities.
- Reuse the engine's existing anchor and bounds-provider patterns instead of inventing a parallel responsive system.

## Success Criteria

The change is complete when:

- the menu remains authored in 720p
- the menu runtime binds to the current resolution
- the scene uses anchors for responsive placement
- the same authored menu can run at `480p`, `4:3`, and `16:9`
- no letterboxing is required to make the menu fit
- the editor can load and preview the responsive authored scene correctly


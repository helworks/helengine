# Demo Menu Scene List Scroll And Clipping Design

## Goal

Make the generated Demo Disc menu scene list scroll and clip correctly inside its panel instead of drawing item rows outside the visible panel rectangle.

The scene list must support:

- row-based scrolling
- keyboard/controller selection-driven scrolling
- mouse-wheel scrolling
- clipping through the existing engine clip system

## Scope

This change applies to the generated demo menu scene and the generic menu runtime support it relies on.

It updates:

- the baked panel structure authored by `DemoMenuSceneAssetFactory`
- the generic menu runtime binding path in `MenuComponent`
- the use of the existing `ScrollComponent` for menu item lists

It does not introduce a new menu-specific scroll system.

## Recommended Approach

Use the existing `ScrollComponent` as the only scroll-state owner for menu item lists.

That means:

- no bespoke per-panel scroll math in `MenuComponent`
- no separate menu-only scroll component
- no clip-only fix that still strands off-screen rows

If `ScrollComponent` is missing behavior needed by the menu list, extend it generically so other UI lists can benefit from the same capability.

## Authored Scene Structure

Each baked menu panel should keep:

- panel surface
- top band
- heading
- selected-description text

Each baked menu panel should gain:

- `panel-<id>-items-viewport`
- `panel-<id>-items-root`

Behavior:

- `items-viewport` is fixed inside the panel and owns the clip rectangle
- `items-root` is a child of the viewport and owns the `ScrollComponent`
- baked item rows are children of `items-root`

This replaces the current direct parentage where item rows sit under the panel root with no dedicated clipped viewport.

## Scroll Model

Keep `ScrollComponent` row-based in this slice.

For each menu panel:

- `ItemCount` is the number of enabled menu items
- `VisibleItemCount` is `MenuPanelDefinition.VisibleItemCount`
- `ScrollOffset` is the first visible row index

The component must support:

- vertical row-based content offset
- wheel-driven row scrolling
- ensuring a target row is visible when selection changes

## Runtime Behavior

`MenuComponent` remains responsible for:

- selection changes
- action execution
- item visual state updates

`MenuComponent` should not own scroll state.

Instead it should:

- discover the panel `ScrollComponent` while binding the panel
- ask that component to keep the selected row visible when navigation changes selection
- route mouse-wheel input over the active panel viewport to the same `ScrollComponent`

## Clipping

Clipping should use the existing rectangular clip path:

- `ClipRectComponent` lives on the fixed `items-viewport`
- item rows render through the clipped viewport
- overflow rows are not visible outside the panel body

This must work for both static rendering and while scrolling.

## Testing

Coverage should prove both authored structure and runtime behavior:

- generated menu panels contain an item viewport host and scrolling root
- generated item rows are parented under the scrolling root instead of directly under the panel root
- generated viewport host owns the clip rectangle
- generated scrolling root owns a `ScrollComponent`
- selection movement scrolls the list when the selected row moves past the visible window
- mouse wheel scrolls the active panel list by rows
- overflow rows do not draw outside the panel viewport

## Success Criteria

After regenerating `DemoDiscMainMenu.helen`:

- the scene-select list does not render outside the panel bounds
- selection can move through more rows than the panel visibly fits
- the list auto-scrolls to keep the selected row visible
- mouse wheel scrolls the list
- scrolling uses `ScrollComponent`, not a menu-specific duplicate system

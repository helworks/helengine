# Demo Menu Generator Static Copy Removal Design

## Goal

Rework the generated Demo Disc main menu so each panel no longer includes:

- the static subtitle text under the panel heading, such as `Pick destination or peek at the menu shell`
- the decorative left vertical accent bar

The dynamic selected-item description at the bottom of each panel stays in place and continues to be driven by the runtime menu system.

## Scope

This change is generator-side only.

It updates:

- the demo menu scene asset factory that authors panel entities
- demo scene writer and generator regression coverage
- the regeneration flow that rewrites `DemoDiscMainMenu.helen`

It does not change:

- `MenuComponent` runtime selection logic
- `MenuSelectedDescriptionComponent`
- dynamic item-description behavior
- menu serialization formats beyond the resulting authored scene content

## Recommended Approach

Remove the unwanted elements at the panel factory level instead of hiding them through theme values or provider-specific data shaping.

That keeps regenerated scenes clean:

- no dead subtitle entity remains in the panel tree
- no unused decorative bar entity remains in the panel tree
- runtime behavior remains unchanged because the selected-description path is preserved

## Layout Changes

Each generated panel keeps the existing high-level structure:

- panel heading at the top
- menu item list in the body
- selected-item description at the bottom

With the static subtitle row removed, the item list moves upward to reclaim that vertical space.

With the decorative left bar removed, the item-list layout also reclaims that horizontal margin. The adjustment should be conservative and should not become a broader visual redesign in this pass.

The generator should not leave empty placeholder spacing for either removed element.

## Implementation Notes

The main change point is `DemoMenuSceneAssetFactory`.

The factory should:

- stop authoring the static panel-description text entity
- stop authoring the left accent rounded-rect entity
- update the remaining panel-child positions so the item list fills the recovered space cleanly
- continue authoring the selected-description entity exactly as before

The city-side menu definition data can keep existing description strings for now if removing them there would broaden the scope. The generator simply stops rendering the static panel description field in this scene composition path.

## Testing

Regression coverage should prove the generator output changed in the intended way:

- regenerated demo menu scenes no longer contain the static subtitle text entity
- regenerated demo menu scenes no longer contain the decorative left bar entity
- generated panels still contain the selected-description entity
- runtime menu-selection behavior remains covered by existing tests

## Success Criteria

After regenerating `DemoDiscMainMenu.helen`:

- no static subtitle is shown beneath panel headings
- no left vertical accent bar is present
- the dynamic bottom description still updates normally
- the panel body uses the reclaimed space without obvious empty gaps

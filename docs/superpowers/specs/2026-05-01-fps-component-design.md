# FPS Component Design

## Goal

Add a reusable runtime `FPSComponent` in `helengine.core` that displays two live lines in the top-left corner of the viewport:

- update FPS
- render FPS

The component must build its own runtime entity hierarchy when attached, so users can add one component and get the overlay without manually assembling text entities.

## Scope

In scope:

- a new core-runtime component
- runtime entity creation from `ComponentAdded`
- two text lines, one for update FPS and one for render FPS
- top-left placement using the existing 2D/text rendering path
- periodic refresh of the displayed text

Out of scope:

- editor-only behavior
- custom styling UI
- graphing, spark lines, or history views
- global engine bootstrap changes

## Design

`FPSComponent` will live in `helengine.core` as a normal component. When it is attached to an enabled entity, it will create a child entity that owns two `TextComponent` instances. The component will keep the text nodes updated with the current update and render frame rates.

The component should not render text itself. It should only own timing, sampling, and text content. `TextComponent` remains the actual renderer.

### Runtime hierarchy

- Root entity: holds `FPSComponent`
- Child entity: holds the text overlay components
- Text components:
  - first line: update FPS
  - second line: render FPS

The component should position the root/child hierarchy so the text appears in the top-left corner of the viewport using the existing UI layering and render order conventions.

### Sampling behavior

- Update FPS is sampled from the engine update loop.
- Render FPS is sampled from the render loop.
- The component stores counters and elapsed time, then refreshes the text at a fixed interval instead of every frame.

## API Shape

`FPSComponent` should expose:

- `UpdateFpsText`
- `RenderFpsText`
- a refresh interval setting

It should also provide methods that the engine can call from update and render paths to record frame activity. The component owns the displayed strings and pushes them into the child text components when the refresh interval elapses.

## Lifecycle

When the component is added:

- it creates the child entity
- it creates and configures the two text components
- it attaches them to the child entity
- it seeds the initial text

When the component is removed or the parent hierarchy is disabled:

- the child entity should stop participating in rendering through normal hierarchy rules
- no special cleanup path should be needed beyond the engine’s normal entity/component ownership model

## Error Handling

The component should fail fast if:

- it is attached to an invalid or missing parent
- required text resources cannot be created
- it is asked to sample FPS before initialization is complete

No silent fallback overlay should be created.

## Verification

Add focused tests for:

- component builds its child text entity at runtime
- update and render counters update the displayed strings
- the overlay uses the expected top-left render ordering
- removing or disabling the parent hierarchy removes the overlay from render participation

## Notes

This component is intentionally core-runtime, not editor-only. The same overlay is useful in the Windows player and during engine runtime debugging.

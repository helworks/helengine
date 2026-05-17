# Debug Component Design

## Goal

Add a separate runtime `DebugComponent` in `helengine.core` that displays a fixed five-line diagnostics overlay in the top-left corner of the viewport.

The overlay should focus on blunt, useful runtime data:

- render FPS
- resident memory
- committed memory
- active 2D drawable count
- active 3D drawable count together with draw calls

The component must build and own its own runtime text hierarchy so users can add one component and get the overlay without manually assembling supporting entities.

## Scope

In scope:

- a new core-runtime component
- runtime entity creation from `ComponentAdded`
- a fixed five-row overlay with deterministic formatting
- periodic refresh of the displayed values
- editor persistence, runtime deserialization, and scene packaging support
- a shared-core draw-call metric that `DebugComponent` can read without backend casts

Out of scope:

- changes to `FPSComponent`
- per-row toggles or customization UI
- backend-specific overlay formatting logic inside the component
- graphs, history, or expandable debug panels

## Design

`DebugComponent` will live beside `FPSComponent` as a separate `UpdateComponent` in `helengine.core`. It will follow the same broad lifecycle pattern:

- stay inert until attached and assigned a font
- create a small overlay entity hierarchy under the owning entity
- register itself in a static active-component list for shared render-frame sampling
- refresh its text rows on a configurable interval instead of every frame

This keeps the existing `FPSComponent` stable while providing a dedicated home for richer debug diagnostics.

### Runtime hierarchy

- Root entity: holds `DebugComponent`
- Overlay host child entity: positions the diagnostics block in viewport space
- Five row child entities: each hosts one `TextComponent`

The row order will be fixed:

1. `Render FPS: ...`
2. `Memory Res: ...`
3. `Memory Com: ...`
4. `Drawables 2D: ...`
5. `Drawables 3D: ... DrawCalls: ...`

The overlay should use the same general top-left placement and 2D render-order conventions already established by `FPSComponent`.

### Sampling behavior

- Render FPS is sampled from render-frame ticks using the same active-component broadcast pattern already used by `FPSComponent`.
- Memory values are refreshed from `RuntimeDiagnosticsService.CaptureSnapshot()`.
- Drawable counts come from the shared `ObjectManager` lists.
- Draw-call count comes from a core-owned metric updated during draw.

The component will not update visible strings every frame. It stores counters and refreshes them only when `RefreshIntervalSeconds` elapses. This keeps the overlay stable and avoids unnecessary text churn.

## API Shape

`DebugComponent` should remain intentionally minimal for v1.

Public properties:

- `Font`
- `Padding`
- `RenderOrder2D`
- `RefreshIntervalSeconds`

Public read-only text state may be exposed for tests in the same style as `FPSComponent`, with one string per visible row.

No per-row enable flags or style configuration should be added in this first version.

## Data Sources

`DebugComponent` must read only shared runtime/core state. It should not cast to `DirectX11Renderer3D` or any other concrete backend type.

### Render FPS

Render FPS should use a local frame counter plus elapsed core time, matching the sampling model used by `FPSComponent`.

### Memory

Memory should come from `Core.RuntimeDiagnosticsService.CaptureSnapshot()`.

The overlay should read:

- `ResidentBytes`
- `CommittedBytes`

If runtime diagnostics are unavailable, the rows still remain visible and show placeholders.

### Drawables

Drawable counts should come from `Core.ObjectManager`:

- `Drawables2D.Count`
- `Drawables3D.Count`

These values represent the active runtime drawable lists rather than trying to count what a specific backend submitted internally.

### Draw Calls

Draw-call count should be copied into `Core` during draw in the same spirit as existing core-owned timing and performance-overlay metrics.

That means:

- the renderer backend may calculate the count
- `Core` becomes the stable shared source of truth
- `DebugComponent` reads only from `Core`

This keeps the component backend-agnostic and leaves room for other renderers to publish the same metric later.

## Formatting

Formatting should stay simple, fixed, and deterministic.

Expected row shapes:

- `Render FPS: 60.0`
- `Memory Res: 128.4 MB`
- `Memory Com: 192.0 MB`
- `Drawables 2D: 14`
- `Drawables 3D: 8 DrawCalls: 23`

Rules:

- one decimal place for FPS values
- one decimal place for memory values expressed in megabytes
- integer formatting for drawable counts and draw calls
- fixed row presence even when data is unavailable

Placeholder examples are acceptable for unavailable values, such as `--` for missing diagnostics.

## Lifecycle

When the component is added:

- if `Font` is missing, no overlay children are created
- if `Font` is present, the overlay hierarchy is created immediately
- the five text rows are seeded with live values or placeholders

When the component is updated:

- it checks whether the refresh interval elapsed
- if so, it recomputes all five rows and updates the text components

When the component is removed or loses its font:

- it disposes the generated overlay subtree
- it unregisters itself from shared frame sampling

The component should behave as a single-instance overlay in authoring flows, matching `FPSComponent`.

## Editor And Runtime Integration

`DebugComponent` needs the same integration layers as `FPSComponent`:

- component add-catalog registration
- editor persistence descriptor
- runtime component deserializer
- runtime component registry registration
- build-packager support for Windows scene packaging
- scene asset reference inference for the assigned font

This keeps the component usable in authored scenes, editor previews, and packaged runtime content with the same workflow users already have for `FPSComponent`.

## Error Handling

The component should fail fast for invalid required state:

- building the overlay without an attached parent
- building the overlay without a font
- reading required shared services from an invalid runtime state

It should not silently manufacture fake diagnostics values. Missing memory diagnostics should surface only as placeholder row text, not as invented numeric defaults.

## Verification

Add focused tests for:

- overlay remains absent when no font is assigned
- assigning a font after attachment builds the fixed five-row hierarchy
- removing the component disposes overlay entities and drawables
- sampled updates refresh all five visible rows
- placeholder memory text is used when diagnostics are unavailable
- memory rows format resident and committed values in megabytes with one decimal
- drawable and draw-call rows use the expected shared-core values
- persistence and runtime deserialization preserve the component configuration
- editor add-catalog and packaging paths recognize the new component type

## Notes

This component is intentionally minimal and separate from `FPSComponent`. If more runtime overlays appear later, the engine can revisit a shared overlay abstraction with real evidence instead of introducing one preemptively.

# Native 2D Clip Command Stream Design

## Goal

Add a generic engine-wide rectangular 2D clipping model that survives managed queue ordering, generated-core translation, and native Windows execution. This slice should make clipping a first-class render concept for 2D UI without coupling it to one backend or one widget family.

## Problem

The current 2D pipeline has partial scroll and viewport behavior, but it does not have a real clip ownership seam in core. Drawables can be ordered and rendered, yet the engine cannot express "everything between these two points is clipped to this rectangle" as part of the queue itself.

That causes two architectural problems:

1. Core cannot describe nested UI clipping in a backend-agnostic way.
2. Native backends can only clip to the camera viewport, not to UI container bounds.

The new resolved 2D command-list foundation already handles primitive execution for textured quads, glyph quads, and rounded rectangles. The next step is to extend that foundation with explicit clip state transitions.

## Scope

This slice adds:

- rectangular clip ownership in the 2D render ordering layer
- ordered clip transitions in the render queue
- clip push and clip pop commands in the resolved 2D command stream
- native Windows scissor execution for nested clips
- engine-level tests that prove overflowed content is clipped correctly

This slice does not add:

- rounded or arbitrary-shape masking
- managed backend migration to the resolved command stream
- text shaping redesign
- rounded-rectangle visual fidelity work beyond existing behavior

## Architectural Direction

The 2D pipeline should be split into four layers with clear ownership:

1. UI/layout code declares clip intent.
2. `RenderQueue2D` owns ordered clip state transitions and drawables.
3. `RenderCommandListBuilder2D` resolves that ordered stream into codegen-safe commands.
4. Native backends execute the stream with a clip stack and scissor state.

Clipping belongs to render ordering, not to individual drawable implementations. A panel, scroll view, or similar UI container should push a clip region before its children render and pop it after they render. The children should stay simple primitives.

## Clip Model

The clip model for this slice is intentionally narrow:

- axis-aligned rectangles only
- one push rectangle per `ClipPush`
- `ClipPop` restores the previous clip state
- nested clips intersect with the active clip region
- coordinates are in the same resolved 2D space the command list already uses

This model is sufficient for:

- scroll views
- dialog bodies
- panels
- property lists
- queued build cards
- nested UI containers

It also maps well to constrained hardware because rectangular clip stacks are cheap to represent and easy to translate to backend scissor semantics.

## Core Queue Changes

`RenderQueue2D` should gain a first-class notion of ordered clip entries beside drawables. The queue must be able to emit:

- clip push
- clip pop
- drawable

The ordering contract matters more than the storage shape. The queue must preserve exact relative order so nested containers can express:

1. push outer clip
2. draw some content
3. push inner clip
4. draw nested content
5. pop inner clip
6. draw more outer content
7. pop outer clip

That ordered sequence is the engine truth. Backends must not infer clipping heuristically from widget types.

## Resolved Command Stream Changes

The resolved command stream should extend `RenderCommand2DType` with:

- `ClipPush`
- `ClipPop`

`RenderCommandList2D` should remain codegen-safe:

- list-backed storage only
- explicit payload getters
- no object graph
- no delegates
- no local helper closures

`ClipPush` carries one rectangular payload. `ClipPop` carries no payload beyond the logical command type.

The updated resolved command list then supports:

- `ClipPush`
- `ClipPop`
- `TexturedQuad`
- `GlyphQuad`
- `RoundedRect`

## Native Windows Execution

`Win32RenderManager2D` should maintain a clip stack while iterating the resolved command stream.

Execution rules:

- no active clip means the backend uses only the current camera viewport
- `ClipPush` intersects the new rectangle with the current effective clip if one exists
- `ClipPop` restores the previous effective clip
- all draw commands render under the current scissor rectangle

The backend should continue using the current camera viewport as the outer rendering boundary. Clip stack rectangles should be resolved inside that camera pass, not treated as a replacement for the viewport.

This gives correct nested clipping without changing primitive semantics.

## UI Integration Boundary

The clip owner should be a container-level UI/render participant, not the primitive drawables themselves.

Expected integration pattern:

- a container determines its clip rect
- it pushes that rect into the queue
- it emits child content
- it pops the clip rect after child content

This keeps UI composition explicit and avoids backend-specific special cases. It also allows future containers to reuse the same clipping contract without modifying the renderer again.

## Error Handling

The engine should fail clearly on invalid clip stream construction rather than silently accepting broken nesting.

Required behaviors:

- popping an empty clip stack during command execution should throw
- malformed queue ordering that produces impossible clip nesting should fail fast in tests
- draw execution with no active clip should still work as it does today

The design should not add best-effort recovery that masks render-order bugs.

## Testing

Testing should prove generic behavior instead of only validating the demo menu.

Coverage should include:

- render queue ordering preserves clip push/pop around drawables
- resolved command builder emits clip commands in the correct sequence
- nested clip intersection is preserved
- Windows platform build still succeeds with the new generated-core types
- at least one engine-level UI overflow case clips instead of bleeding outside its container

The existing Windows build-result feature summary logging must remain intact.

## Deferred Work

The following are intentionally deferred:

- rounded clip masks
- arbitrary polygon clipping
- managed DirectX11 or Vulkan migration to the resolved command stream
- full rounded-rectangle raster fidelity improvements
- broader text behavior parity

Those should build on this clip foundation instead of being folded into it.

## Success Criteria

This slice is successful when:

- 2D clipping is represented in core render ordering, not inferred in the backend
- the resolved command stream carries clip transitions safely through generated-core
- native Windows executes nested rectangular clips with scissor state
- generic UI overflow can be clipped correctly
- the system remains compatible with the current constrained-hardware-friendly command-stream direction

# Native 2D Command List Foundation Design

## Summary

The engine should introduce a compact backend-neutral 2D command list between the existing `RenderQueue2D` scene-facing API and backend-specific rendering implementations. The first implementation target is the native Windows host, which currently has the most duplicated backend-specific 2D logic and the most immediate correctness gaps.

This command list is intended to be a solid low-level foundation for constrained hardware targets without overfitting to the most restrictive future platforms. It should be compact, transient, allocation-light, and limited to the primitives the engine actually needs today.

## Goals

- Build a native-friendly 2D rendering foundation that is viable for RAM-constrained hardware.
- Preserve the existing high-level `RenderQueue2D` scene API in the first pass.
- Resolve layout, wrapping, and UI state upstream before backend rendering.
- Route the native Windows backend through the new command list first.
- Limit the first command set to current real engine needs.
- Keep the backend render path simple, predictable, and low-overhead.

## Non-Goals

- Replacing the entire 2D pipeline across every backend in one pass.
- Introducing extra future-facing primitives that current scenes do not need.
- Moving text wrapping, anchor resolution, or menu layout into backend code.
- Designing for GBA-class hardware constraints in this pass.
- Creating a retained 2D command graph that persists across frames.

## Why This Boundary

The current visitor-based 2D backend model requires each renderer to interpret high-level drawables directly. That duplicates logic across backends and makes correctness, clipping, and batching harder to unify. It also creates a weak boundary for future native or constrained-hardware renderers.

A compact resolved command stream is a better foundation because:

- upstream systems can continue to own layout and text logic
- backends can consume already-resolved draw data
- batching and clipping become explicit backend work rather than implicit scene-object interpretation
- constrained platforms can translate a flat command stream more efficiently than a graph of scene objects

This is appropriate for PS1-class future targets. It is not intended to define the full baseline for GBA-class hardware, which would likely require a stricter specialized subset or backend path later.

## High-Level Architecture

The first-pass 2D flow becomes:

1. Scene/components populate the existing `RenderQueue2D`.
2. A new `RenderCommandListBuilder2D` walks the queue once per camera.
3. The builder emits a compact resolved `RenderCommandList2D`.
4. The native Windows renderer consumes that command list directly.
5. Existing managed backends remain on their current path during the first migration phase.

This keeps the scene-facing engine API stable while improving the backend contract.

## Command List Model

Each camera frame produces one transient 2D command list.

### Structural rules

- The list is rebuilt every frame.
- The list is reset between camera renders.
- The list is array-backed and sequentially written.
- Command emission order is the final render order.
- The design must avoid per-command heap allocation.

### First command types

- `ClipPush`
- `ClipPop`
- `SolidRect`
- `TexturedQuad`
- `GlyphQuad`

No additional primitives are included in the first pass.

## Data Carried By Commands

Commands carry resolved backend-ready data only.

### `ClipPush`

- resolved clip rectangle in pixel space

### `ClipPop`

- no payload beyond command type

### `SolidRect`

- resolved pixel-space rectangle
- resolved fill color

### `TexturedQuad`

- resolved pixel-space rectangle
- resolved UV rectangle
- resolved tint color
- texture reference handle or backend-resolvable texture identity

### `GlyphQuad`

- resolved pixel-space rectangle
- resolved atlas UV rectangle
- resolved tint color
- font atlas texture reference handle or backend-resolvable texture identity

## Upstream Versus Backend Responsibilities

### Upstream responsibilities

- anchor resolution
- final UI layout
- text wrapping
- glyph positioning
- selection/highlight state
- flattening current rounded-rect behavior into supported commands
- command ordering

### Backend responsibilities

- consume the resolved command stream
- manage clip stack application
- draw quads and rects
- batch where possible without changing visible ordering semantics
- sample textures and atlas data

The backend must not own menu layout or text wrapping decisions.

## Rounded Rect Strategy

The first pass does not introduce a dedicated rounded-rect backend primitive.

Rounded rectangles must be flattened upstream into the currently supported primitives needed by existing scenes. This keeps the command list small and backend-simple. If later profiling or fidelity requirements justify a dedicated command, that should be added in a future pass rather than upfront.

## Text Strategy

Text remains upstream-resolved.

The command list will not carry high-level text runs. Instead, it will carry already-resolved glyph quads. This keeps backend font logic minimal and avoids duplicating shaping, wrapping, or layout behavior across renderers.

This is the preferred tradeoff for constrained hardware because it moves complexity into shared engine logic and keeps the backend format compact and explicit.

## Native Windows Scope

The native Windows backend is the first consumer of the command list.

The Windows host renderer should:

- stop depending on ad hoc high-level drawable interpretation for the targeted 2D path
- consume the command list in strict emission order
- apply clip push/pop explicitly
- draw solid rects, textured quads, and glyph quads through its DirectX path

The native Windows backend remains the immediate verification target for the new architecture.

## Managed Backend Migration

The first pass does not force DirectX11 managed or Vulkan managed onto the same command path immediately.

### First migration phase

- add command-list builder in core
- route native Windows through it
- keep managed backends unchanged

### Later migration phase

- move managed backends onto the shared command stream
- remove duplicated drawable interpretation logic once parity is proven

This reduces risk while still establishing the correct architecture.

## Performance And Memory Constraints

The command list must be designed for low overhead.

### Required characteristics

- transient per-frame memory
- contiguous storage
- minimal payload per command
- no retained object graph
- no command-level virtual dispatch requirement after emission

The design should assume future low-memory native backends will benefit from a flat stream much more than from traversing high-level drawable objects directly.

## Engine Units To Introduce

The first implementation pass should add focused units with one clear purpose each.

### Core

- `RenderCommand2DType`
- `RenderCommandList2D`
- `RenderCommandListBuilder2D`
- compact command payload structs for the supported command types

### Native Windows host

- adapter logic that reads `RenderCommandList2D`
- clip stack execution
- quad/rect submission path using the already-established native 2D pipeline

## Risks

### Ordering regressions

The command builder could accidentally change visible stacking compared to the existing queue traversal.

Mitigation:
- preserve strict queue traversal order
- treat emission order as final draw order

### Clipping regressions

Clip stack handling could diverge from current UI expectations.

Mitigation:
- test explicit clip push/pop nesting
- keep clip state owned by the command stream instead of implicit backend heuristics

### Text placement regressions

Glyph flattening could emit wrong bounds or excessive quads.

Mitigation:
- add focused builder tests for text quad output
- validate against the demo menu scene

### Backend state leakage

The backend and upstream path could both mutate rendering assumptions in conflicting ways.

Mitigation:
- make the command stream fully resolved before backend submission
- treat backend work as execution, not interpretation

## Testing Strategy

### Builder-level tests

- rounded-rect flattening emits only supported primitives needed by current scenes
- text flattening emits the expected glyph quads and positions
- clip push/pop nesting is correct
- command order matches the existing queue order

### Native integration diagnostics

- command counts by type for the demo menu scene
- verification that glyph commands and rect commands are both consumed
- verification that clip commands are emitted when expected

### Live packaged verification

- packaged Windows build renders the current demo menu through the command stream
- no blank-output regression
- inactive baked panels remain hidden

## Success Criteria

This pass is successful when:

- native Windows renders the current menu scene through the command stream
- the command list contains only the current required primitives
- layout and wrapping remain upstream
- clipping is explicit and backend-controlled
- the architecture is viable for future constrained-hardware backends without requiring renderer-specific drawable interpretation

## Recommended Implementation Order

1. Add core command-list types and builder.
2. Teach the builder to emit current needed primitives only.
3. Route native Windows through the command stream.
4. Preserve current packaged menu rendering behavior.
5. Add builder and integration diagnostics/tests.
6. Leave managed backend migration for a later dedicated pass.

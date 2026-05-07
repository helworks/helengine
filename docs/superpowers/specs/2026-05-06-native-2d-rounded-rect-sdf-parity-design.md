# Native 2D Rounded-Rect SDF Parity Design

## Goal

Bring the native Windows 2D renderer to rounded-rectangle visual parity with the managed renderer by replacing the current rectangle-strip approximation with a dedicated signed-distance-field draw path. The shared `RenderCommandList2D` rounded-rect command stays semantic and compact.

## Problem

The current native Win32 bridge handles `RenderCommand2DType::RoundedRect` by drawing one filled rectangle plus four border strips. That ignores:

- corner radius
- per-corner enablement
- anti-aliased curved edges
- proper fill and border blending near corners

The result is structurally wrong for most editor UI chrome and for menu/runtime scenes that rely on rounded cards, buttons, tabs, dialog panels, and checkbox backgrounds.

## Decision

Use a native SDF rounded-rect shader path in the Win32 2D bridge.

The command stream does not change. `RenderCommandList2D` continues to carry the rounded-rect semantic payload it already has:

- bounds
- radius
- border thickness
- fill color
- border color
- corner mask

The Win32 backend derives the SDF inputs locally from that payload and renders each rounded rect as one quad with a dedicated pixel shader.

## Rejected Approaches

### Rectangle-strip approximation

This is what exists today. It is fast but fundamentally wrong and cannot reach parity.

### Nine-slice atlas rendering

This can look acceptable, but it adds atlas/cache concerns to the native host and treats rounded rects like pre-baked art instead of a renderer primitive. It is the wrong first parity slice.

### Tessellated geometry

This would increase CPU work and vertex traffic, complicate borders and edge smoothing, and still not match the managed SDF path as cleanly.

## Architecture

### Shared engine boundary

No core command-stream expansion is required for this slice.

`RenderCommandList2D` remains the cross-backend semantic boundary. Future backends still receive one stable rounded-rect command shape and can choose their own implementation strategy.

This avoids baking Win32-specific implementation details into the portable 2D command stream too early.

### Native Win32 rendering path

The Win32 bridge gets a dedicated rounded-rect pipeline parallel to the existing textured-quad pipeline.

That path includes:

- a small rounded-rect vertex shader
- a rounded-rect pixel shader with SDF evaluation
- one dynamic quad draw per rounded rect
- one compact constant payload per draw

The constant payload should include:

- rect origin and size
- radius
- border thickness
- fill color
- border color
- corner enable mask

The quad remains a simple axis-aligned rectangle covering the command bounds. The pixel shader computes local rect-space distance and resolves whether each fragment is:

- outside the shape
- inside the fill
- inside the border band

Fragments outside the shape output transparent alpha, allowing the existing blend state and clip/scissor path to work naturally.

## Rendering Behavior

The native rounded-rect path must support:

- fill-only rounded rects
- border-only rounded rects
- fill plus border
- per-corner enablement through `RoundedRectCorners`
- anti-aliased edges comparable to the managed renderer

The new path must compose correctly with the completed clip-command stream:

- clip push and pop continue to constrain the rounded-rect quad through scissor state
- clipped rounded corners must still look correct at the scissor boundary

## Performance Constraints

This slice should optimize for constrained hardware as well as future backend portability.

The chosen shape is:

- one semantic command per rounded rect
- one quad per rounded rect
- no CPU tessellation
- no atlas dependency
- no command-payload expansion

That keeps CPU cost and command memory low while preserving a clean future backend contract.

## Files And Responsibilities

### Engine worktree

- `engine/helengine.core/managers/rendering/RenderCommandList2D.cs`
  - unchanged in shape for this slice
- `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
  - preserves generated-core and Windows build coverage

### Native host worktree

- `src/platform/windows/win32/win32_render_bridge.hpp`
  - rounded-rect pipeline state, constants, and helper declarations
- `src/platform/windows/win32/win32_render_bridge.cpp`
  - rounded-rect shader compilation, constant setup, and SDF draw execution

## Testing Strategy

### Focused validation

Keep the existing engine-side command-stream and packaging tests intact. This slice should not alter rounded-rect command semantics.

Add or retain focused verification for:

- Win32 bridge compilation with the existing generated core
- Windows build smoke test with generated core containing rounded-rect commands

### Real integration verification

Run a real packaged Windows build using the editor app entrypoint and confirm it completes successfully with the native host changes.

### Manual visual verification

Because there is no reliable image snapshot harness here yet, final visual validation remains manual:

- editor/runtime surfaces that use rounded rects should now render curved corners instead of plain rectangles
- clipped rounded-rect surfaces should still clip correctly

## Non-Goals

This slice does not:

- change the shared rounded-rect command payload
- add atlas-based rounded-rect rendering
- add geometry tessellation for rounded rects
- attempt text parity
- attempt broader shader unification across every backend

## Follow-Up Work

After this slice, the next likely parity target is text behavior, since clipping and rounded-rect correctness will both be in place.

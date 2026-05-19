# Direct 2D In 3D Viewport Design

## Summary

The editor should stop previewing scene 2D content through a render-target plane and instead render 2D content directly inside every 3D scene viewport.

`ViewportComponent` becomes the authoritative owner of scene viewport resolution. The resolved viewport size defines a world-space 2D presentation rectangle where `1 viewport pixel = 1 world unit`. A `1280x720` viewport therefore exposes a `1280 x 720` world-space 2D presentation area.

This removes the proxy-plane architecture, lets the viewport picker target underlying 2D entities directly, and makes 2D selection win over overlapping 3D geometry.

## Goals

- Render authored 2D scene content directly in every 3D scene viewport.
- Move scene viewport resolution responsibility into `ViewportComponent`.
- Preserve screen-scale authoring semantics for 2D content.
- Allow direct selection of underlying 2D entities from the 3D viewport.
- Ensure 2D picking wins when 2D and 3D overlap visually.

## Non-Goals

- Converting 2D entities into freeform 3D entities.
- Preserving the render-target plane as the long-term presentation model.
- Adding a per-viewport toggle for this behavior.
- Redesigning general-purpose runtime 2D authoring semantics outside the viewport-resolution and world-presentation contract needed here.

## Current Problem

The editor currently renders scene 2D content to an offscreen render target and displays that result on a world-space plane inside the 3D viewport.

That architecture causes two problems:

1. Picking sees the plane, not the underlying 2D entities.
2. Viewport presentation logic is split between `ViewportComponent` and editor-only canvas-plane systems.

The existing plane-selection bridge can map some pointer hits back into canvas-space entities, but it remains a proxy architecture and does not satisfy the desired model of rendering and selecting the actual 2D scene content in the 3D viewport.

## Proposed Model

### 1. Viewport Resolution Ownership

`ViewportComponent` becomes the system that resolves scene viewport size and bounds for direct 2D-in-3D presentation.

The editor scene viewport consumes that resolved rectangle instead of routing scene 2D presentation through `EditorViewportCanvasPlanePreviewComponent`.

The resolved viewport width and height define the direct 2D world-presentation size.

### 2. World-Presented 2D Space

Scene 2D content is rendered directly in the 3D viewport using a world-space presentation rectangle whose dimensions match the resolved viewport size.

Rules:

- `1 viewport pixel = 1 world unit`
- viewport width becomes world width
- viewport height becomes world height
- authored 2D positions and sizes remain screen-scale in intent

Example:

- viewport size `1280x720`
- world-presented 2D area `1280 x 720`

This is still screen-scale presentation, not freeform 3D authoring.

### 3. Every Viewport

The behavior applies to every scene viewport by default.

There is no per-viewport toggle in this design. Any scene viewport that can currently show scene content should also show directly rendered 2D content under the same viewport-resolution contract.

### 4. Picking Priority

When both projected 2D and 3D content lie under the pointer, 2D wins.

Selection order:

1. resolve selectable 2D entity under the pointer
2. if no selectable 2D entity is hit, resolve 3D selection normally

This gives authored UI/content reliable editability even when overlapping with 3D scene geometry.

## Architectural Consequences

### Remove the Plane Bridge as the Primary Model

The current render-target plane preview and plane hit-test bridge become transitional compatibility code at most. They should no longer be the primary presentation or selection mechanism for scene 2D content.

### Unify Presentation Responsibility

Viewport resolution and 2D presentation rules should no longer be split across:

- `ViewportComponent`
- editor canvas profile state
- editor canvas plane preview entities
- special canvas-plane picking bridges

The core rule should be explicit and centralized:

- `ViewportComponent` resolves viewport size
- scene 2D content is presented directly in 3D using that size
- selection resolves actual 2D entities first

## Expected Benefits

- Direct selection of actual 2D entities from the 3D view
- Simpler mental model for users and engine contributors
- Fewer editor-only proxy systems
- Better alignment between rendering and picking
- Cleaner future path for mixed 2D and 3D scene authoring

## Risks

### Render Ordering

Direct 2D-in-3D submission needs a clear render-order contract relative to 3D scene content and editor gizmos. The picking rule is already defined as “2D wins,” and render submission must align with that behavior.

### Coordinate Mapping

The world-presentation transform must stay deterministic across viewport resizes and camera updates so that the presented 2D content remains stable and pickable.

### Legacy Canvas-Plane Dependencies

Editor systems that currently assume the existence of the preview plane may need migration or deletion as this model becomes authoritative.

## Acceptance Criteria

- 2D scene content renders directly in every 3D scene viewport.
- `ViewportComponent` owns the relevant scene viewport resolution contract.
- A `1280x720` viewport presents 2D content in a `1280 x 720` world-space rectangle.
- Clicking visible 2D content in the 3D view selects the underlying 2D entity.
- When 2D and 3D overlap, 2D selection wins.
- The old plane-proxy model is no longer the primary scene 2D viewport path.

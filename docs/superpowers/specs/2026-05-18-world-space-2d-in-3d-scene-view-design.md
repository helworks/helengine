# World-Space 2D In 3D Scene View Design

## Summary

The editor scene view should render 2D scene content as true world-space content, using each entity's actual transform in the 3D world.

This replaces the earlier idea of projecting 2D scene content into a screen-sized world rectangle. The correct model is the Unity-style scene view model:

- 2D objects appear at their real `Position` / transform in the 3D world
- the scene camera applies real perspective and distance
- 2D still has editor picking priority over overlapping 3D content

The only exception is a subtree governed by `ViewportComponent`. When a 2D entity lives under a `ViewportComponent`, that subtree continues to use viewport-lock and resizing behavior.

## Goals

- Render 2D scene components in the 3D scene view at their actual world transform.
- Preserve full 3D scene-camera perspective and depth behavior for 2D rendering.
- Keep `ViewportComponent` as the one explicit exception that can lock and resize a 2D subtree against viewport space.
- Make editor picking prefer 2D entities over overlapping 3D entities.

## Non-Goals

- Rendering scene 2D into a screen-sized proxy rectangle.
- Keeping the render-target plane as the primary scene-view model.
- Adding a second transform mode toggle to every 2D component.
- Changing runtime gameplay semantics for 2D transforms outside the existing `ViewportComponent` layout exception.

## Current Problem

The current work moved scene-view handling closer to direct selection and viewport-owned sizing, but it still treats scene 2D too much like a special projected layer instead of real world content.

That is the wrong model for scene authoring. Users should see 2D scene objects in the 3D view exactly where they are in the world, with the same camera perspective behavior as other scene content. The only special-case layout behavior should come from a real authored `ViewportComponent` ancestor.

## Proposed Model

### 1. Default World-Space 2D

All authored 2D scene objects render in the 3D scene view at their actual entity transform.

Rules:

- entity `Position`, rotation, and scale remain authoritative
- 2D content is part of the 3D world view
- scene camera distance and perspective affect 2D visuals just like other scene content

This is the baseline behavior for scene-view rendering.

### 2. `ViewportComponent` As The Only Layout Exception

If a 2D entity belongs to a subtree governed by `ViewportComponent`, that subtree keeps the viewport-lock and resizing behavior already associated with viewport-owned layout.

Rules:

- `ViewportComponent` remains the explicit authored signal for viewport-driven layout
- no separate per-component mode is introduced
- outside a `ViewportComponent` subtree, 2D content behaves as ordinary world content

This keeps the exception localized and predictable.

### 3. No Proxy Plane

The editor should not rely on a world-space render-target plane to represent scene 2D content.

That proxy architecture should no longer be the primary scene-view rendering or picking model. Scene 2D should be rendered and selected as actual scene content.

### 4. Picking Priority

When a 2D entity and a 3D object overlap under the pointer, 2D wins for editor selection.

This is an editor interaction rule, not a depth rule.

Selection order:

1. resolve selectable 2D entity under the pointer
2. if none exists, fall back to normal 3D picking

That preserves usability for UI-heavy or overlay-heavy scenes even when 3D geometry visually overlaps.

## Architectural Consequences

### Rendering

Scene-view 2D should be submitted as world content, not first converted into a separate screen-space presentation model.

Any remaining editor code that treats scene 2D as a simulated canvas layer should be reduced to the `ViewportComponent` exception or removed.

### Viewport Ownership

`ViewportComponent` still matters, but its role is narrower and clearer:

- it owns viewport-driven layout exceptions
- it does not redefine the default scene-view rendering model for all 2D content

### Picking

Picker logic should explicitly resolve 2D scene hits first, using the actual rendered 2D scene content model, then fall back to 3D selection.

## Expected Benefits

- Scene view matches author expectation: 2D objects appear where they really are in the world
- Mixed 2D/3D authoring becomes more intuitive
- `ViewportComponent` remains useful without dominating all 2D scene rendering
- Selection remains practical because 2D can still override 3D during editor picking

## Risks

### Existing Screen-Space Assumptions

Some editor viewport code currently assumes scene 2D is effectively screen-space or canvas-space first. Those assumptions will need to be removed or pushed behind the `ViewportComponent` exception.

### Transform Interpretation

2D components that were previously authored assuming proxy-plane behavior may appear in unexpected places once the scene view uses true world transforms. That is expected, because the old model was wrong for this goal.

### `ViewportComponent` Boundary

The boundary between normal world-space 2D and viewport-owned layout must stay explicit and deterministic. The implementation should not let partial inference or hidden fallback rules blur that line.

## Acceptance Criteria

- A 2D scene object outside any `ViewportComponent` subtree appears in the 3D scene view at its real world transform.
- Scene-view rendering applies normal 3D camera perspective and distance to 2D content.
- A 2D scene object inside a `ViewportComponent` subtree still follows viewport-lock/resizing behavior.
- Scene-view selection prefers 2D entities over overlapping 3D objects.
- The render-target plane is no longer the primary model for scene-view 2D rendering.

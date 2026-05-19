# Editor-Only World-Space 2D Scene View Design

## Summary

The editor scene view should display authored 2D scene components as world-space content at their real scene transform, but this must be implemented entirely on the editor side.

`helengine.core` must not change.

That means the editor will create and maintain internal world-space preview objects for authored 2D components instead of changing the core/runtime 2D rendering model. These preview objects exist only for scene-view visualization and picking.

Supported component types in the first pass:

- `SpriteComponent`
- `TextComponent`
- `RoundedRectComponent`

## Goals

- Show authored 2D scene content in the 3D scene view at the source entity's real transform.
- Keep all implementation editor-only.
- Preserve `ViewportComponent` as the exception that keeps viewport-lock/resizing behavior.
- Make selection of 2D scene content resolve back to the underlying authored 2D entity.
- Keep 2D priority over overlapping 3D content during editor picking.

## Non-Goals

- Changing `helengine.core`
- Changing runtime 2D rendering contracts
- Making 2D components themselves become 3D runtime objects
- Introducing new author-facing component modes just for scene-view visualization

## Current Constraint

The current 2D rendering path is fundamentally screen-space and runtime-owned. It is not appropriate to refactor that inside this work because this request is explicitly editor-only and must not touch `helengine.core`.

Therefore, the scene-view result has to come from editor-managed preview proxies rather than runtime/core renderer changes.

## Proposed Model

### 1. Editor-Owned World-Space Preview Proxies

For each supported authored 2D component, the editor builds one or more internal preview objects that render in the scene view as world-space content.

These preview objects:

- are editor-only
- are internal and not directly selectable
- mirror the authored source entity/component
- are updated as the source changes
- are removed when the source goes away

### 2. Component Mapping

#### `SpriteComponent`

The editor creates a world-space textured quad preview positioned at the source entity transform.

#### `TextComponent`

The editor creates world-space text preview geometry or quads from the loaded font asset so text appears in the 3D scene view at the source transform.

#### `RoundedRectComponent`

The editor creates a world-space shape preview that matches the authored rounded-rectangle appearance closely enough for scene editing.

## 3. Transform Rules

### Default Behavior

Outside a `ViewportComponent` subtree, preview objects use the real source entity transform.

That means scene-view 2D appears in the 3D world where the entity really is, like Unity scene view behavior.

### `ViewportComponent` Exception

If the source 2D entity belongs to a subtree governed by `ViewportComponent`, the editor preserves viewport-lock and resizing behavior for the preview.

That keeps `ViewportComponent` as the only special-case layout rule.

## 4. Picking

Picking resolves through the preview object back to the original authored 2D entity.

Rules:

- preview objects themselves are not the final selection target
- the underlying authored 2D entity is selected
- if 2D and 3D overlap, 2D wins

This is an editor interaction rule, not a rendering-depth rule.

## 5. Lifecycle

The editor needs one synchronization layer that:

- discovers supported 2D scene components
- creates preview proxies when needed
- updates preview transforms/material state when source state changes
- destroys preview proxies when the source entity/component is removed or disabled

This should be centralized rather than having ad hoc preview logic per viewport subsystem.

## Architectural Consequences

### No Core Changes

The runtime and shared engine remain untouched.

All new rendering and synchronization behavior lives in editor code and editor-owned entities/components.

### Scene-View Only

This affects scene-view visualization and selection only. It does not alter runtime gameplay rendering.

### Existing Canvas Proxy

The existing render-target plane should no longer be the primary model for scene-view 2D visualization. It may remain only where still needed for viewport-owned exceptions or compatibility during migration.

## Expected Benefits

- Users see 2D content in the 3D scene view at its real world transform
- No risky changes to shared runtime/core rendering
- Selection stays intuitive because clicking previewed 2D chooses the real authored entity
- `ViewportComponent` remains the single explicit exception

## Risks

### Preview Fidelity

Editor-only preview proxies may not match runtime 2D rendering perfectly at first, especially for text and rounded rectangles.

### Synchronization Complexity

The editor must keep preview proxies in sync with their source components and transforms. The synchronization layer needs to be deterministic and easy to reason about.

### Duplicate Scene Representation

There will temporarily be both:

- authored 2D scene components
- editor-owned 3D preview proxies

That duplication must stay internal and well-mapped.

## Acceptance Criteria

- `SpriteComponent`, `TextComponent`, and `RoundedRectComponent` appear in the 3D scene view through editor-only world-space previews.
- Outside a `ViewportComponent` subtree, previews use the real scene transform of the source entity.
- Inside a `ViewportComponent` subtree, previews preserve viewport-lock/resizing behavior.
- Clicking a previewed 2D object selects the underlying authored 2D entity.
- When 2D and 3D overlap, 2D selection wins.
- No changes are made in `helengine.core`.

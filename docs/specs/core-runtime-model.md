# Core Runtime Model

Status: living document — reflects `engine/helengine.core` as built. Update it when the behavior it describes changes. Rendering, physics-runtime internals, and diagnostics are covered elsewhere.

## Core

`Core` is the singleton coordinator: a cheap constructor followed by an explicit `Initialize` that wires up `ObjectManager`, the content manager, and (optionally) `SceneManager`. Each `Update()` runs a fixed stage order — input early-update, `ObjectManager.Update()`, audio, fixed-step physics, then late input/pointer handling — and physics runs on a capped, carry-over fixed-step scheduler (unconsumed time rolls into the next update rather than being dropped). `Draw()` optionally commits deferred scene load/unload at a single frame-boundary safe point (`CompleteFrameBoundary`); that is the only place scene mutation is allowed to happen.

Guidelines:
- Don't reorder the `Update()` stage sequence without checking what depends on it (e.g. physics must run before the late input/pointer pass).
- Scene load/unload must stay confined to the frame-boundary commit — never mid-update or mid-draw.

## Entity & Component

`Entity` is a scene-graph node with explicit, one-time collection init (`InitComponents`/`InitChildren`) and transforms composed parent-down using a fixed scale → rotate → translate convention. Enabled/static state propagate to children and components. Disposal order is deliberate: components first, then children recursively, then self-detach — rendering and asset-ownership cleanup depend on that order.

`Component` lifecycle callbacks (`ComponentAdded`, `ComponentInitialized`, etc.) are virtual no-ops by default, and only `Entity` may attach/detach a component's `Parent`. A small synthetic-member bag lets platform-extended code attach ad hoc values without subclassing.

Guidelines:
- Keep the transform composition order and the components-then-children disposal order stable — both have downstream dependents.
- Don't give components a way to reparent themselves outside `Entity`.

## ObjectManager

Owns the flat registries the engine iterates each frame (entities, updateables, drawables, cameras, lights, interactables) and mediates which camera's render queues a drawable lands in, based on layer mask and viewport binding. The update list stays sorted by update order; any registration change requested while the update loop is actively iterating is queued and applied after the loop finishes, never spliced in mid-iteration.

Guidelines:
- Never mutate the update list while it's actively iterating — route through the deferred-operation queue.
- Camera/drawable matching should stay a fresh computation (layer mask + viewport binding), not a cached relationship that can go stale across a reparent.

## SceneManager

Scene load/unload requests are deferred by default and only take effect at the `Core` frame boundary. A separate engine-owned "scene transition" path advances a small state machine one step per frame boundary for progressive loading. Assets owned by loaded scenes (textures, models, etc.) are reference-counted across scenes, not per-scene, so a texture shared by two loaded scenes survives until both unload it.

Guidelines:
- Keep all scene mutation on the frame-boundary commit path.
- Keep owned-asset release reference-counted across scenes — releasing on the first scene's unload would break assets shared with a still-loaded scene.

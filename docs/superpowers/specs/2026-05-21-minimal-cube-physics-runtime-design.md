# Minimal Cube Physics Runtime Design

## Goal

Replace the current `helengine.physics3d` runtime behavior with a small cube-only solver that can be converted to C++ and run under tight memory budgets. The engine should do cube physics well before supporting any other 3D collider behavior.

## Scope

The active physics runtime supports only box colliders in this pass:

- Dynamic, static, and kinematic cube or cuboid rigid bodies.
- Gravity, fixed timestep stepping, solver substeps, and solver iterations.
- Box-box contacts, including dynamic-dynamic and dynamic-static pairs.
- Four-point face contact manifolds for stable cube stacking.
- Edge and corner contact fallback for tipping behavior.
- Deterministic, allocation-free hot path after scene binding.

Unsupported runtime behavior is intentionally removed from the active solver path:

- Sphere, capsule, character controller, and static mesh contact resolution.
- Trigger events for non-box shapes.
- Mesh triangle support and slope walking.
- General convex collision support.
- Any BEPU dependency or direct BEPU source vendoring.

Public authoring components can remain in the codebase for serialization compatibility, but non-box runtime participation should fail clearly or be ignored by explicit policy rather than trying to simulate poorly.

## BEPU-Informed Rules To Keep

BEPU is a reference for simulation rules, not a dependency:

- Separate body state into pose, velocity, inertia, collidable shape, and activity state.
- Predict/update bounds before contact detection.
- Generate contact manifolds before solving.
- Use sequential impulse contact solving as the primary behavior.
- Use positional correction only as a small residual penetration cleanup, not as the main physics response.
- Use fixed timesteps and substeps.
- Sleep only after repeated low-motion supported contact frames.

## Runtime Shape

The cube runtime should be explicit and compact:

- `CubePhysicsWorld3D` owns fixed-size arrays of cube bodies, shape records, candidate pairs, manifolds, and solver contacts.
- `CubeBodyState3D` stores only the data needed by the solver: body kind, pose, velocity, inverse mass, inverse inertia, half extents, material, activity, and entity/component sync references.
- `CubeContactManifold3D` stores up to four contact points, a normal, penetration depths, and body indices.
- `CubeSequentialImpulseSolver3D` resolves normal and friction impulses.
- `CubeBroadphase3D` starts simple with O(n^2) pair collection for correctness; a fixed-grid broadphase can be reintroduced later only after the solver is stable.

The hot path should avoid heap allocations. Dynamic collections are allowed during scene binding, but `Step` should reuse arrays.

## Contact Rules

Box-box contact should use oriented box SAT to decide overlap and select the collision normal.

Face contacts should generate a clipped four-point manifold. A cube resting flat on a cube or ground box must have a contact patch, not one point.

The solver should:

- Integrate gravity into velocities.
- Detect contacts from current predicted poses.
- Warm starting is optional for the first cube-only pass.
- Apply normal impulses to stop closing velocity.
- Apply friction impulses after normal impulses.
- Integrate positions and orientations.
- Apply small split positional correction for remaining penetration.

The solver should not move a supported lower cube upward just because an upper cube applies force. Stacked bodies transfer impulse through contact constraints; they should not be launched by positional correction.

## Compatibility

`PhysicsWorld3D` can become a wrapper around the cube runtime for now. It should bind only entities that have:

- `RigidBody3DComponent`
- `BoxCollider3DComponent`

For unsupported colliders, the first implementation should use a clear runtime exception during binding or stepping. Silent partial simulation would make debugging worse.

Existing scene serialization and editor UI should not be removed in this pass. The runtime behavior is narrowed first; authoring cleanup can happen later.

## Testing

Required regression coverage:

- Single cube falls under `-9.81` gravity at a plausible fixed-step rate.
- Dynamic cube lands on static cube and remains stable.
- Two dynamic cubes stack without lower-cube popping.
- Offset upper cube tips without launching the lower cube.
- Four stacked cubes settle without vertical overlap.
- Edge-supported cube tips instead of freezing.
- Rotated separated boxes do not collide because their broadphase bounds overlap.
- Unsupported non-box collider scene fails clearly.

## Success Criteria

The Windows city physics scene should show cube-cube behavior that is boring and stable:

- The falling cube falls at normal speed.
- The lower cube does not jump when loaded by the upper cube.
- The upper offset cube can tip naturally.
- Resting stacks do not jitter, sink, or pop.

The resulting C# code should be straightforward to convert dynamically to C++: small classes, fixed arrays, no reflection, no generic-heavy solver path, no runtime allocation in `Step`, and no dependency on BEPU.

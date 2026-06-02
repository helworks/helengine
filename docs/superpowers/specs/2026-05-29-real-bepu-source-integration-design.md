# Real BEPU Source Integration Design

## Goal

Replace the current fake `helengine.bepu` replacement-pass solver with a real integration of upstream BEPU v2 source so Windows/runtime physics for dynamic and static boxes and spheres uses genuine rigid-body simulation, including angular motion and naturally unstable stacks.

## Problem

The current `engine/helengine.bepu` assembly does not contain BEPU. It contains a custom narrow-scope solver plus a naming layer that implies BEPU integration. That solver only performs linear integration and simple overlap resolution, so unstable stacks do not topple correctly. A later heuristic (`ApplySupportInstability`) made this worse by simulating toppling with an artificial shove instead of real rigid-body behavior.

## Requirements

- Vendor upstream BEPU v2 source into the repository at a pinned revision.
- Use the upstream source as the actual simulation backend for `helengine.bepu`.
- Scope the first real integration to:
  - static rigid bodies
  - dynamic rigid bodies
  - box colliders
  - sphere colliders
  - gravity
  - entity transform synchronization
  - basic friction and restitution mapping
- Preserve hard failures for unsupported features such as capsules, static meshes, and character controllers.
- Remove the fake toppling heuristic and stop relying on the custom overlap-only solver as the production runtime path.
- Prove the fix with focused BEPU tests and the cooked `city` stacked-box scene.

## Non-Goals

- No capsule support in this pass.
- No static mesh support in this pass.
- No character controller support in this pass.
- No kinematic body implementation unless required as a minimal runtime dependency for scene loading.
- No attempt to support native C++ transpilation for upstream BEPU source in this pass if the runtime path remains managed-only for Windows player execution. If native codegen needs the same path, that becomes an explicit follow-up requirement rather than an accidental partial port.

## Architecture

### 1. Vendor upstream source

Import the upstream BEPU v2 source into a dedicated vendor area inside the engine repository, with the imported commit recorded in docs and project metadata. Keep the upstream layout recognizable so later updates can be diffed against the source project rather than reverse-engineered from a flattened copy.

### 2. Keep `helengine.bepu` as an adapter layer

`helengine.bepu` remains the Helengine-facing assembly, but it becomes a wrapper around upstream BEPU simulation objects rather than a hand-rolled solver. Its responsibilities are:

- runtime registration
- component-to-shape/body conversion
- BEPU simulation lifecycle
- entity-to-runtime and runtime-to-entity synchronization
- feature guard enforcement for unsupported Helengine components

### 3. Body and shape mapping

Map Helengine authored components into BEPU bodies and shapes:

- `RigidBody3DComponent` with `BodyKind3D.Static` becomes a static BEPU body.
- `RigidBody3DComponent` with `BodyKind3D.Dynamic` becomes a dynamic BEPU body with mass/inertia derived from the authored collider shape and body mass.
- `BoxCollider3DComponent` becomes a BEPU box shape.
- `SphereCollider3DComponent` becomes a BEPU sphere shape.

The first pass should use one collider per supported entity. Unsupported combinations remain rejected.

### 4. Runtime state synchronization

At bind time:

- create BEPU simulation
- create bodies/shapes for supported entities
- store Helengine-to-BEPU handles

At step time:

- advance BEPU simulation with the configured fixed step
- copy solved body transforms back to Helengine entities
- copy solved linear and angular velocity back to authored rigid-body components where needed

### 5. Testing

The red-green path must center on real rigid-body behavior:

- unstable half-unit-offset box tower topples without any heuristic
- cooked `city` stack-box scene topples through direct `world.Step`
- cooked `city` stack-box scene topples through `Core.Update`
- simple supported box and sphere stacks still remain stable
- unsupported collider/component combinations still fail immediately

## File Strategy

- Add vendored upstream source in a dedicated subtree.
- Replace the current fake solver internals in `engine/helengine.bepu`.
- Keep test coverage in `engine/helengine.bepu.tests`.
- Remove the heuristic path from `BepuPhysicsWorld3D`.

## Risks

- Upstream BEPU may rely on APIs or build assumptions that need small project-file adaptation for this repo.
- Existing tests written around the custom fake solver may need adjustment once real angular dynamics are introduced.
- If the Windows player build depends on transpilation/native conversion of this managed code path, upstream source compatibility with that toolchain must be validated early rather than discovered late.

## Success Criteria

- `helengine.bepu` contains real upstream BEPU simulation code, not a fake solver.
- The overhung four-box stack falls because of rigid-body dynamics, not because of any injected instability heuristic.
- The direct-start `city` stack-box Windows build visibly topples in live runtime.

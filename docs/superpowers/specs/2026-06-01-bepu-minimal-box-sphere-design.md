# Minimal Real-BEPU Box/Sphere Slice Design

## Summary

`helengine.bepu` should stop treating the immediate shipping target as the full upstream `BepuPhysics` library. The shipping target is a pruned real-BEPU slice that supports only the current Helengine rigid-body scene needs:

- `box-box`
- `sphere-sphere`
- `box-sphere`

The implementation must continue using real upstream BEPU source, but the native codegen/build pipeline should only traverse the smallest subset of BEPU required for static, dynamic, and kinematic rigid bodies using box and sphere colliders.

The broader converter/runtime work already completed is not wasted. It remains valid groundwork for later expansion back toward larger portions of upstream BEPU. However, the immediate Windows and DS build targets must be unblocked by pruning the active BEPU source graph instead of forcing full-library conversion now.

## Goals

- Keep the physics runtime based on real upstream BEPU source.
- Reduce the active BEPU source graph to the minimal subset required for box and sphere rigid-body scenes.
- Support the current authored physics validation scenes:
  - `test_scene_dynamic_stack_boxes`
  - `test_scene_dynamic_sphere_stack`
  - `test_scene_dynamic_mixed_stack`
- Preserve the existing `helengine.bepu` adapter surface so later shape/contact expansion can widen the slice without replacing the engine integration.
- Keep the portable codegen/runtime direction compatible with very low targets such as Nintendo DS.

## Non-Goals

- Full upstream `BepuPhysics` conversion for the immediate shipping milestone.
- Immediate support for mesh, compound, convex hull, sweep, reduction, or broad constraint families unrelated to the current box/sphere scenes.
- Replacing BEPU with a handwritten solver or fake instability logic.
- Solving every future BEPU feature now.

## Supported Physics Scope

The minimal slice must support these authored runtime behaviors:

- static box colliders
- dynamic box colliders
- kinematic box colliders
- static sphere colliders
- dynamic sphere colliders
- kinematic sphere colliders
- gravity
- authored linear and angular velocity synchronization
- collision filtering using the existing Helengine collidable-property bridge
- contact generation and solver support for:
  - box-box
  - sphere-sphere
  - box-sphere

The minimal slice does not need to support:

- meshes
- compounds
- convex hulls
- capsules
- sweeps
- reduction utilities
- character-controller-specific BEPU features
- unused constraint families beyond what the current rigid-body/contact solve path requires

## Architecture

### 1. Keep the Helengine adapter stable

`helengine.bepu` already exposes the correct engine-facing direction:

- `BepuPhysicsWorld3D`
- `BepuShapeFactory3D`
- `BepuBodyHandle3D`
- `BepuEntitySynchronization3D`
- `HelengineBepuNarrowPhaseCallbacks`
- `HelengineBepuPoseIntegratorCallbacks`

That layer should remain the stable bridge. The pruning work belongs below it, in the vendored BEPU source graph and project structure, not in new engine-side hacks.

### 2. Introduce a minimal vendored BEPU project slice

The preferred shape is a new vendored project layout that keeps real BEPU source but excludes unrelated subsystems from compilation. This slice should be created inside the vendored BEPU area rather than scattering exclusions across Helengine projects.

Recommended structure:

- keep `BepuUtilities` only as far as required by the minimal physics slice
- create a reduced `BepuPhysics` project, or a sibling minimal project, that includes only:
  - core simulation orchestration needed by `Simulation.Create(...)`
  - bodies/statics handling
  - shape registration for box and sphere collidables
  - broad-phase and narrow-phase pieces actually required by those shapes
  - solver/contact path required for box/sphere pair solving
  - trees or supporting utilities only when they are direct dependencies of the above

The goal is not to fork behavior. The goal is to fork compile surface.

### 3. Prune by project membership, not by codegen special casing

The immediate problem arose because native codegen traversed the entire upstream BEPU project graph. The correct fix is to reduce the source graph that codegen sees.

Pruning should happen through project/file inclusion boundaries, not through Helengine-specific hardcoding in `csharpcodegen`.

This keeps `csharpcodegen` generic and keeps the pruning decision owned by the engine/vendor integration layer.

### 4. Keep later expansion straightforward

The minimal slice should be organized so later support can be added by widening project membership in controlled increments:

- box/sphere only first
- then capsules or additional convex shapes
- then more complex narrow-phase pairs
- then compound/mesh families only when actually needed

This means the file/project structure for the minimal slice should reflect dependency boundaries clearly instead of becoming a pile of one-off exclusions.

## Pruning Strategy

### Preferred strategy

Create a reduced vendored BEPU project that compiles only the required upstream files.

Why this is preferred:

- `csharpcodegen` sees only the intended minimal graph
- Windows native build surface becomes much smaller
- DS-target portability pressure is reduced because fewer advanced numerics/intrinsics paths are active
- later expansion remains explicit and reviewable

### Rejected strategy

Do not solve this by adding BEPU-specific remap/hack logic inside `csharpcodegen`.

Reason:

- `csharpcodegen` is a generic project
- the user explicitly rejected engine-specific hardcoding there
- project-graph pruning solves the root cause more directly

## DS Portability Constraint

The completed converter/runtime work must remain useful for low-end platforms, including DS-class targets.

That means:

- avoid introducing new x86-only assumptions as part of the minimal slice
- keep SIMD/intrinsics wrappers portable and fallback-friendly
- prefer compile-surface reduction over “implement every advanced upstream runtime helper now”
- treat unsupported advanced BEPU subsystems as pruned, not as mandatory work for the immediate milestone

The DS requirement does not mean the full upstream BEPU library must compile today on DS. It means the converter/runtime direction and the immediate minimal slice must stay compatible with that eventual target class.

## Validation Plan

The reduced slice must be validated in this order:

1. `helengine.bepu` unit tests stay green or are updated narrowly to match the reduced real-BEPU project structure.
2. Native codegen repro for `helengine.physics3d` completes against the reduced BEPU graph.
3. Windows city build succeeds for the direct-start package targeting `test_scene_dynamic_stack_boxes`.
4. Launch the produced Windows package and visually confirm the stack topples.
5. Validate `test_scene_dynamic_sphere_stack`.
6. Validate `test_scene_dynamic_mixed_stack` so `box-sphere` contact support is confirmed.
7. Restore `C:\dev\helprojs\city\user_settings\build_config.json` to the normal multi-scene menu configuration.

## Risks

### Hidden transitive dependencies

The minimal box/sphere path may still require more solver, broad-phase, or tree infrastructure than it appears to at first glance. This is acceptable as long as the dependency set remains real and intentionally included.

### Over-pruning

It is possible to remove files that look unrelated but are still required by the minimal contact pipeline. The pruning pass should therefore be guided by compile evidence and direct dependency tracing, not by guesswork.

### Divergence from upstream layout

Any minimal vendored project introduces some maintenance overhead versus the upstream project file. That is acceptable because the alternative is blocking the shipping target behind full-library native support.

## Success Criteria

This design is successful when:

- the active vendored BEPU project graph only covers the real box/sphere rigid-body path
- `csharpcodegen` no longer needs to traverse unrelated mesh/compound/reduction/sweep systems to build the city physics runtime
- the Windows direct-start package for `test_scene_dynamic_stack_boxes` builds and launches successfully
- the same slice is structurally suitable for later DS-target work
- the previously completed generic converter/runtime improvements remain available for future broader BEPU expansion

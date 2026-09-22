# Plan: HelPhysics sphere shapes and trigger contacts

## Ownership

Luna physics agent: shape geometry/storage, body description/state, broadphase, collision dispatch and low-level world stepping; matching geometry/collision/world tests. Coordinate world public APIs with the scene agent. Do not edit scene binder/synchronizer/registration files.

## Steps

1. Read the existing box vertical slice, storage invariants, manifold cache and solver contract. Preserve existing box constructor/API compatibility.
2. Introduce an explicit shape kind and sphere representation. Store validated radius, compute bounds and dynamic inverse inertia, and keep fixed-capacity storage and handle-generation guarantees.
3. Implement sphere/sphere and sphere/oriented-box contact generation in the existing manifold convention. Handle coincident centers, sphere centers inside boxes, touching contacts and reversed order deterministically. Route only physical contacts to the solver.
4. Add explicit trigger metadata and persistent overlap tracking. Generate enter/stay/exit transitions using stable body generations, collision masks and shape geometry. Remove stale overlap state on destruction; trigger pairs must never enter contact solving or artificially wake/sleep physical islands.
5. Expose enough body-pair event/query information for scene binding and controllers without introducing engine entity ownership into numeric storage. Agree these interfaces with the scene agent before implementation.
6. Add focused tests for sphere settling/contact normals, rotated boxes, filtering, trigger transitions/removal and box regressions. Check existing allocation expectations and generated-code constraints.

## Acceptance

Box tests remain valid; sphere contacts use the existing material and solver semantics; trigger-only overlaps have no physical response; world capacity failures stay explicit and deterministic. Report exact tests and API changes to the coordinator and scene agent.

## Implemented and validated

Luna implemented sphere geometry/storage, sphere/sphere and oriented sphere/box contacts, trigger enter/stay/exit tracking, filtering without response, and generation/world-safe body-handle equality and hashing. Native-compatible trigger storage uses a reusable read-only view over preallocated storage.

The final non-benchmark HelPhysics suite passed 296/296 tests. Standalone generated C++ passed ownership validation and MSVC compilation (final-7). See the master plan for artifact locations and remaining whole-game integration validation.
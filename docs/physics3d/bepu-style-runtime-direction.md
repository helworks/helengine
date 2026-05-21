# BEPU-Style Physics Runtime Direction

The engine does not vendor BEPUphysics2 directly. The physics runtime will instead borrow stable internal ideas from `C:\dev\helworks\reference\physics\bepuphysics2` while keeping helengine component serialization and runtime feature stripping.

## Adopted Ideas

- Runtime handles identify bodies and shapes.
- Shapes are stored independently from bodies and can be reused.
- World stepping is split into clear stages: synchronize, sleep, broadphase, narrow phase, solve, integrate, synchronize back.
- Contact generation produces manifolds rather than implicit single-point response.
- Sleep uses activity history, not one-frame velocity checks.
- Stability tuning uses fixed timestep, solver iterations, and substeps.

## Non-Goals

- Do not import BEPU as a runtime dependency.
- Do not expose BEPU-specific APIs in public helengine components.
- Do not rewrite every collider pair in one change.
- Do not introduce unsafe or pinned-memory buffer pools until the simpler object-backed architecture is stable.

## Migration Order

1. Runtime handles.
2. Shape store.
3. Activity-based sleeping.
4. Box-box contact manifold.
5. Solver substeps.
6. Narrow phase boundary.
7. Stability scenario tests.
8. Optional packed storage after behavior is stable.

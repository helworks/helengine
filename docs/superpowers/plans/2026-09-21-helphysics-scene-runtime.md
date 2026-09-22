# Plan: HelPhysics scene lifecycle and character controllers

## Ownership

Luna scene agent: scene binder, entity binding/synchronizer/lifecycle, runtime adapter/factory and character controller implementation; focused scene/controller tests. Coordinate low-level APIs with the physics agent and registration APIs with the build agent. Avoid editing low-level world/shape files without explicit coordination.

## Steps

1. Read the engine runtime, scene-binding, body-synchronization and trigger-event contracts plus the existing legacy controller semantics. Inspect DemoDisc authored controller/trigger components to establish actual requirements.
2. Generalize scene binding to supported box/sphere rigid bodies with trigger metadata. Preserve full-hierarchy preflight, exact component identity, transform/scale validation, runtime authored updates and deterministic teardown.
3. Implement the normal scene-bindable runtime boundary with clear world ownership, scene load/unload, fixed-step scheduling, pause behavior, disposal and runtime body synchronization. Map low-level trigger pair transitions to existing engine events/observers.
4. Implement DemoDisc's box character controller behavior using HelPhysics collision/support data: authored desired movement, gravity, grounded state, slope limits, step height and ground snapping; account for moving support where required by the existing contract. Do not delegate simulation to the legacy world or silently ignore controller components.
5. Test scene replacement/removal, trigger delivery and unsubscription, sphere binding/scale changes, controller floor/slope/step behavior and synchronization after authored teleports.
6. Provide the build agent a concrete runtime creation/registration interface, and report any actual static mesh/capsule requirement rather than guessing from scene names.

## Acceptance

Existing serialized components remain valid. Runtime interfaces are honored, unsupported configurations fail explicitly before partial binding, and DemoDisc controller/trigger behavior is covered by meaningful tests.

## Implemented and validated

Luna implemented box/sphere scene binding, box character controllers without rigid bodies, trigger translation, dynamic body synchronization, transactional scene replacement and deterministic disposal. The runtime uses the core fixed-step scheduler and scene-sized capacities.

Controller regressions cover DemoDisc's authored step/ramp dimensions, rotated support bounds, X/Z/diagonal wall blocking, and validation of an entire controller batch before mutation. The final non-benchmark HelPhysics suite passed 296/296 tests; see the master plan for reports and remaining whole-game integration validation.
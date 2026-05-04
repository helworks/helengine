# Console Physics Architecture Design

## Summary

This design introduces a console-first physics architecture for Helengine that can scale from very constrained platforms up through Xbox 360-class hardware without forcing one heavy runtime shape onto every target.

The core decision is to keep physics out of `helengine.core` and ship it as optional runtime libraries:

- `helengine.physics3d`
- `helengine.physics2d`

`Physics3D` is the real first implementation target.
`Physics2D` is only scaffolded architecturally for now so 2D-only platforms and future handheld/retro targets are not forced into a 3D runtime dependency.

The design targets medium rigid-body physics first:

- static, kinematic, and dynamic bodies
- primitive colliders
- cooked static world mesh collision
- first-class character controller
- fixed-step simulation
- project-level physics defaults with scene-level overrides

The design does not try to deliver high-end general-purpose simulation on day one.
It deliberately prioritizes:

- stable world collision
- predictable CPU cost
- bounded memory use
- kinematic-heavy gameplay on weak platforms
- extension seams for future joints, CCD, and ragdolls on stronger platforms

## Problem Statement

Helengine currently has no dedicated physics runtime, but it already has the pieces that make physics integration sensitive:

- `Entity` owns transform state in [`Entity.cs`](C:/dev/helworks/helengine/engine/helengine.core/Entity.cs)
- the engine update loop is centralized in [`Core.cs`](C:/dev/helworks/helengine/engine/helengine.core/Core.cs)
- components participate through [`Component.cs`](C:/dev/helworks/helengine/engine/helengine.core/Component.cs) and [`UpdateComponent.cs`](C:/dev/helworks/helengine/engine/helengine.core/components/UpdateComponent.cs)

The problem is not simply "add rigid bodies."
The real problem is that Helengine targets hardware with radically different constraints:

- PS1
- N64
- DS
- PS2
- GameCube
- original Xbox
- Xbox 360

That means the physics system cannot assume:

- abundant RAM
- abundant CPU
- one universal broadphase
- one universal numeric mode
- one always-loaded 2D and 3D runtime
- one collision representation for every world

At the same time, Helengine should not split into separate gameplay-facing physics APIs for weak and strong platforms.
The engine needs one coherent authoring model with multiple internal runtime profiles.

## Goals

- Keep physics outside `helengine.core` and make it optional.
- Allow games to ship with:
  - no physics
  - only `helengine.physics2d`
  - only `helengine.physics3d`
  - both
  - a completely custom physics runtime
- Build a real `Physics3D` target first.
- Keep `Physics2D` architecturally parallel, but implementation-light for now.
- Support medium rigid-body physics first:
  - rigid bodies
  - primitive colliders
  - cooked static world collision
  - trigger volumes
  - character controllers
- Keep the simulation fixed-step.
- Support same-platform, same-build reproducibility over bounded replays.
- Allow future joints, continuous collision detection, and ragdolls without baking them into the base runtime floor.
- Keep runtime state dense and allocation-light.
- Make platform/profile constraints explicit and enforceable at build time.
- Provide exportable scenario scenes that double as end-to-end physics demos on real hardware.
- Add a generic per-platform property override system that physics can use immediately and other systems can reuse later.

## Non-Goals

- Deliver a fully featured general-purpose desktop-style physics engine in the first version.
- Promise cross-platform lockstep determinism.
- Support dynamic triangle-mesh rigid bodies in the first version.
- Build ragdolls or joints in the first version.
- Make 2D and 3D share one merged runtime core at the cost of code size and load-time separation.
- Put the solver inside `helengine.core`.
- Require every component to implement custom editor code to expose per-platform overrides.

## Design Principles

### 1. Keep Physics Optional

Physics should be pluggable into the engine lifecycle, not fused into the core runtime.

### 2. Separate Authoring From Simulation

Components are for authoring and scene integration.
Runtime simulation should use dense records, arrays, handles, and explicit pools.

### 3. Optimize For Weak Platforms First

The baseline design should assume tight memory and CPU budgets.
Higher-end targets can loosen ceilings later.

### 4. Prefer Kinematic Gameplay On Weak Targets

The system should support dynamic bodies, but the architecture should not assume every gameplay object is fully simulated.
`Kinematic` should be the normal path for constrained platforms.

### 5. Use Fixed-Step Simulation

The first architecture should be built around fixed-step only.
Interpolation can be layered later if needed.

### 6. Make Static World Collision Cooked And Platform-Aware

Shipping runtime builds should not reconstruct heavy collision structures from raw scene meshes.

### 7. Treat Character Motion As First-Class

A stable controller path is not the same thing as a tuned rigid body.
Character traversal should be its own supported model.

### 8. Fail Early At Build Time

When a platform cannot support a scene's physics content, the build/cook pipeline should reject it instead of hoping runtime survives.

## Library Layout

### `helengine.core`

`helengine.core` keeps:

- `Entity`
- `Component`
- scene/runtime lifecycle
- transform ownership
- update loop integration
- physics hosting contracts

It must not contain:

- rigid-body solver logic
- broadphase logic
- narrowphase logic
- static mesh collision runtime
- physics-specific platform backends

### `helengine.physics3d`

`helengine.physics3d` contains:

- 3D world runtime
- rigid body runtime
- primitive collider runtime
- character controller runtime
- static mesh collision runtime
- broadphase implementations
- solver implementations
- profile-driven limits and feature gating

This is the real first physics subsystem.

### `helengine.physics2d`

`helengine.physics2d` contains:

- 2D-facing component contracts and world shape
- placeholder architecture for later implementation

It should not be required by 3D games.

### Custom Physics Runtimes

Helengine users must be free to ship their own physics engine.
That means Helengine's default physics packages should implement engine contracts instead of becoming the only possible runtime.

## Core Integration Contract

`helengine.core` should define small hosting contracts such as:

- `IPhysicsRuntime`
- `IPhysicsWorldHost`
- `IPhysicsSceneBinding`
- `IPhysicsTransformSyncPolicy`

The exact type names can change, but the boundaries should remain:

- core hosts physics
- core does not solve physics
- core owns entity transforms
- physics writes and reads transforms through a controlled sync path

This allows the following build shapes:

- no physics runtime present
- only 2D runtime present
- only 3D runtime present
- both runtimes present
- custom runtime present

## Authoring Model

### 3D Components

The first real authoring set should include:

- `RigidBody3DComponent`
- `BoxCollider3DComponent`
- `SphereCollider3DComponent`
- `CapsuleCollider3DComponent`
- `MeshCollider3DComponent`
- `CharacterController3DComponent`
- `PhysicsMaterial3DAsset`

### Body Kinds

The runtime must support three body kinds:

- `Static`
- `Kinematic`
- `Dynamic`

#### `Static`

- not solver-driven
- typically level pieces and immobile authored collision

#### `Kinematic`

- moved by gameplay code or animation
- can push and interact with dynamic bodies
- not fully driven by forces
- expected to be heavily used on low-end consoles

#### `Dynamic`

- solver-driven
- participates in forces, impulses, mass, sleep, and contact resolution

### Collider Rules

Colliders should be independent components that can be attached under one body.
Each collider should carry:

- local offset
- local rotation
- collision layer/filter
- material reference
- trigger flag
- enabled flag

### Mesh Collider Rule

Static world mesh collision is allowed in the first version, but it must be cooked offline.
Dynamic triangle-mesh collision is out of scope for the first version.

## Runtime Simulation Model

Simulation must not operate directly on component objects.
The runtime should maintain compact state arrays or pools for:

- body records
- collider records
- broadphase proxies
- contact manifolds
- trigger pairs
- character controller records
- sleep/island data

Components should reference runtime state through stable handles, not raw pointers or direct object graphs.

This is required for:

- memory predictability
- codegen friendliness
- cache behavior on weak consoles
- easier runtime limits enforcement

## Transform Ownership And Sync

`Entity` remains the transform owner in core.
Physics should synchronize with `Entity`, not replace it.

The ownership rules should be:

- `Static`
  - authored transform registers into physics once or on explicit change
- `Kinematic`
  - gameplay writes transform into entity
  - sync layer pushes that transform into physics
- `Dynamic`
  - physics solves motion
  - sync layer writes solved transform back to entity after the step
- `CharacterController3D`
  - controller owns motion resolution
  - controller writes resulting transform back through the same controlled path

Gameplay should never observe half-solved state during the step.

## Simulation Pipeline

`Physics3D` should run through a fixed-step world update hosted by core.

Per simulation step:

1. Collect external writes
- ingest component changes
- push kinematic transforms
- process body/collider registration changes

2. Update broadphase
- update dynamic and kinematic proxies
- generate dynamic body candidate pairs
- keep static world queries on their own path

3. Run character controller pass
- perform controller sweep/move logic
- handle step offsets, slope rules, ground snap, and ceiling cases

4. Run narrowphase
- primitive-vs-primitive contact generation
- primitive-vs-static-mesh contact generation
- trigger overlap generation
- contact manifold caching/update

5. Solve
- integrate forces
- resolve contacts iteratively
- apply friction and restitution
- apply positional correction / penetration stabilization

6. Sleep and cleanup
- update sleep state
- clear stale contacts
- reset one-shot accumulators

7. Write back
- sync solved transforms to entities
- queue trigger and collision events for gameplay consumption

## Broadphase Strategy

The broadphase must be configurable because the engine cannot assume one content style.

Supported design:

- project/platform default broadphase
- optional scene/world override

The initial architecture should support broadphase strategies behind a shared interface, such as:

- `UniformGrid`
- `SweepAndPrune`
- later `DynamicBvh`

This allows:

- grid-oriented corridor/platform scenes
- more open arena-style scenes
- future stronger-target backends

The first implementation bias should still favor simple, predictable structures suitable for console hardware, but the API must not hardcode one world assumption.

## Static World Collision

Static world collision should be cooked offline into platform-ready data.

Runtime builds on consoles should consume cooked collision assets, not raw scene meshes.

### Why Offline Cooking

- smaller runtime CPU cost
- more control over memory layout
- platform-specific data layout
- build-time validation of unsupported content

### Octree Guidance

The old Nucleus octree is useful as a behavioral reference, especially its world-collision response patterns.
However, it should not be ported directly as the universal Helengine storage model.

The main reason is that the Nucleus builder aggressively splits triangles and duplicates geometry across leaves, which is risky for low-memory targets.

Helengine should instead prefer:

- shared triangle storage
- compact node/index references
- cooked backend-specific storage layouts

This still leaves room for an octree-style 3D world backend on stronger targets, but it avoids locking the weakest platforms into a duplication-heavy layout.

## Character Controller

`CharacterController3D` should be a first-class runtime path, not just a tuned rigid body.

It should support:

- sweep-based movement
- slope limits
- step offsets
- ground snapping
- ceiling checks
- stable moving-platform interaction

This controller path should be implemented before fancy dynamic-body features because it is more important for real console gameplay.

## Numeric Model

The public authoring API should remain float-oriented.

The internal architecture should allow lower-end backends to adopt quantized or fixed internal math later.
That means the design should not assume:

- one numeric representation everywhere
- full cross-platform determinism

The determinism target is:

- same platform
- same build
- same fixed-step inputs
- same bounded replay output

## Physics Profiles

Physics must have explicit runtime profiles.

Suggested profile tiers:

- `VeryLow`
- `Low`
- `Medium`
- `High`

### `VeryLow`

Targets:

- PS1
- DS
- future GBA/SNES-style ambitions

Bias:

- kinematic-heavy gameplay
- low dynamic body counts
- primitive-only dynamic collision
- very strict limits on active contacts and solver iterations

### `Low`

Targets:

- N64
- upper-end constrained legacy targets

Bias:

- limited dynamic interaction
- conservative static mesh collision usage
- no joints or CCD initially

### `Medium`

Targets:

- PS2
- GameCube

Bias:

- main first-version shipping target
- static mesh world collision
- dynamic primitive-vs-primitive collision
- stronger controller behavior

### `High`

Targets:

- Xbox
- Xbox 360

Bias:

- more active bodies
- looser ceilings
- likely first place for future joints, ragdolls, and CCD

### What Profiles Control

Profiles should control:

- max active bodies
- max colliders per body
- max broadphase pairs
- max contact manifolds
- solver iterations
- sleep thresholds
- allowed shape types
- allowed broadphase strategies
- allowed static collision backends
- future feature gates for joints and CCD

## Per-Platform Property Overrides

Per-platform overrides should be a core data-model feature, not a physics-only trick.

### Requirement

Components should be able to mark selected properties as per-platform overridable without rewriting custom editor UIs for each component.

### Design

- base component value remains canonical
- selected properties are marked as overridable through stable metadata
- editor discovers overrideable properties generically
- platform override UI is rendered from metadata
- effective value resolves at build/runtime:
  - override if present
  - otherwise base value

### Storage Model

Use a hybrid override model:

- scene/component instance stores:
  - base value
  - optional override binding/reference for the field
- platform override store stores:
  - target platform/profile id
  - object/component identity
  - stable property id/path
  - override value

### Why This Matters For Physics

Physics can immediately use this for:

- body kind downgrade
- gravity scale
- collider complexity
- broadphase choice
- solver iteration count
- sleep thresholds
- collision quality flags

The same system can later serve rendering, audio, materials, and platform-specific gameplay tuning.

## Build And Cook Validation

Physics content should be validated during build/cook.

Examples:

- too many dynamic bodies for the selected physics profile
- unsupported collider type on platform
- static collision asset exceeds profile memory budget
- unsupported broadphase override for platform
- future joints or CCD requested on a platform that forbids them

The weakest target should fail by content validation, not by undefined runtime degradation.

## Exportable Validation Scenes

Physics validation scenarios should exist as real scene assets that can be exported and run on consoles, not just internal tests.

Each scene should include:

- fixed or scripted camera framing
- deterministic reset/start behavior
- optional debug text for profile/backend
- clear observable pass/fail behavior

Recommended first set:

- `physics3d_character_slope`
- `physics3d_character_steps`
- `physics3d_character_moving_platform`
- `physics3d_dynamic_stack_boxes`
- `physics3d_dynamic_sphere_ramp`
- `physics3d_kinematic_push`
- `physics3d_mesh_ground_stability`
- `physics3d_trigger_volume`

Future stronger-target scenes:

- `physics3d_ragdoll_demo`
- `physics3d_joint_chain`

These scenes should double as:

- engine demos
- regression content
- profiling content
- real hardware end-to-end validation

## Testing Strategy

### 1. Logic And Unit Tests

- primitive overlap tests
- sweep tests
- contact manifold generation
- solver invariants
- sleep/wake transitions
- controller edge cases

### 2. Scenario Tests

- stack stability
- kinematic-vs-dynamic interaction
- static mesh traversal
- no-fall-through-ground regressions
- trigger behavior

### 3. Profile And Build Validation Tests

- profile limit enforcement
- cooked collision data compatibility
- per-platform override resolution
- unsupported feature rejection

### 4. Replay Stability Tests

Bounded deterministic replay tests should verify reproducibility within the same build/profile.

## Future Extensions

The architecture should intentionally leave seams for:

- joints
- continuous collision detection
- ragdolls
- stronger 2D runtime
- additional static collision backends
- additional broadphase strategies
- lower-end quantized/fixed internal math backends

Those features should attach to the architecture without changing the core hosting model.

## Recommended Implementation Direction

The first implementation plan should focus on:

1. physics hosting contracts in `helengine.core`
2. `helengine.physics3d` library skeleton
3. fixed-step world integration
4. body/collider authoring model
5. dense runtime records
6. primitive dynamic collision
7. cooked static world collision path
8. character controller
9. profile enforcement
10. per-platform overridable property metadata and generic editor integration
11. exportable validation scenes
12. `helengine.physics2d` placeholder structure

This sequence keeps the architecture console-safe while still giving Helengine a believable path to richer physics on stronger targets later.

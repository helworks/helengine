# Console-First Physics Engine Design

## Summary

HelEngine will introduce a new 3D rigid-body physics engine designed around the limits of PlayStation 2, Nintendo DS, Nintendo 3DS, GameCube, and Wii hardware. The engine will be authored in a restricted C# subset and converted to C++ through the existing generator. Its algorithms must remain efficient without SIMD; platform-specific numeric and vector kernels may optimize measured hotspots later.

The first version is deliberately narrow. It supports primitive rigid bodies, stable contact solving, triggers, kinematics, and aggressive island sleeping. It does not attempt BEPU feature parity. Static meshes, joints, vehicles, continuous collision detection, arbitrary convex hulls, and multithreaded solving are outside the first version.

The new implementation is greenfield. The dormant `PhysicsWorld3D` and BEPU integration are references for required behavior and testing, not implementation foundations.

## Motivation

BEPU is designed around wide vector processing, generalized constraint processors, type batching, modern caches, and optional parallel execution. The current C#-to-C++ runtime lowers `System.Numerics.Vector<T>` and hardware-intrinsic types into portable lane arrays and scalar loops. Its SSE, AVX, and AVX2 capability checks return false. A native compiler can recover occasional vector operations through auto-vectorization, but the physics engine cannot depend on that behavior.

This leaves older targets executing the control flow and data management of a modern wide-vector solver without receiving its intended SIMD benefit. The result is excessive fixed overhead and poor scaling for small console workloads. A four-box scene taking approximately 8 ms on PlayStation 2 demonstrates that the active architecture is unsuitable as the long-term shared runtime.

## Goals

- Deliver equivalent gameplay across platforms without requiring bit-identical cross-platform simulation.
- Remain efficient as scalar code on the weakest target.
- Make sleeping worlds nearly free.
- Scale work with active bodies, candidate pairs, manifolds, and contact points.
- Allocate all persistent and temporary simulation memory when the world is created.
- Keep the simulation single-threaded and deterministic on each individual platform.
- Preserve existing HelEngine scene components and runtime-facing physics interfaces.
- Allow build-time pruning of unused shape-pair implementations.
- Expose enough metrics to explain physics cost on real hardware.
- Establish a scalar baseline before introducing platform-specific acceleration.

## Non-Goals

- Static mesh collision.
- Joints or articulated bodies.
- Vehicles.
- Continuous collision detection or bullet guarantees.
- Arbitrary convex hulls.
- Soft bodies or cloth.
- Multithreaded simulation.
- Runtime-growing pools.
- BEPU API or behavioral parity.
- Bit-identical replays between different processor architectures.

## Supported Runtime Features

The first production version supports:

- Dynamic, kinematic, and static bodies.
- Box, sphere, and capsule shapes.
- Dynamic-dynamic and dynamic-static primitive collision.
- Gravity, damping, friction, restitution, and configurable materials.
- Persistent contact manifolds and warm starting.
- Trigger overlaps and enter/stay/exit events.
- Whole-island sleeping and waking.
- Character-controller support after primitive rigid bodies satisfy stability and performance gates.

Floors and walls use static boxes. Static spheres and capsules are permitted by the same primitive collision pipeline. Static-static pairs are never generated.

## Source and Platform Architecture

Restricted C# is the source of truth for the shared simulation algorithm. The step path uses explicit arrays, indexed pools, loops, and value types. It does not use `System.Numerics.Vector<T>`, general collections, delegates, virtual dispatch, exceptions, or heap allocation.

Internal mathematics use dedicated types:

- `PhysicsScalar`
- `PhysicsVector3`
- `PhysicsQuaternion`
- `PhysicsMatrix3x3`
- `PhysicsMath`

The C# reference implementation stores `PhysicsScalar` as a 32-bit floating-point value. Code generation substitutes the numeric layer by platform:

- Windows, PlayStation 2, GameCube, Wii, and Nintendo 3DS initially use native 32-bit floating point.
- Nintendo DS uses a saturating fixed-point scalar, initially 16.16, with wider intermediates for multiplication, division, accumulation, and geometric predicates.

The fixed-point format may be revised only from measured world-range and precision tests; simulation code must not assume its bit layout. Public engine values such as `float3` cross into dedicated physics types only at the world boundary.

Platform kernels may replace measured hotspots such as dot products, cross products, quaternion rotation, matrix operations, and reciprocal square root. The scalar implementation remains the correctness reference and must meet baseline performance requirements without those kernels.

## World Memory Model

The physics world owns fixed-capacity indexed pools for:

- Bodies.
- Shapes.
- Broadphase proxies and endpoints.
- Candidate pairs.
- Persistent manifolds and contact points.
- Islands and island graph edges.
- Solver constraints.
- Trigger-pair state and emitted events.
- Queued world commands.

Public handles contain a 16-bit pool index and a 16-bit generation. Destroyed slots enter a free list, and stale handles fail validation. Each pool has an explicit capacity supplied during world creation. The world calculates and reports its complete persistent and scratch-memory requirement before allocation.

One fixed scratch arena is reset after each step. Capacity exhaustion is a hard diagnostic failure and never causes silent contact, body, command, or event loss.

Body storage uses a hot/cold split. Hot state contains pose, linear and angular velocity, inverse mass, inverse inertia, damping, body mode, and sleep state. Cold state contains entity ownership, material identity, event flags, and authoring metadata. Shape data is separate from body state so solver loops do not load collision geometry.

The solver processes one real contact against its two bodies. It does not batch unrelated bodies into synthetic SIMD lanes.

## Broadphase

The initial broadphase is coherent one-axis sweep-and-prune:

- Every active primitive owns a conservative AABB and sorted interval endpoints.
- Endpoint order is maintained incrementally so coherent motion is inexpensive.
- The sweep emits dynamic-dynamic and dynamic-static candidates.
- Static-static candidates are rejected by construction.
- Sleeping-sleeping candidates do not enter the narrow phase.
- An active candidate touching a sleeping proxy schedules the sleeping island to wake.

A small velocity-dependent expansion and collision skin reduce ordinary tunnelling at low fixed-update rates. This is speculative contact support, not continuous collision detection, and does not guarantee extreme high-speed collision capture.

Alternative broadphases are not part of the first version. Capacity benchmarks will determine whether a later platform or game profile requires a uniform grid or another specialized implementation.

## Narrow Phase and Manifolds

Shape-pair dispatch occurs once through a compact shape-type table. Each supported pair calls a direct implementation:

- Box-box uses full oriented-box separating-axis tests followed by reference/incident face clipping.
- Sphere-sphere produces one contact from center distance.
- Sphere-box uses the closest point on an oriented box.
- Capsule-sphere uses the closest point on the capsule segment.
- Capsule-capsule uses closest points between line segments.
- Capsule-box tests the capsule segment against the expanded oriented box and handles endpoint and edge cases explicitly.

Each routine writes into one fixed manifold representation. A manifold contains no more than four contacts. Each contact stores:

- Local-space anchors on both bodies.
- World-space contact normal.
- Separation or penetration depth.
- Stable feature identifier.
- Accumulated normal impulse.
- Two accumulated tangent impulses.
- Previous-step lifetime.

Box-box manifolds may retain four contacts. Simpler shape pairs generally retain one or two. Contacts are matched between steps by feature identifier first and anchor proximity second. Matched contacts preserve accumulated impulses for warm starting. Contacts outside the persistence margin are removed in place.

## Solver

The engine uses a sequential-impulse velocity solver followed by a separate penetration-correction pass.

- Forces and gravity update active dynamic velocities.
- Cached impulses warm-start the current manifolds.
- Normal impulses are clamped so contacts may push bodies apart but never pull them together.
- Two tangent constraints model friction and are clamped by the accumulated normal impulse.
- Friction coefficients combine using their geometric mean.
- Restitution coefficients combine using their maximum.
- Restitution activates only above a configurable impact-speed threshold.
- A fixed number of sequential velocity iterations processes the island's contacts.
- Positional correction removes persistent overlap without adding kinetic energy or artificial bounce.
- Poses integrate after velocity solving and before final sleep evaluation.

The first version does not introduce a general constraint abstraction. Contact solving is implemented directly, because joints and other constraint families are out of scope.

## Fixed-Step Pipeline

Each fixed simulation step performs these phases in order:

1. Validate and apply queued body, shape, and material changes.
2. Read kinematic poses from their owning entities.
3. Update active and moved broadphase AABBs.
4. Maintain sweep-and-prune endpoint order and emit candidate pairs.
5. Wake sleeping islands touched by active candidates.
6. Run specialized narrow-phase routines and update persistent manifolds.
7. Build dynamic-body islands from active contact manifolds.
8. Integrate forces and gravity for active dynamic bodies.
9. Warm-start manifold impulses.
10. Perform sequential velocity iterations.
11. Perform penetration correction.
12. Integrate dynamic poses.
13. Evaluate whole islands for sleeping.
14. Update trigger-pair state and emit events.
15. Write dynamic poses back to entities.
16. Publish profiler counters and reset scratch memory.

Gameplay mutations enter a fixed command buffer and are applied only at the beginning of a step. Simulation callbacks cannot mutate the active solver structures in place.

## Islands and Sleeping

An island contains dynamic bodies connected by active contact manifolds. Static bodies never connect islands through one another, preventing independent stacks on the same floor from becoming one island. Kinematic bodies act as external drivers; their movement wakes affected dynamic bodies without merging unrelated dynamic groups through the kinematic body.

An island becomes eligible for sleep only when:

- Every body remains below configured linear and angular velocity thresholds.
- No body receives a force, impulse, teleport, body-mode change, mass change, shape change, or material change.
- No new energetic contact enters the island.
- Persistent contact anchors remain within the stability tolerance.
- All conditions remain true for the configured number of fixed steps.

The default console profile uses intentionally aggressive thresholds and approximately one quarter-second of stable low energy. When an island sleeps, all body velocities are explicitly zeroed and the entire island transitions together.

Sleeping islands are not integrated, narrow-phase tested against other sleeping islands, or solved. Their broadphase proxies and persistent manifolds remain available for wake detection and warm starting.

An island wakes as a unit when:

- A force or impulse targets any body.
- Gameplay moves any body directly.
- A moving kinematic body touches it.
- An active body creates a candidate pair with one of its proxies.
- A body changes mass, shape, material, or body mode.
- A retained contact separates beyond its persistence margin.

## Engine Integration

A new `HelPhysicsWorld3D` implements the existing physics runtime interfaces. Existing scene components and serialized scenes remain unchanged.

- Scene binding validates components and creates body and shape slots.
- Dynamic transforms are synchronized back after each completed step.
- Kinematic transforms are synchronized into physics before broadphase processing.
- Trigger events use the existing engine event interfaces.
- Build-time feature analysis includes only required shape-pair routines.

A box-only build must not contain capsule narrow-phase code. Static-mesh collision code does not exist in this version.

BEPU remains available temporarily as a comparison backend and test oracle. It is not part of the shipping path after the replacement gates pass.

## Validation and Failure Behavior

Body creation rejects invalid mass, dimensions, transforms, quaternions, material coefficients, and body-mode combinations. Debug and Profiler builds validate pool ownership, handle generations, manifold invariants, island membership, finite values, and fixed-point overflow.

Release builds remove expensive validation but preserve structural failure behavior. They do not silently discard bodies, contacts, commands, or trigger events.

Validation is divided into four layers:

1. Mathematical tests cover scalar, vector, quaternion, inertia, and fixed-point operations.
2. Collision tests verify expected separation axes, normals, depths, feature identifiers, and anchors.
3. Scenario tests cover gravity, bouncing, friction, stacking, sleeping, waking, kinematics, and triggers.
4. Generated-C++ parity tests compare C# and platform backends using platform-specific tolerances.

BEPU differential tests may detect major behavioral errors, but BEPU output is not the new engine's exact behavior contract.

## Profiling and Performance Gates

Every build can report:

- Total, dynamic, kinematic, static, awake, and sleeping body counts.
- Active and sleeping island counts.
- Broadphase candidate count.
- Active manifold and contact-point counts.
- Solver iteration count.
- Persistent and scratch-memory use.
- Timings for broadphase, narrow phase, island construction, solving, integration, sleeping, events, and synchronization.

Benchmarks sweep body count and contact density rather than assigning one theoretical maximum. Standard scenarios include sparse falling bodies, dense stacks, scattered sleeping bodies, active bodies striking sleeping islands, and trigger-heavy scenes.

The initial replacement gates are:

- Zero heap allocations during a simulation step.
- Stable visible behavior for the current four-box stack at 20 Hz.
- Reliable whole-island sleep and wake behavior.
- At least a twofold step-time improvement over generated BEPU on the same four-box workload and platform.
- No unreported capacity or numeric failure.

Per-platform capacity curves become shipping-profile inputs. Stronger platforms may support more simultaneous active bodies while preserving equivalent gameplay rules and features.

## Delivery Sequence

Implementation proceeds through independently testable vertical slices:

1. Numeric types, fixed pools, handles, and world lifecycle.
2. Static and dynamic axis-aligned boxes with gravity and discrete contact generation.
3. Oriented box-box SAT, manifolds, warm starting, and the sequential solver.
4. Islands, aggressive sleeping, waking, and profiler counters.
5. Sphere shape pairs.
6. Capsule shape pairs.
7. Kinematic bodies and trigger events.
8. Existing component integration and build-time feature pruning.
9. Generated Windows C++ parity and performance validation.
10. PlayStation 2 validation and platform-kernel profiling.
11. GameCube, Wii, and Nintendo 3DS validation.
12. Nintendo DS fixed-point backend and range validation.
13. Character-controller design and implementation as a separate approved feature.

Each slice must satisfy correctness, allocation, and instrumentation requirements before the next shape or platform is introduced.

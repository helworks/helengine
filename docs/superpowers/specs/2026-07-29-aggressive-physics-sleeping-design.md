# Aggressive Physics Sleeping

## Goal

Allow settled dynamic-body contact islands to sleep quickly on low-performance console targets while preserving reliable wake-up behavior.

## Scope

The initial change applies to the BEPU-backed 3D runtime. It replaces the current shape-derived BEPU sleep activity defaults with authored rigid-body sleep settings. It does not force-sleep bodies or change collision, solver, or fixed-step behavior.

## Data model

`RigidBody3DComponent` gains two authored settings for dynamic bodies:

- `SleepThreshold`: the BEPU combined linear-and-angular velocity-squared threshold below which a body can become a sleep candidate. The aggressive default is `0.5`.
- `SleepTicks`: the number of consecutive fixed steps under the threshold before a body becomes a sleep candidate. The aggressive default is `10`.

Static and kinematic bodies ignore both settings.

## Runtime behavior

When registering a dynamic body, `BepuPhysicsWorld3D` creates a `BodyActivityDescription` from the authored settings and passes it to BEPU's dynamic body description. BEPU continues to decide when an entire connected contact island sleeps.

A physics island is the set of dynamic bodies connected by contacts or constraints. A four-box stack is one island. Every member must meet BEPU's sleep requirements before the island can sleep.

BEPU wake-up behavior remains authoritative. Forces, impulses, nonzero velocity changes, teleports, and contact with awake bodies wake the applicable island. The normal simulation step does not explicitly set bodies awake.

## Diagnostics

The Windows profiler build exposes the existing awake-body counter and adds counters for sleep candidates and sleeping dynamic bodies. This enables tuning against 20 Hz / 1 iteration console-like workloads without inferring sleep state from visual motion.

## Validation

- Unit tests verify defaults, explicit serialized values, and dynamic-body activity registration.
- A focused stack-box test advances a quiet supported stack at 20 Hz / 1 iteration and verifies that its dynamic island sleeps within the configured tick window.
- Existing tests verify an impulse or velocity update wakes a sleeping dynamic body.
- A Windows profiler capture verifies awake bodies drop to zero after the stack settles and collision work correspondingly falls.

## Non-goals

- No forced sleep timeout.
- No platform-specific hidden wake or sleep behavior.
- No changes to generated C++ output outside the normal C# code-generation path.

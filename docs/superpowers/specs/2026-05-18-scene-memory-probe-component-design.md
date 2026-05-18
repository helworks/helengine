# Scene Memory Probe Component Design

## Summary

Add a generic runtime memory-stability harness to `helengine.core` named `SceneMemoryProbeComponent`.

The component is scene-authorable, uses the existing dynamic component system, and executes a fixed ordered list of authored steps. It exists to make memory behavior measurable under repeatable runtime conditions such as idle soaks, scene ping-pong, additive load/unload cycles, and longer-running baseline checks.

This is not an asset-fragmentation solution by itself. It is the engine-level harness we use to expose leaks, non-returning allocations, poor reclamation behavior, and future fragmentation symptoms reliably.

## Goals

- Provide one reusable runtime component that can drive repeatable scene-memory scenarios without bespoke host code.
- Keep the component generic so any project can author probe scenes with it.
- Avoid a custom serializer by staying inside the current dynamic component system.
- Use the lightweight scalar diagnostics path for steady-state sampling so the probe itself does not distort the measurement.
- Emit compact, stable runtime logs that make baseline comparison straightforward.

## Non-Goals

- Solving allocator fragmentation directly.
- Adding CSV export, custom files, or structured report artifacts in v1.
- Building a separate host-only automation harness.
- Supporting every possible scene action in v1.

## Recommended Approach

Implement one generic `SceneMemoryProbeComponent` in `helengine.core` plus one simple authored step class `SceneMemoryProbeStep`.

This is preferred over separate purpose-built soak components because it keeps the behavior centralized and reusable. It is preferred over a host-only harness because users need to author real runtime scenes that reproduce project behavior under the same scene and asset system used by shipping content.

## Component Model

### `SceneMemoryProbeComponent`

`SceneMemoryProbeComponent` derives from `UpdateComponent`.

It owns the authored probe script and the runtime execution state. It is responsible for:

- advancing the current step
- issuing scene actions at deterministic boundaries
- waiting for authored durations
- sampling memory and resource counters
- logging stable checkpoint lines
- optionally looping back to the beginning

### Authored Properties

The component should expose only dynamic-system-friendly properties in v1:

- `SceneMemoryProbeStep[] Steps`
- `bool Loop`
- `bool StartAutomatically`
- `double InitialDelaySeconds`
- `string ProbeName`

`StartAutomatically` allows the probe to begin without a separate trigger.

`InitialDelaySeconds` allows a brief startup warmup delay before the first measurement or transition. This is useful because process startup and first-frame renderer initialization are not representative of steady-state scene behavior.

`ProbeName` gives each emitted log line a stable logical source identifier.

### `SceneMemoryProbeStep`

`SceneMemoryProbeStep` is a simple data class so it can be authored through the current dynamic component system.

It exposes:

- `SceneMemoryProbeActionKind ActionKind`
- `string SceneId`
- `double DurationSeconds`
- `string Label`

`SceneId` is required for load and unload actions and ignored for wait steps.

`DurationSeconds` is used only by wait steps in v1. For non-wait actions it remains present for schema consistency but is not consumed.

`Label` is an optional authored identifier that appears in the runtime logs so humans can correlate probe phases without relying only on numeric step indexes.

### `SceneMemoryProbeActionKind`

Supported v1 actions:

- `Wait`
- `LoadSceneSingle`
- `LoadSceneAdditive`
- `UnloadScene`

These four actions are sufficient for the initial memory-stability cases:

- idle soak
- menu to scene ping-pong
- additive scene layering
- explicit unload validation

## Execution Model

The probe executes its steps in fixed authored order.

### Startup

When the component becomes active:

1. it enters an optional initial delay
2. it logs one initial baseline line
3. it begins processing the first authored step

### Wait Step

`Wait` consumes elapsed update time until `DurationSeconds` has fully elapsed.

The component logs:

- one line at step start
- one line at step completion

This gives a stable pair of measurements around every soak segment.

### Scene Action Steps

`LoadSceneSingle`, `LoadSceneAdditive`, and `UnloadScene` execute once when their step begins.

The component then advances to the next step on the next update pass. It should not busy-wait inside the same frame or recursively execute multiple authored steps in one update call.

The component logs:

- one line before issuing the action
- one line after the action has been issued

This keeps the transition boundary visible in logs while preserving predictable control flow.

### Completion

When the final step completes:

- if `Loop` is `false`, the component stops and logs completion
- if `Loop` is `true`, the component logs loop completion, resets its internal state, and restarts from step zero

## Measurement Contract

The probe must use the lightweight scalar diagnostics path introduced for steady-state runtime sampling. It must not rely on the rich snapshot path for its regular step logging because that would pollute the measurement and recreate the same class of bug we just removed from the menu diagnostics loop.

### Required Sample Data

Each log line should include:

- probe name
- current cycle index
- current step index
- step label
- action kind
- resident memory bytes
- committed memory bytes
- loaded scene ids
- 2D drawable count
- 3D drawable count
- last 3D draw-call count
- active owned texture count
- active owned font count
- active owned model count
- active owned material count

This is the minimum useful payload for distinguishing:

- genuine leaks
- scene ownership leaks
- non-returning asset references
- scene count mistakes
- drawables or render registrations that accumulate unexpectedly

### Optional Future Data

The design intentionally leaves room for later additions such as backend texture-resource counts, material buffer counts, or external allocator snapshots, but those are not required for v1.

## Logging Format

V1 should log through the existing runtime logging path rather than writing its own files.

Each emitted line should use a stable, grep-friendly prefix such as:

`[SceneMemoryProbe]`

Each line should include the probe name, cycle, step, label, action, and scalar counters in a flat key-value format.

The format should be designed for easy diffing between runs and easy filtering from host logs.

## Error Handling

The component should fail loudly on invalid authored state.

### Required Validation

- `Steps` must not be null or empty when the probe starts.
- `Wait` steps must have `DurationSeconds >= 0`.
- load and unload steps must provide a non-empty `SceneId`.
- unsupported enum values must throw.

### Runtime Failures

If `SceneManager` rejects a load or unload request, the component should let that failure surface rather than swallowing it. The purpose of the harness is to make invalid memory-transition scenarios obvious, not to keep running after bad authored inputs.

## Dynamic System Constraint

The component must remain compatible with the current dynamic component system.

That means:

- no custom serializer in v1
- no bespoke persistence descriptor in v1
- no special-case editor-only authoring pipeline

The design assumes the current dynamic path can author a simple nested step class with primitive, enum, and string properties. If that assumption proves false during implementation, the work should stop and the design should be revised explicitly rather than quietly degrading into parallel arrays.

## Testing Strategy

Implementation should be driven by tests.

### Core Runtime Tests

Add focused tests that verify:

- a probe with one wait step advances only after the authored duration
- a `LoadSceneSingle` step calls the scene manager once with `SceneLoadMode.Single`
- a `LoadSceneAdditive` step calls the scene manager once with `SceneLoadMode.Additive`
- an `UnloadScene` step calls unload once
- a completed probe loops when `Loop` is enabled
- the probe logs stable step boundary lines
- the probe uses the lightweight diagnostics path rather than the rich snapshot path for steady-state sampling

### Integration Use

After implementation, author at least one project scene that uses the probe to validate:

- idle menu soak
- menu to secondary-scene ping-pong
- additive load and unload cycle

## Success Criteria

The feature is successful when:

- a project can author a probe scene using only the dynamic component system
- the probe can drive fixed scene-transition scripts without custom host code
- probe logging is stable and easy to read
- the probe does not introduce obvious self-generated memory drift
- we can use the harness to distinguish startup warmup from true steady-state growth

## Risks

### Dynamic Authoring Risk

The most important implementation risk is the actual current capability of the dynamic component system for arrays of simple authored classes. This design depends on that working cleanly.

### Measurement Risk

If the probe samples through a rich object-allocating path, it will invalidate its own usefulness. The lightweight counter path is therefore part of the design contract, not an optimization detail.

### Misinterpretation Risk

The harness will expose memory behavior, but it will not classify every anomaly automatically. Users still need to interpret whether a plateau, a slowly rising high-water mark, or a non-returning unload baseline points to expected cache warmup, leak, or fragmentation symptoms.

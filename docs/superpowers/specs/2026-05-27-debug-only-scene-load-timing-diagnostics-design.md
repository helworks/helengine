# Debug-Only Scene Load Timing Diagnostics Design

## Goal

Add a generic engine-side diagnostics contract for scene-load timings that is available only in debug builds. Platform hosts such as `helengine-ps2` can attach a sink and display or persist the timing events, but the engine owns the timing boundaries and event emission.

This design is intentionally narrow:

- scene-load timings only
- debug builds only
- timing payloads only
- no scene ids, entity ids, component names, counts, or asset paths in this first pass

## Problem

Current runtime logging is too coarse to explain where scene-load time is being spent. Platform-side timing around `SceneManager->LoadScene(...)` can only show total time for large regions of work. That is not enough to separate:

- scene-manager orchestration
- scene-content fetch and deserialization
- runtime scene/entity/component construction
- hierarchy initialization
- final activation and registration

The current PS2 investigation exposed this limitation directly. We can see a large total stall, but we cannot attribute it precisely enough without adding more internal boundaries.

This should not be solved with PS2-specific engine code or by platform-side guessing around larger calls. The runtime itself should expose the timing boundaries.

## Non-Goals

- release-build diagnostics
- generic string logging in engine runtime
- asset-path, component-type, or entity-specific diagnostics payloads
- update, render, disposal, or asset-deserialize diagnostics outside scene load
- cross-platform persistence behavior

## Recommended Approach

Introduce a debug-only scene-load timing diagnostics interface in shared engine runtime code. Engine runtime code emits begin/end timing events for a small set of internal scene-load phases. Platform hosts attach a sink in debug builds and decide how to present the events.

This keeps responsibilities clean:

- engine defines timing boundaries
- engine emits structured timing events
- platform hosts choose display and storage behavior

## Alternatives Considered

### 1. Engine-owned formatted text logging

Rejected.

This would mix formatting and output policy into the runtime, make later consumers harder to support, and produce a weaker contract than structured timing callbacks.

### 2. Platform-side stopwatches around existing high-level calls

Rejected.

This is too coarse and is effectively the current failure mode. It cannot attribute time inside scene-load internals.

### 3. Broader diagnostics surface from the start

Rejected for this pass.

The current problem is scene-load attribution. Expanding immediately into render/update/disposal diagnostics increases scope without helping the current investigation enough to justify it.

## Design

### Debug-only compile gate

The diagnostics interface and all call sites must be compiled only in debug builds.

Requirements:

- release builds must not emit scene-load timing diagnostics
- release builds must not pay runtime cost for these diagnostics branches
- debug builds may attach a timing sink, but must still behave normally when no sink is attached

The exact compile symbol wiring should follow existing engine debug-build conventions rather than introducing a new ad hoc platform-specific switch.

### Engine contract

Add one shared runtime diagnostics interface dedicated to scene-load timing.

The interface should support:

- notifying phase begin
- notifying phase end
- including elapsed milliseconds for the completed phase

Payload must remain minimal in this pass:

- phase name
- elapsed milliseconds

The contract should be structured enough that platforms can:

- print the timings
- aggregate them
- persist them

without engine changes.

### Attachment point

The diagnostics sink should be attached through existing runtime initialization options, using the same general runtime diagnostics ownership pattern already used elsewhere in the engine. This keeps the new behavior aligned with current runtime configuration boundaries.

The engine must treat the sink as optional:

- debug build with sink attached: emit events
- debug build with no sink attached: no-op
- release build: compile the feature out

### Instrumentation boundaries

The first pass should instrument only the boundaries needed to explain current scene-load stalls.

Required phases:

1. `SceneManager.LoadSceneRequest`
2. `SceneManager.LoadSceneImmediate`
3. `SceneManager.BeforeContentLoad`
4. `SceneManager.SceneContentLoad`
5. `SceneManager.BeforeSceneLoadServiceLoad`
6. `RuntimeSceneLoadService.Load`
7. `RuntimeSceneLoadService.RootEntityLoadLoop`
8. `RuntimeSceneLoadService.InitializeHierarchyLoop`
9. `SceneManager.AfterSceneLoadServiceLoad`
10. `SceneManager.SceneActivation`
11. `SceneManager.SceneRegistrationComplete`

The final implementation may split or rename nearby phases slightly to match the current code structure, but it must preserve the same attribution value:

- scene-manager orchestration separated from content load
- content load separated from runtime scene construction
- runtime scene construction separated from hierarchy initialization
- post-load activation separated from raw load

### Timing semantics

Each phase should measure exclusive elapsed time for that phase boundary, not only cumulative time since the beginning of the full load.

The sink can derive cumulative totals if needed, but the engine should emit phase-local durations directly so the result is immediately useful in logs.

Timing source should use the engine/runtime timing facilities already present in the generated runtime path, rather than introducing a platform-specific clock source.

### Platform responsibilities

Platforms do not define the timing boundaries.

Platforms only:

- attach a sink in debug builds
- decide how to present the emitted timing events

For the current PS2 use case, `helengine-ps2` can keep printing the emitted scene-load timings to its existing boot diagnostics output. That is acceptable because the PS2 repo is only consuming the generic engine-side diagnostics contract.

## Expected Result

After this design is implemented, a debug PS2 build should be able to show a breakdown like:

- scene-manager immediate orchestration
- scene content load
- scene-load-service entity/component construction
- hierarchy initialization
- activation/registration

without adding PS2-specific scene-load instrumentation to engine runtime internals.

## Testing

### Engine tests

Add shared engine tests that verify:

- debug builds emit scene-load timing events in expected order
- emitted phase timings are attached to the correct boundaries
- runtime behavior remains correct when no sink is attached
- release build path does not depend on the diagnostics contract

Tests should focus on boundary coverage and event order, not on asserting exact millisecond values.

### PS2 validation

Add PS2-side contract validation that the boot host attaches the shared scene-load timing sink in debug diagnostics mode.

PS2 should not add its own engine-side timing boundaries.

## Risks

### Phase naming drift

If phase names are too tied to current implementation details, they may become unstable during runtime refactors.

Mitigation:

- use stable, coarse-but-useful phase names
- avoid naming phases after temporary helper methods unless those helpers are already stable boundaries

### Debug/release divergence

Debug-only compilation can drift if not tested carefully.

Mitigation:

- keep diagnostics attachment optional
- keep debug-only code isolated to the diagnostics interface and call sites
- add tests around the intended debug-only behavior

### Scope creep

It is tempting to add component ids, asset paths, or other metadata once the timing hook exists.

Mitigation:

- keep this pass timing-only
- treat richer payloads as a separate follow-up spec if still needed

## Implementation Notes

This change belongs primarily in the shared engine runtime and generated runtime boundary, not in `helengine-ps2`.

The PS2 repo should only need:

- sink attachment
- presentation of emitted timing events

No PS2-specific engine branch should be introduced to make scene-load timings work.

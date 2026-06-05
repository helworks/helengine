# BEPU Native vs Managed Differential Harness Design

## Summary

The current reduced-BEPU stack-box bug is no longer a broad rendering or scene-authoring problem. Managed C# produces the expected topple, while the native converted runtime pins the second support body and collapses the stack incorrectly.

The next debugging step should stop relying on ad hoc log inspection and instead build a deterministic differential harness that compares the managed and native reduced-BEPU paths at matching simulation boundaries, then reports the first divergence automatically.

This harness is a debugging tool for the reduced real-BEPU slice. It is not a gameplay feature and it is not a replacement solver.

## Problem Statement

Current evidence already establishes:

- reduced managed BEPU behaves correctly for the authored four-box stepped stack
- reduced native BEPU diverges before the stack visibly fails
- the native divergence happens before or during constrained-body preparation, not just at final scene synchronization
- repeated narrow logging has identified suspicious areas, but not yet the single first incorrect state transition

The current debugging loop is inefficient because each manual probe only reveals one nearby symptom. We need a tool that can compare the same scene across both runtimes and stop exactly where the state first disagrees.

## Goals

- Build a deterministic managed-vs-native differential harness for the reduced BEPU slice.
- Reuse the authored `test_scene_dynamic_stack_boxes` scene as the primary reproduction.
- Capture compact, comparable trace records from both runtimes at matching phases.
- Diff the traces automatically and stop at the first divergence.
- Make the harness reusable for later reduced-BEPU regressions and future DS-target debugging.

## Non-Goals

- Replacing the BEPU runtime.
- Expanding physics support beyond the reduced box/sphere slice.
- Solving every native BEPU conversion bug in one pass.
- Producing user-facing diagnostics in shipping builds.
- Depending on Windows-only APIs for the trace schema or comparison format.

## Constraints

- The reduced BEPU slice remains the source of truth for both managed and native runs.
- The trace format must be simple enough to emit from converted native code without introducing fragile formatting dependencies.
- The harness must compare semantic state, not renderer output.
- The approach must remain compatible with low-capability targets later, so the trace schema should stay text-based and portable.

## Recommended Approach

Implement a two-part differential system:

1. Managed golden trace generation
2. Native trace generation plus automatic diff

The system should produce phase-tagged records with a shared schema, then compare them frame-by-frame and body-by-body until the first mismatch is found.

This is preferred over more one-off probes because:

- it scales beyond the current stack-box case
- it removes guesswork about where the first divergence occurs
- it gives a stable regression tool once the bug is fixed

## Alternatives Considered

### 1. Continue adding targeted probes

Pros:

- lowest up-front code cost
- easy to land incrementally

Cons:

- already showing diminishing returns
- encourages local reasoning around symptoms instead of first divergence
- requires repeated rebuilds and manual log interpretation

### 2. Force all constrained bodies through one integration path temporarily

Pros:

- could quickly validate one hypothesis

Cons:

- changes the runtime behavior while debugging it
- may hide the real issue instead of locating it
- weaker as a reusable tool

### 3. Full native debugger stepping through generated code

Pros:

- maximal local visibility

Cons:

- generated code volume is too large and noisy for efficient first-divergence discovery
- difficult to keep both managed and native runs aligned by hand

## Harness Architecture

### 1. Shared trace schema

Use a compact line-oriented text schema with one record per phase observation.

Required fields:

- `frame`
- `phase`
- `body_handle`
- `body_index`
- `bundle_index` when relevant
- `constraint_batch` when relevant
- `type_batch` when relevant
- `body_slot` when relevant
- `encoded_refs` when relevant
- `integration_mask` when relevant
- `position`
- `orientation`
- `linear_velocity`
- `angular_velocity`

Not every phase needs every field, but the schema must stay regular enough for a single parser.

### 2. Initial phases to capture

Capture only the boundaries already known to be close to the bug:

- `integrate_velocity_callback`
- `integration_responsibility_assignment`
- `gather_and_integrate_before`
- `gather_and_integrate_after`
- `two_body_solve_before`
- `two_body_solve_after`
- `sync_snapshot`

This is intentionally narrower than "trace everything." The goal is to find the first mismatch quickly, not to dump the whole engine.

### 3. Managed golden trace

Extend the existing headless reduced-BEPU test path so it can emit a golden trace file for the four-box stack scene under deterministic settings.

Managed trace requirements:

- fixed timestep
- stable frame count, for example `0..120`
- deterministic body ordering by handle
- explicit scene identifier in the trace header

### 4. Native trace

Add a matching debug-trace mode to the native reduced-BEPU path.

Native trace requirements:

- emit the same schema as managed
- use the same frame numbering
- write to a stable package-local log file
- avoid expensive formatting that is fragile in generated C++

### 5. Diff tool

Implement a small comparison tool that:

- loads the managed golden trace
- loads the native trace
- groups by `frame + phase + body_handle`
- compares numeric values with small tolerances for float noise
- stops and reports the first mismatch

The output should identify:

- phase
- frame
- body handle
- field name
- managed value
- native value

## Scope of First Implementation

The first pass only needs to support:

- one scene: `test_scene_dynamic_stack_boxes`
- one body set of interest: handles `0..3`
- one native host target: current Windows package
- one managed headless target: existing `helengine.bepu.tests` path

This keeps the harness narrow enough to land quickly while still being reusable.

## Implementation Notes

### Managed side

Prefer extending the existing stack-box headless validation tests and diagnostics helpers rather than creating a separate standalone app.

### Native side

Prefer reusing the current BEPU diagnostics entry points, but refactor them toward a structured trace emitter instead of accumulating unrelated log snippets.

### Comparison format

Prefer plain UTF-8 text with one record per line over JSON. The emitted native path is already sensitive to runtime-support surface area, and line-oriented text is easier to keep portable.

## Validation Plan

Validation should happen in this order:

1. Managed golden trace emits successfully for the reduced stack-box scene.
2. Native trace emits successfully for the same scene.
3. The diff tool runs and reports at least one known current mismatch.
4. After the underlying bug is fixed, the same harness reports no divergence through the chosen frame range.

## Risks

### 1. Phase alignment mismatch

Managed and native may not currently expose exactly the same timing boundaries. The harness mitigates this by starting from already instrumented boundaries and tightening only as needed.

### 2. Log volume

Tracing every lane for every phase could become noisy. The first version mitigates this by restricting the scene, frame range, and tracked body handles.

### 3. Diagnostic code changing behavior

The tracing must remain read-only and avoid altering scheduling or solver decisions. Compact text emission and bounded snapshots reduce this risk.

## Recommendation

Proceed with the differential harness rather than more narrow manual probes.

The current debugging state is already specific enough that a first-divergence comparator should give much more leverage than another round of isolated logging. Once the first mismatch is mechanically identified, we can return to targeted fixes with far more confidence.

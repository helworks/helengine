# C++ Feature-Pruned Build Design

## Summary

`cs2.cpp` should support feature-pruned native builds so the generated C++ core only includes the engine subsystems required by the selected build. The design must support aggressive executable size reduction for constrained targets such as PlayStation 2, GameCube, Wii, and external package-owned console targets while still working for the initial Windows headless core.

The system must combine automatic detection with explicit operator control. A build profile can force-enable or force-disable features, and the conversion/build output must report the final decision set both in generated files and from inside the engine at runtime.

## Goals

- Remove unused engine subsystems from generated C++ output.
- Allow build profiles to force-enable or force-disable features.
- Produce deterministic reports showing which features were included and why.
- Expose the final feature set from inside the generated engine core.
- Keep the runtime footprint small enough for memory- and CPU-constrained hardware.

## Non-Goals

- Full linker-time dead-code elimination as the primary pruning mechanism.
- Reflection-heavy feature discovery at runtime.
- Per-member pruning in phase 1.
- Immediate support for every engine subsystem in the first iteration.

## Feature Decision Model

Each build feature uses a three-state mode:

- `Auto`
- `Enabled`
- `Disabled`

Resolution precedence is:

1. `Disabled`
2. `Enabled`
3. `Auto`

`Disabled` must win even when usage is detected. This is required so a build can intentionally strip a subsystem for size-constrained targets.

## Detection Sources

The final feature set must be derived from three input classes.

### 1. Explicit build profile

The build profile declares feature states and conflict policy. This is the authoritative operator input.

### 2. Scene and asset usage

The build process scans scenes and included assets to identify subsystem use. Examples:

- sprite components imply `Sprites`
- text/font usage implies `Text2D`
- shader assets or shader package references imply `Shaders`

### 3. Code and registration roots

The build process scans configured code roots and known registrations, including startup roots, content processors, and selected game/runtime roots. This is required because not all subsystem usage is visible from scenes.

## Feature Conflict Policy

A forced-disabled feature may still be referenced by selected content or code roots. That must not be silently accepted.

Each feature therefore has a conflict policy:

- `Warn`
- `Error`

Recommended default:

- use `Error` when the build cannot degrade cleanly without the feature
- use `Warn` only when a defined fallback exists

The build report must include every conflict, the roots that triggered it, and the final decision.

## Proposed `cs2.cpp` Additions

### `CPPFeatureKind`

Represents prunable engine capabilities. Phase 1 should cover:

- `Render2D`
- `Sprites`
- `Text2D`
- `Shaders`
- `DebugOverlay`

Later expansion can include:

- `UI2D`
- `Audio`
- `Render3D`
- `Physics2D`
- `Physics3D`

### `CPPFeatureMode`

Represents `Auto`, `Enabled`, or `Disabled`.

### `CPPFeatureConflictPolicy`

Represents `Warn` or `Error` for forced-disable conflicts.

### `CPPBuildFeatureProfile`

Added to `CPPConversionOptions`. It contains:

- per-feature mode
- per-feature conflict policy
- selected code roots
- included scene roots
- included asset roots

### `CPPBuildUsageReport`

Produced during conversion. It records:

- final feature decisions
- forced decisions
- auto-detected decisions
- inclusion roots per feature
- forced-disable conflicts
- warnings and errors
- reachable generated type counts by feature

## Conversion Pipeline Changes

### 1. Pre-scan phase

Before normal conversion, the pipeline scans build inputs and code roots to collect initial feature hits.

Outputs:

- detected features
- root-to-feature edges
- feature-to-feature implications

### 2. Feature resolution phase

The pipeline applies precedence and conflict policy to produce the final feature set.

Outputs:

- included features
- excluded features
- conflicts and diagnostics

### 3. Reachability phase

The pipeline starts from selected roots plus feature roots and walks only reachable engine types, members, and runtime requirements.

This replaces the whole-assembly emission mindset for build output.

### 4. Runtime requirement pruning

Runtime requirements must also become feature-aware. If a feature is excluded, its runtime helpers must not be emitted unless needed by another included feature.

### 5. Report and config emission

The pipeline emits:

- a machine-readable build usage report
- a generated feature config header
- a small runtime-accessible feature manifest

## Subsystem Tagging Strategy

Feature membership should be explicit near subsystem boundaries. The backend should not rely long-term on ad hoc name heuristics.

Phase 1 tagging can be based on known namespaces, root types, and asset families. Example mapping:

- `helengine.core.shaders.*` -> `Shaders`
- sprite rendering interfaces/components/assets -> `Sprites`
- font/text rendering roots -> `Text2D`
- debug overlay roots -> `DebugOverlay`

This can later be replaced or augmented with explicit metadata if needed.

## Reachability Rules

Pruning should occur primarily at type/module reachability, not only through preprocessor defines.

Reason:

- removing files and types yields more predictable binary size reduction
- relying only on defines still emits broader output and weakens the value of the feature graph

Defines are still useful for compile-time configuration, but they are secondary to pruning the generated set itself.

## Runtime Feature Visibility

The generated native core must expose a tiny static feature manifest.

Proposed API shape:

- `bool he_feature_enabled(HEFeature feature)`
- `const HEFeatureEntry* he_get_feature_entries(int* count)`

Each entry contains only:

- feature id
- final state
- decision origin:
  - `ForcedEnabled`
  - `ForcedDisabled`
  - `AutoDetected`
  - `NotIncluded`
- optional short reason identifier

Constraints:

- no reflection
- no dynamic allocation required
- static arrays only
- string payload should be removable or minimized in size-focused profiles

## Generated Build Outputs

The feature-pruning system should emit at least:

- `cpp-build-feature-report.json`
- generated feature config header
- generated native feature manifest source/header

The JSON report should include:

- build roots
- scene inputs
- asset inputs
- final feature states
- inclusion and exclusion reasons
- conflicts
- counts of reachable emitted types by feature

## Platform Relationship

This design belongs in the portable transpiled core and must remain platform-neutral.

Later platform runners such as `helengine-windows` may add their own platform feature roots, such as DirectX-specific or Vulkan-specific modules, but those must layer on top of the portable feature graph rather than replacing it.

## Recommended Phase 1 Scope

Phase 1 should implement the full plumbing for a small set of visible, size-relevant features:

- `Shaders`
- `Sprites`
- `Text2D`
- `Render2D`
- `DebugOverlay`

Recommended delivery order:

1. add feature domain models and report structures
2. add build profile parsing and resolution rules
3. add pre-scan and root-to-feature mapping
4. add reachability-pruned emission
5. add runtime feature manifest emission
6. prove pruning with `Shaders` and `Sprites`

## Success Criteria

The design is successful when:

- a build can force-disable `Shaders` and no shader subsystem output is generated
- a build can force-disable `Sprites` and no sprite subsystem output is generated
- the build emits a clear report explaining all enabled and disabled features
- the generated core can report feature state at runtime
- the pruning mechanism reduces emitted output before native compilation rather than depending only on linker stripping

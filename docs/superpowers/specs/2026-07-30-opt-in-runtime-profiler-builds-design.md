# Opt-In Runtime Profiler Builds

## Problem

The generic runtime profiler currently executes in ordinary generated builds. Physics-enabled runtimes create a `RuntimePhysicsProfilerMetrics` reference object each update, and constrained native targets such as PS2 do not provide managed garbage collection to reclaim those transient objects. The observed result is an exact 128-byte heap increase per update until PS2 cannot allocate a VU render packet.

Ordinary Debug builds must not contain or execute generic runtime profiling. Debuggability and profiling are separate build concerns.

## Decision

Runtime profiling will use a positive opt-in build feature. A build includes generic profiler code only when its selected codegen profile explicitly enables the stable `runtime_profiler` feature. The absence of that feature is the default for every platform and every existing build profile.

The existing forced-disabled feature mechanism is not suitable because every current and future platform would have to remember to disable profiling. A positive opt-in makes the safe, zero-overhead behavior structural.

## Build Configuration

The shared platform build system will support an explicit enabled-feature codegen setting alongside the existing disabled-feature setting. It will translate enabled feature identifiers into stable preprocessor symbols.

PS2 will expose two build profiles:

- `PS2 Default` uses the normal codegen profile and does not enable runtime profiling.
- `PS2 Profiling` uses a profiling codegen profile that explicitly enables `runtime_profiler`.

No existing project or platform selection silently becomes a profiling build.

## Generated Runtime Boundary

The generic profiler implementation and its integration points will be compiled only when the runtime-profiler symbol is present. This includes:

- core-owned runtime profiler state;
- per-frame profiler reset and counter collection;
- renderer and scene-operation profiler reporting;
- physics profiler metric queries and temporary samples;
- profiler snapshot APIs and their supporting data types.

Normal builds must not merely skip collection through a runtime boolean. The generated runtime must omit the profiler path so it has no frame-time cost, heap activity, or accidental platform dependency.

## PS2 Diagnostics

The PS2 host's hardcoded build number and native timing overlay remain independent of the generic profiler. They may continue to report PS2 frame, physics, 3D, and 2D timings in the ordinary diagnostic ISO because those values are captured directly by the host and renderer.

Generic runtime-profiler counters are available only in the explicit PS2 Profiling profile.

## Compatibility

Desktop editor diagnostics are not removed. Editor execution and explicit profiling builds can enable the feature. Existing ordinary platform builds retain their current gameplay and rendering behavior while excluding generic runtime profiling.

The profiling API remains source-compatible inside profiling builds. Code that directly depends on profiling APIs must itself be under the same build feature boundary.

## Validation

Tests will prove:

1. The shared build system emits the runtime-profiler preprocessor symbol only for explicit enabled-feature selections.
2. PS2 Default does not enable runtime profiling.
3. PS2 Profiling does enable runtime profiling.
4. Generated core output for PS2 Default contains no per-frame physics profiler metric query or runtime profiler reset.
5. Existing profiler behavior tests pass when the profiling feature is enabled.
6. A fresh full DemoDisc PS2 Default build boots Level 01 without `FPS N/A`, without VU packet allocation failure, and without steady 128-byte-per-update heap growth.

## Out of Scope

- Changing PS2 renderer batching or VU packet capacity.
- Redesigning the profiler's public counters.
- Changing Level 01 tessellation or authored assets.
- Hiding allocation failures or increasing heap limits.

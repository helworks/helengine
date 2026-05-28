# Helengine BEPU Replacement Design

## Goal

Replace active usage of `helengine.physics3d` with a new package named `helengine.bepu`.

The replacement target is one identical C# physics codebase that runs on every platform through the existing C# code generation pipeline, including DS. The design intentionally avoids another long solver-parity effort inside the current custom physics stack.

## Why Replace Instead Of Continue Tuning

The current custom solver has consumed too much iteration time for too little parity progress.

What we learned from the recent tower and stack work:

- the current stack can be improved incrementally,
- those improvements do not move fast enough toward BEPU behavior,
- the remaining gaps are in deeper contact and support behavior,
- continuing to tune `helengine.physics3d` is likely to keep producing local wins without giving us a trustworthy general rigid-body base.

The replacement direction is therefore:

- stop spending more time on solver-parity tuning in `helengine.physics3d`,
- move to a BEPU-backed runtime package,
- keep the platform story unified by staying in C# so the existing codegen path can still generate DS-native C++.

## Product Decision

The new package will be:

- `engine/helengine.bepu`

This package becomes the active rigid-body runtime used by Helengine scenes and builds once migration is complete.

`helengine.physics3d` will no longer be the runtime used by the project. It may remain in the repo temporarily during migration, but the design target is removal of active usage rather than indefinite dual-stack support.

## Scope Of The First Replacement Pass

The first pass is intentionally narrow.

Supported feature set:

- dynamic rigid bodies
- static rigid bodies
- box colliders
- sphere colliders
- box-box collision
- sphere-sphere collision
- box-sphere collision
- basic gravity
- transform synchronization between entities and runtime bodies
- sleeping behavior provided by the BEPU-backed runtime

Explicitly out of scope for the first pass:

- capsules
- static mesh collision
- joints
- continuous collision detection beyond what is strictly required for the first pass
- the current custom character-controller runtime
- direct parity work inside `helengine.physics3d`

This narrow scope is deliberate. The primary migration success criterion is not "support every old feature immediately." The success criterion is "ship one trustworthy cross-platform rigid-body stack for the scenes we actually need first."

## Architecture

### New runtime package

Add a new engine package:

- `engine/helengine.bepu`

Responsibilities:

- own BEPU world setup and stepping,
- own BEPU body creation and teardown,
- translate Helengine rigid-body and collider components into BEPU bodies and shapes,
- synchronize entity transforms into the runtime before stepping,
- synchronize runtime transforms back to entities after stepping,
- expose a Helengine-friendly runtime interface so higher layers do not become BEPU-specific.

### Public engine-facing shape

The engine-facing component model should remain as stable as possible during the replacement.

Existing scene authoring should continue to rely on concepts like:

- `RigidBody3DComponent`
- `BoxCollider3DComponent`
- `SphereCollider3DComponent`

Those components should feed the new runtime package instead of the current `helengine.physics3d` world.

This keeps the migration focused on runtime replacement, not on rewriting the entire authoring model.

### Runtime interface boundary

The replacement should preserve a small engine-owned abstraction boundary between scene authoring and the physics backend.

The boundary should cover:

- world creation
- scene bind and unbind
- fixed-step simulation
- body lookup and lifecycle
- entity-to-runtime synchronization
- runtime-to-entity synchronization

The boundary should not leak broad BEPU implementation details into unrelated scene or editor code.

### Platform model

There will be one physics implementation architecture and one solver code path:

- managed C# source in `helengine.bepu`
- converted to native code for DS through `csharpcodegen`

This is explicitly not a "same API, different physics per platform" design.

The runtime behavior should be defined by the shared BEPU-backed code, not by separate platform-specific backends.

## Migration Strategy

### Phase 1: establish the package

Create `helengine.bepu` with the minimum runtime world needed for:

- static ground,
- dynamic boxes,
- dynamic spheres,
- gravity,
- simple authored rigid-body scenes.

At the end of this phase, the package should compile and run under normal .NET, independent of DS packaging.

### Phase 2: prove codegen viability

Before broad scene migration, validate that the limited `helengine.bepu` subset survives the C#-to-C++ pipeline.

This phase is critical because the platform strategy depends on it. If the subset cannot be codegened cleanly, the replacement architecture fails and must be revised before deeper migration.

Required proof points:

- the package builds in managed form,
- generated C++ compiles,
- a minimal DS runtime scene with dynamic boxes or spheres executes successfully.

### Phase 3: wire active runtime usage

Switch the active engine integration from `helengine.physics3d` to `helengine.bepu` for the supported feature set.

That means:

- scene binding uses the new package,
- runtime stepping uses the new package,
- Windows builds use the new package,
- DS builds use the same generated code path.

Scenes using unsupported features should be excluded temporarily or rewritten to fit the supported box/sphere-only scope until later phases expand support.

### Phase 4: remove active dependency on `helengine.physics3d`

Once the supported runtime path is stable:

- stop routing any active scene/runtime flow through `helengine.physics3d`,
- remove or isolate remaining references,
- mark the old package as legacy until it can be deleted cleanly.

## Scene And Content Impact

Because the first pass supports only boxes and spheres, current project content must be evaluated against that boundary.

Expected content actions:

- keep box and sphere showcase scenes,
- keep physics scenes that only require supported shapes,
- temporarily exclude or defer scenes needing capsules or static mesh collision,
- avoid pretending unsupported scenes are still valid under the first replacement pass.

This is important because a partial runtime replacement is acceptable. Silent feature gaps are not.

## Testing Strategy

### Primary runtime gates

The first replacement pass should be validated with simple, high-signal rigid-body tests instead of broad feature claims.

Required automated coverage:

- dynamic box falling onto static box
- dynamic sphere falling onto static box
- dynamic box stack settling
- dynamic sphere stack settling
- box-sphere interaction
- transform synchronization correctness
- deterministic fixed-step stepping for the supported scenarios

### Cross-platform gates

Required platform verification:

- managed test execution on Windows
- Windows demo build and launch
- DS codegen build
- DS runtime launch with at least one supported physics scene

### Migration safety gates

We should also add source-audit or integration checks to confirm:

- active runtime wiring no longer points at `helengine.physics3d`,
- supported scene/component paths route into `helengine.bepu`,
- unsupported shapes fail clearly instead of silently degrading.

## Failure Handling

The design rejects "best-effort" migration behavior.

Examples:

- If a scene requests an unsupported collider type, fail explicitly.
- Do not silently fall back to the old runtime.
- Do not keep both physics stacks active behind opaque automatic switching.
- Do not add scene-specific hacks to make the replacement look more complete than it is.

Clear unsupported-state failure is better than a half-migrated runtime that behaves differently per platform.

## Risks

### Highest risk: codegen compatibility

The biggest technical risk is not BEPU itself. It is whether the chosen BEPU-backed subset can survive the Helengine C# code generation path for DS.

This is why codegen viability must be proven early, before broad scene migration.

### Feature regression risk

A box/sphere-only first pass means some existing physics scenarios will be unsupported for a while.

This is acceptable if the unsupported scope is explicit and managed. It is not acceptable if the migration claims full replacement before that is true.

### Integration churn risk

Replacing the runtime while preserving the existing authoring surface is still a significant integration change. The design therefore prefers a stable engine-facing abstraction rather than exposing BEPU concepts across unrelated code.

## Recommended Approach

Build `helengine.bepu` as the new active rigid-body runtime, scoped initially to boxes and spheres only, and migrate the engine to it through the existing component and fixed-step world interfaces.

This approach is preferred because it:

- stops the endless parity-tuning loop in the custom solver,
- keeps one cross-platform solver codebase,
- preserves the existing authoring model where practical,
- limits the first-pass scope to something we can actually validate on Windows and DS.

## Expected Outcome

After the first replacement pass:

- Helengine uses `helengine.bepu` for supported rigid-body scenes,
- Windows and DS share the same physics code path through managed source plus codegen,
- box and sphere scenes run on the new runtime,
- `helengine.physics3d` is no longer the active runtime dependency for supported content,
- unsupported features are explicit rather than half-working.

This is not the end-state for all future physics features. It is the smallest credible path to replacing the current solver with one shared BEPU-backed stack.

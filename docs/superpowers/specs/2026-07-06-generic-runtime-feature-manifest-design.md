# Generic Runtime Feature Manifest

**Date:** 2026-07-06

## Goal

Add one generic engine-wide runtime feature manifest system that lets HelEngine understand exactly which fine-grained runtime capabilities a build requires, where those requirements came from, and which capabilities a platform build may safely strip when the user explicitly opts out.

The first consumer is DS size reduction, but the seam must be generic enough for every platform to share.

## User Direction

- Runtime feature stripping must be generic engine-wide, not DS-specific.
- Feature units should be fine-grained runtime capabilities, not only coarse subsystem toggles.
- Features required by authored content must still be supported; the engine should not silently rewrite content into weaker forms just to save size.
- Size-driven stripping should happen only at the explicit direction of the user.
- Feature requirements must be derived from both serialized content and used code/plugin types.
- Code/plugin requirements should use a hybrid model:
  - C# attributes declare feature requirements close to the code that needs them.
  - plugin/runtime registration remains the seam that exposes which runtime types participate in the build.
- If a user disables a feature that the manifest proves is required, the build must fail.
- The build must produce dependency logging that shows what assets, types, or plugins pulled each feature in.

## Problem

HelEngine already has pieces of feature policy, but they are fragmented and too specialized.

Current behavior:

- platform codegen profiles can force-disable named features
- physics3d can analyze scene content into feature flags
- plugins and generated-core registration already participate in build-time type discovery

But today there is no single generic answer for these questions:

- which exact runtime capabilities does this build require?
- did a requirement come from authored scenes, materials, code, or a plugin?
- which user-disabled features are safe to remove?
- which user-disabled features would break the build and why?

That missing seam blocks the next size-reduction step. Without it, platforms either:

- keep too much runtime flexibility because the build cannot prove what is unused, or
- strip too aggressively and risk removing needed behavior.

## Non-Goals

- Silently rewrite authored content into weaker platform-specific forms.
- Automatically strip features without an explicit user opt-out.
- Introduce platform-named feature ids.
- Solve every existing size problem in the first pass.
- Replace existing platform codegen feature toggles in the same first pass.
- Require every plugin author to hand-assemble a separate manifest file.

## Existing Precedent

HelEngine already has one useful local pattern in physics3d:

- `PhysicsSceneFeatureAnalyzer3D` derives fine-grained requirements from serialized scene content.
- `PhysicsSceneFeatureSymbolCatalog3D` maps those requirements to compile-time symbols.

That proves the content-analysis idea works. The missing step is to generalize the model so feature requirements can come from any runtime domain, aggregate into one manifest, and drive validation plus stripping across all platforms.

## Decision

Adopt one generic runtime feature manifest system with these properties:

- feature ids are fine-grained and engine-owned
- feature requirements come from both content and code/plugin discovery
- user-disabled feature sets remain platform/build-policy inputs
- builds fail if any disabled feature is still required
- successful and failed builds can emit one dependency report artifact that explains what pulled each feature in

This makes DS the first consumer, but not a special case in the model.

## Feature Model

### Feature Ids

Feature ids are stable dotted strings that describe runtime capabilities rather than platform names.

Examples:

- `physics3d.box_box_contact`
- `physics3d.capsule_static_mesh_contact`
- `render3d.directional_light`
- `render3d.material.textured_lit`
- `runtime.code_modules`

Rules:

- ids must be platform-neutral
- ids must be stable enough to appear in authored configuration and build reports
- ids should be fine-grained enough that platforms can strip specific capabilities instead of whole subsystems when desired

### Required Feature Record

Each requirement in the manifest should carry:

- `FeatureId`
- `SourceKind`
- `SourceId`
- `Reason`

Examples:

- `FeatureId = render3d.material.textured_lit`
- `SourceKind = material`
- `SourceId = Materials/rendering/test/Cube00`
- `Reason = material schema requires textured lit 3D runtime path`

## Requirement Sources

### Content-Derived Requirements

Cook-time analyzers should emit required feature records from serialized authored content such as:

- scenes
- materials
- textures
- animation clips
- physics setups
- any future asset type whose runtime behavior is feature-sensitive

These analyzers should report what the content requires, not decide what the platform is allowed to drop.

### Code And Plugin-Derived Requirements

Code/plugin requirements should use the agreed hybrid seam:

- C# attributes declare feature requirements on runtime types and, when necessary, specific members.
- plugin/runtime registration identifies which runtime types are actually participating in the build.

The build then:

1. resolves the participating runtime types
2. reads their declared feature requirements
3. emits required feature records only for used types

This preserves the "only when runtime types are used" rule while keeping feature ownership close to the code that needs it.

## Build Flow

### Aggregation

The editor build graph should aggregate required feature records before platform packaging begins.

High-level flow:

1. content analyzers emit required feature records
2. code/plugin discovery emits required feature records
3. the editor unions them into one build feature manifest
4. platform/build settings contribute explicitly disabled feature ids
5. validation compares `disabled features` against `required features`
6. the build either:
   - fails with a dependency report, or
   - proceeds and allows downstream stripping to consume the surviving feature set

### Validation

Validation is strict.

If a feature is user-disabled and the aggregated manifest says it is required, the build must fail before final packaging.

There is no warning-only mode in the default path for this system.

## User Controls

Users should control stripping through explicit cook/build annotations and settings, not implicit platform policy.

That means:

- the manifest proves what is required
- the user chooses what they want to disable
- the build enforces compatibility between those two facts

This keeps the engine honest:

- no silent degradation
- no accidental removal of needed runtime behavior
- no hidden platform-only rewrite policy

## Reporting

The system should emit one readable dependency report artifact for every build, with extra emphasis on failure cases.

Each report entry should at least show:

- feature id
- source kind
- source id
- reason
- whether the feature was user-disabled
- final outcome such as `required`, `disabled-and-failed`, or `enabled`

The same data should also be present in build output summaries so users do not need to open raw artifacts just to understand a failure.

This reporting is a core part of the feature, not optional polish. The user specifically wants to see what assets and code are including what.

## Integration With Stripping

The manifest system does not itself remove code. It becomes the proof/input layer that downstream stripping consumes.

Potential downstream consumers include:

- generated-core feature defines
- platform codegen feature filtering
- native build defines
- plugin/runtime registration pruning

This separation is intentional:

- analyzers answer `what is required`
- user configuration answers `what is allowed`
- stripping mechanisms answer `how code actually gets removed`

## First Implementation Slice

The first implementation slice should stay narrow and generic:

1. introduce the generic runtime feature-id and required-feature record model
2. introduce one build feature manifest aggregate artifact
3. generalize the existing physics3d analyzer pattern into the new model
4. add code/plugin feature requirement attributes plus build-time discovery of participating runtime types
5. add explicit disabled-feature validation with build failure and dependency reports
6. wire DS to consume the generic manifest as the first size-focused platform

This slice gives HelEngine a correct generic seam before broader stripping work expands on top of it.

## Testing

Add focused coverage for:

1. feature-id stability and serialization
2. manifest aggregation across multiple requirement sources
3. content analyzers producing expected feature records
4. code/plugin discovery producing records only for participating runtime types
5. attribute-driven feature declaration on types and members
6. build failure when a disabled feature is required
7. dependency report output for both successful and failed builds
8. DS integration proving the generic manifest can drive platform stripping decisions safely

## Risks

### Feature Id Explosion

Fine-grained ids can become noisy if the engine creates unstable or overly detailed capabilities. Feature ids must remain meaningful runtime units, not arbitrary internal implementation details.

### Incomplete Requirement Discovery

If analyzers or code discovery miss a requirement, stripping could become unsound. The first pass should bias toward conservative inclusion until coverage is proven.

### Hidden Dynamic Usage

If some runtime behaviors are activated indirectly without analyzable content or type registration, the manifest can under-report. Those paths should either become analyzable or explicitly declare requirements.

### Premature Platform Coupling

If DS-specific assumptions leak into feature ids or report semantics, the generic system will age poorly. The model must stay engine-owned and platform-neutral.

## Success Criteria

- HelEngine can produce one generic build feature manifest that unions content-derived and code/plugin-derived requirements.
- Users can explicitly disable fine-grained runtime features.
- The build fails whenever a disabled feature is still required.
- Failure output explains exactly what asset, type, or plugin required the blocked feature.
- Successful builds can still emit the same dependency report for inspection and optimization work.
- DS can consume this generic system first without introducing DS-specific feature knowledge into the manifest model.

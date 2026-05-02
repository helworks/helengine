# Editor-Owned Generated Core Regeneration

## Summary

The editor owns `helengine.core` regeneration for every platform build. Platform builder assemblies do not regenerate the shared core themselves. Instead, each builder exposes typed metadata that tells the editor how to regenerate the core for that platform, including codegen flags such as endianness and any other generation-specific switches.

This design keeps the shared engine output fresh on every build, prevents stale generated trees from drifting away from the C# source, and avoids pushing codegen responsibility into the native Windows, PS2, or future platform builders.

## Goals

- Regenerate the generated `helengine.core` C++ tree on every platform build.
- Keep regeneration under editor control.
- Let each platform builder describe its codegen needs through typed metadata.
- Support platform-specific codegen differences such as endianness and future generation flags.
- Keep native platform builders focused on staging, packaging, and native compilation only.
- Preserve a single canonical source of truth for the shared engine core.

## Non-Goals

- Rewriting the native builders into codegen hosts.
- Introducing per-platform serializers for every component.
- Making the editor aware of platform-specific native toolchain details beyond the data it needs to feed the builder.
- Changing the shared runtime model so that platforms get separate core source trees by default.

## Design

The editor build pipeline gains an explicit generated-core regeneration step before the platform builder executes.

The sequence is:
1. The editor loads the target platform builder assembly dynamically.
2. The editor reads typed platform metadata from the builder.
3. The editor regenerates `helengine.core` into a fresh build workspace using the platform's codegen metadata.
4. The editor packages scenes and assets into the staging root.
5. The editor passes the fresh generated-core root path into the platform build request.
6. The native builder consumes that fresh output and produces the final player package or executable.

The important boundary is that the editor owns freshness. If the generated C++ tree is stale, that is the editor pipeline's problem, not the native builder's problem.

## Typed Metadata Contract

The platform builder assembly already exposes typed metadata through `PlatformDefinition`. That contract is extended to include a codegen-specific section.

The codegen metadata describes:
- target endianness
- any generated-core feature flags
- any codegen-only compatibility switches
- any platform-specific generation constraints that affect the emitted C++ tree

The metadata is typed and owned by the builder assembly. The editor does not hardcode platform-specific rules for Windows, PS2, GameCube, or any future target. Instead, it asks the active builder for the generation contract and uses that to drive regeneration.

The builder may expose:
- a default codegen profile
- one or more named codegen profiles
- a mapping from build profile to codegen profile when a target needs more than one generation shape

The concrete shape can be implemented with new baseplatform definitions, but the intended behavior is:
- platform metadata is explicit and typed
- the editor reads it at runtime
- generated-core regeneration uses those values directly

## Build Flow

The new build flow is:

1. Load platform builder
2. Read `PlatformDefinition`
3. Select build profile and graphics profile from the builder metadata
4. Select the matching codegen profile from the builder metadata
5. Regenerate `helengine.core` into a fresh build workspace
6. Package the staged assets
7. Build the native player against the fresh generated core

The generated-core path should be part of the build request and the build workspace, not a manually patched repo snapshot.

Each platform build should get its own fresh generated-core output root under the build workspace. That prevents cross-build contamination and guarantees that the native build sees the same source state that the editor packaged.

## Endianness and Generation Flags

Endianness is treated as a generation contract, not a runtime guess.

If a target platform needs little-endian or big-endian generated output, the platform builder advertises that requirement in metadata. The editor then regenerates the core using that contract before any native build runs.

This makes future platform support possible without duplicating the core serializer logic per target. The component model remains canonical in `helengine.core`; the platform metadata only tells the editor how to emit the shared runtime representation for that target.

## Component Compatibility

Component compatibility remains separate from core regeneration, but it uses the same typed builder metadata pipeline.

The builder can say whether a component is:
- pass-through
- transformed during packaging
- unsupported for the target

This is orthogonal to generated-core regeneration:
- regeneration handles how the shared runtime core is emitted
- compatibility handles whether a serialized component record needs a platform-specific adaptation

The editor packages scenes generically, then applies the builder's compatibility rules only where needed.

## Failure Handling

If codegen regeneration fails, the build fails before the native builder runs.

The editor should report:
- which platform requested the regeneration
- which codegen profile was selected
- the generated-core output root
- the specific failure message from the regeneration step

If a builder does not provide required codegen metadata, the editor should fail fast with a clear configuration error.

If a component is unsupported for the target, the build should fail during packaging with a platform-specific reason.

## Testing

The following tests should exist:

1. Generated-core freshness tests
- prove the editor regenerates the core into a clean build workspace
- prove a later build does not reuse an older generated tree

2. Metadata selection tests
- prove the editor reads the builder's codegen contract
- prove the correct codegen profile is selected from build metadata

3. Compatibility tests
- prove pass-through components remain pass-through
- prove transforms are applied only where the builder asks for them

4. End-to-end build tests
- build a Windows target with regenerated core
- build a PS2 target with regenerated core
- verify the native player consumes the fresh generated output path from the build request

## Risks

The main risk is allowing the codegen metadata to drift from the native runtime contract. That is why the metadata must stay typed and live in the builder assembly, not in an ad hoc JSON file that can silently diverge from the implementation.

Another risk is keeping stale generated output around inside build workspaces. The editor should treat the generated-core output as disposable build output and regenerate it into a clean path every build.

## Acceptance Criteria

This work is complete when:

- every platform build regenerates `helengine.core` before native compilation
- platform builders expose typed codegen metadata
- the editor uses that metadata to drive generation
- native builders no longer own generated-core regeneration
- Windows, PS2, and future platforms can request different codegen contracts without changing the editor's platform-specific logic

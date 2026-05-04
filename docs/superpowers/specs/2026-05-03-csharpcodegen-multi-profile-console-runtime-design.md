# CSharpCodegen Multi-Profile Console Runtime Design

## Status

Proposed and approved for planning.

This document defines how `csharpcodegen` should support multiple named conversion presets while preserving C# feature parity and allowing strict low-footprint player builds for memory-constrained consoles.

## Summary

`csharpcodegen` should support multiple named conversion presets that are composed from separate profile axes instead of treating one platform as one fixed conversion mode.

The system must allow combinations such as:

- Windows with shaders
- Windows without shaders
- PS2 low-footprint player runtime
- N64 minimal runtime

The converter must preserve shared authored C# behavior where possible, but must also let builds declare which runtime facilities are allowed to exist in generated native output.

This design adds:

- named conversion presets in `csharpcodegen`
- stricter feature and restriction profiles
- per-type runtime include emission
- explicit Helengine-side preset selection
- conversion-time validation for forbidden runtime systems

## Goals

- Support multiple named conversion presets for the same platform family.
- Keep C# authoring unified instead of forking engine code per target.
- Allow Windows desktop builds to opt into or out of systems such as shaders.
- Allow strict low-footprint player presets for PS2, N64, and similar hardware.
- Ensure unsupported runtime systems are omitted, not merely left unused.
- Prevent shader compilation, runtime JSON parsing, reflection-like runtime support, and similar systems from leaking into strict player outputs when the preset forbids them.
- Reduce generated include churn and generated code size by emitting runtime helper includes per type instead of per full conversion run.
- Make Helengine platform/build configuration choose codegen presets explicitly.

## Non-Goals

- Rewriting all runtime systems in this pass.
- Implementing full platform-specific runtime replacements for every forbidden subsystem.
- Solving every runtime footprint problem only through `csharpcodegen`.
- Replacing the existing compiler, platform, or runtime profile system.
- Introducing a different authored codebase for low-end consoles.

This pass focuses on conversion preset selection, feature gating, restriction enforcement, and include tightening.

## Problem Statement

The current generated C++ output shows that the converter can prune some feature-owned code, but the pruning is too coarse for strict console targets.

Current issues:

- feature buckets are too broad and miss important runtime leakage boundaries
- generated source files include runtime support headers that are globally registered for the conversion, even when a given type does not need them
- runtime-facing C# still exposes systems such as shader-side parsing and runtime JSON parsing to the converter surface
- Helengine can choose a platform and tool path, but not a named conversion preset

This causes strict player outputs to carry runtime support that should not be present at all.

## Design Principles

## Multiple Presets, One Codebase

The authored C# codebase remains shared.

Conversion presets define what native runtime shape is allowed for a given build.

This preserves feature parity where appropriate, while still allowing strict build outputs for constrained targets.

## Composable Profile Axes

A build should not be modeled as one monolithic target profile.

It should be assembled from:

- compiler profile
- platform profile
- runtime profile
- feature profile
- restriction profile

Named presets are stable saved combinations of those axes.

## Restrictions Are Stronger Than Feature Hints

Feature detection and pruning are necessary but not sufficient.

A strict console preset must be able to state that certain runtime systems are forbidden even if reachable authored code still references them.

When a forbidden system is reached, conversion should fail with a deterministic diagnostic.

## Generated Output Must Reflect Actual Type Usage

Runtime helper headers should only be included in generated files that need them.

Global run-level registration is acceptable for reports and generated config, but not for per-type source emission.

## Helengine Must Choose Presets Explicitly

Preset choice must be part of Helengine platform/build configuration.

The editor and platform build graph must not guess whether a build is `windows-shaders` or `windows-no-shaders`.

## CSharpCodegen Model Changes

## Conversion Preset

Add a new named preset model:

- `CPPConversionPreset`

Proposed fields:

- `Id`
- `CompilerProfile`
- `PlatformProfile`
- `RuntimeProfile`
- `BuildFeatureProfile`
- `RestrictionProfile`

## Preset Catalog

Add:

- `CPPConversionPresetCatalog`

Responsibilities:

- define built-in preset ids
- resolve a preset by stable id
- create fully resolved conversion options from the preset

Example built-in preset ids:

- `windows-shaders`
- `windows-no-shaders`
- `ps2-lite`
- `n64-minimal`

## Restriction Profile

Add:

- `CPPRestrictionProfile`

This profile defines which runtime capabilities are allowed or forbidden for the conversion output.

Initial restriction switches:

- `ForbidShaders`
- `ForbidRuntimeJson`
- `ForbidReflectionLikeRuntime`
- `ForbidRegex`
- `ForbidDebugOnlySystems`

Additional restriction switches may be added later, but these cover the current leakage zones.

## Conversion Options

Extend `CPPConversionOptions` with:

- `PresetId`

Resolution flow:

1. resolve preset from `PresetId`
2. copy preset profiles into active options
3. apply future explicit overrides, if the system later supports them
4. run conversion with resolved options

The conversion report must record:

- preset id
- active compiler profile
- active platform profile
- active runtime profile
- active feature profile
- active restriction profile

## Feature Model Changes

The current feature buckets are too coarse for strict player-runtime gating.

Add or split feature buckets so the converter can separately reason about:

- `Shaders`
- `RuntimeJson`
- `ReflectionLikeRuntime`
- `DebugOverlay`
- `HostFileSystem`
- `TextProcessing`

These buckets do not need to map one-to-one with every subsystem in the engine. They only need to cover runtime leakage boundaries that materially affect low-footprint builds.

## Detection and Enforcement

## Feature Detection

The feature scanner should continue to detect explicit roots from Roslyn symbol usage, but the catalog must be expanded so the current shader and runtime parsing surfaces are classified correctly.

Examples of roots that should be classified:

- runtime shader compilation and shader-source parsing types
- runtime JSON manifest parsing types
- runtime type-token and type-driven registration surfaces
- debug overlay surfaces

## Restriction Validation

After feature resolution, the converter must validate the result against the preset restriction profile.

Validation should fail conversion when:

- a forbidden feature bucket is enabled or reachable
- a forbidden runtime requirement is registered
- a forbidden native runtime helper is about to be copied into output

Diagnostics must be explicit and actionable.

Example diagnostic intent:

- preset `ps2-lite` forbids shaders, but shader parsing code is reachable through runtime type `helengine.SomeRuntimeShaderType`
- preset `n64-minimal` forbids runtime JSON, but `RuntimeManifestJsonReader` was included in the reachable graph

## Runtime Requirement Ownership

`CPPRuntimeRequirementCatalog` currently assigns owning features to some runtime helpers.

This ownership map must be expanded so runtime helpers for restricted systems are correctly controlled by the new feature and restriction buckets.

Examples:

- `Regex` should be tied to the relevant feature buckets and restriction validation
- `NativeType` should be tied to reflection-like runtime support
- `StringBuilder` should not remain broadly reachable through unrelated systems without classification

## Include Emission Changes

## Current Problem

Generated source preambles currently include all registered runtime requirements for every emitted `.cpp` file.

That inflates includes, compile cost, and emitted source size, and makes pruned builds appear broader than they really are.

## Required Change

The emitter should include runtime helper headers per type, based on the requirements actually encountered while lowering that type.

Run-level registration should still exist for:

- generated config
- feature manifests
- conversion reports

But per-file source includes must be type-scoped rather than conversion-scoped.

## Helengine Integration

Helengine must be able to select a named `csharpcodegen` preset directly.

Add a stable field to platform/build configuration:

- `codegenPresetId`

This should be plumbed through:

- platform metadata/configuration
- editor-side generated-core regeneration
- platform build execution

The Helengine editor must pass the preset id into `csharpcodegen` rather than relying only on the executable path.

## Example Presets

## Windows With Shaders

- compiler: `msvc`
- platform: `windows`
- runtime: `stl-lite`
- features: shaders enabled, desktop-facing runtime permitted
- restrictions: permissive desktop policy

## Windows Without Shaders

- compiler: `msvc`
- platform: `windows`
- runtime: `stl-lite`
- features: shader systems disabled
- restrictions: forbid shaders

## PS2 Lite

- compiler: target-appropriate console compiler profile
- platform: `ps2`
- runtime: low-footprint runtime profile
- features: no shaders, no debug overlay, no runtime JSON
- restrictions: forbid shaders, runtime JSON, reflection-like runtime, regex, and debug-only systems

## N64 Minimal

- compiler: target-appropriate console compiler profile
- platform: `n64`
- runtime: minimal low-footprint runtime profile
- features: minimal player-only set
- restrictions: stricter than PS2 where necessary

## Verification Strategy

`csharpcodegen` tests should cover:

- preset lookup and resolution
- `windows-shaders` and `windows-no-shaders` producing different resolved feature sets
- strict preset validation failures when forbidden systems are reachable
- generated output omitting forbidden runtime helper files
- per-type include emission not pulling global runtime helper headers into unrelated files

Helengine tests should cover:

- platform/build configuration carrying `codegenPresetId`
- editor-side command construction passing preset id through
- generated build report reflecting the selected preset

## Worktree And Branching Strategy

Implementation should happen in isolated worktrees because another agent is already working in parallel.

Recommended implementation split:

- `csharpcodegen` worktree for preset resolution, restriction validation, feature expansion, and include tightening
- Helengine worktree for preset selection and editor/build integration

This keeps generator changes and consumer wiring isolated while still allowing coordinated rollout.

## Rollout Order

1. Implement preset and restriction support in `csharpcodegen`.
2. Tighten runtime requirement ownership and per-type include emission.
3. Add end-to-end generator tests for shader-disabled and strict console presets.
4. Add Helengine-side preset selection plumbing.
5. Verify generated reports and generated outputs reflect the chosen preset correctly.

## Open Questions Resolved

- Multiple presets are required and are first-class, not ad-hoc switches.
- Helengine must explicitly select the preset.
- Windows variants such as with-shaders and without-shaders are supported by preset composition.
- Strict consoles need omission of forbidden systems from generated runtime output, not merely unused code paths.

# Dynamic Platform Builder Assemblies Design

## Summary

HelEngine should stop treating Windows as a special case in the editor build path. Windows and PS2 should both be built through repo-local builder assemblies that are loaded dynamically from `user_settings/platforms.json`.

The editor remains the build orchestrator at the queue level, but it no longer owns platform-specific native build logic such as `cmake`, Docker, SDK discovery, host toolchain selection, build profile schemas, or graphics profile schemas. That logic moves into the platform repo that owns the target.

The editor still prepares the build request by staging the selected scenes and resolved content inputs, but the actual platform build execution and platform metadata discovery are delegated to the platform builder assembly through the shared base-platform contract.

## Problem

The current Windows path is hardwired in the editor:

- `EditorSession` directly constructs a Windows-specific executor.
- The Windows platform entry in `user_settings/platforms.json` does not point to a builder assembly.
- The platform-specific native build orchestration currently lives in the editor instead of in `helengine-windows`.

That creates three problems:

1. Windows does not follow the same dynamic loading model as PS2.
2. The editor knows too much about platform toolchains and platform metadata.
3. Platform ownership is blurred because the editor is carrying platform-specific build logic and platform schema knowledge that should live in the platform repo.

## Goals

- Load both Windows and PS2 builders dynamically from `user_settings/platforms.json`.
- Remove hardcoded Windows build orchestration from the editor.
- Keep the editor as the queue manager and request preparer, not the owner of native toolchains or platform schemas.
- Make each platform repo own its own builder assembly, build entrypoint, and platform metadata definition.
- Keep the shared base-platform contract as the boundary between editor and builder.
- Preserve the current staged-request flow so the editor can still package scenes and resolve assets before handing execution to the builder.
- Keep the dynamic loading mechanism generic enough for future platform repos.

## Non-Goals

- No remote builder dispatch.
- No rewrite of the base-platform contract in this slice.
- No new platform manifest format beyond filling in the existing builder path for Windows.
- No attempt to collapse the editor-side content packaging pipeline into the platform builder yet.
- No change to how scene selection or build queue persistence works.

## Current State

The current system already has the right pieces in separate places, but not yet with the right ownership:

- `helengine-base-platform` defines the shared `IPlatformAssetBuilder` contract and request/report types.
- `helengine-ps2` already has a builder assembly that implements that contract.
- The editor already has a dynamic builder loader and PS2-specific build executor glue.
- Windows still uses a direct editor executor with embedded native build steps.

The next step is to make Windows match PS2’s separation model, then remove platform-specific executor knowledge from the editor entirely, and then route both through the same loader-driven path.

## Proposed Architecture

### Platform Metadata

`user_settings/platforms.json` remains the source of truth for installed platform payloads.

Each buildable platform entry must provide:

- `platformId`
- `builderAssemblyPath`
- `playerSourceRootPath`

For a platform to be buildable, `builderAssemblyPath` must point to a real assembly on disk. A buildable platform with an empty builder path is invalid.

The Windows entry should point at a Windows builder assembly output under `helengine-windows`.
The PS2 entry should continue pointing at the PS2 builder assembly output under `helengine-ps2`.

The loaded builder assembly is the source of truth for platform-specific metadata:

- build profiles
- graphics profiles
- required build inputs
- asset requirements
- output capabilities

The editor must not hardcode any platform-specific schema for those values.

### Builder-Defined Metadata

Each loaded builder assembly should expose a typed platform definition through the shared contract.

That definition should tell the editor:

- which build profiles exist
- which graphics profiles exist
- which options each profile exposes
- which assets are required to build successfully
- which output modes the platform can produce

The editor uses that typed definition to build the UI and queued build requests generically. The editor must not contain platform-specific branches for build profile names, graphics profile names, or required asset types.

### Editor Execution Flow

The editor build queue should use one router that dispatches by `platformId`, but the routed executors must be dynamic-builder adapters rather than platform-specific hardwired build orchestrators.

For each queued build item, the editor should:

1. Resolve the platform entry from `platforms.json`.
2. Stage the selected scenes and resolved asset inputs into a build request root.
3. Load the builder assembly from `builderAssemblyPath`.
4. Validate the builder descriptor and platform definition against the requested platform and engine version.
5. Call `BuildAsync(...)` on the loaded builder.
6. Collect progress and diagnostics from the builder.
7. Treat the builder report as the authoritative build result.

The editor should not call `cmake`, Docker, or platform SDK tools directly for target builds after this refactor.

### Builder Responsibilities

Each platform builder assembly should own the platform-specific build orchestration and the platform-specific metadata schema for its repo.

That includes:

- validating the staged request
- describing the platform definition consumed by the editor
- building or preparing the target-specific output layout
- invoking the platform-native build tools or containers it needs
- writing the final packaged outputs into `OutputRoot`
- emitting diagnostics and progress through the shared reporter interfaces

The builder assembly is the platform-specific build boundary. The editor should not need to know whether a builder uses CMake, Docker, a Makefile, or any other toolchain.

### Windows Builder Ownership

`helengine-windows` should add its own builder assembly and move the current Windows-specific build orchestration and Windows-specific metadata definition into that assembly.

The builder should be responsible for the current Windows source-build flow that the editor now performs directly, including any generated-source regeneration and native player compilation needed to produce Windows output.

This is the key behavior change:

- the editor stops being a special-case Windows build host
- `helengine-windows` becomes the owner of the Windows build logic
- the editor only loads the builder dynamically and forwards the staged request

### PS2 Builder Ownership

`helengine-ps2` should continue owning the PS2 builder assembly and its metadata definition.

The PS2 builder remains the platform-specific owner of its staging and packaging behavior, and it can continue to evolve its native build orchestration internally without requiring editor changes.

### Shared Editor Loader

The editor should use one loader for all platform builder assemblies.

That loader should:

- load the assembly from the path in `platforms.json`
- locate the concrete `IPlatformAssetBuilder` implementation
- instantiate it
- read the typed platform definition from the builder
- validate that the descriptor and definition match the target platform

The loader must stay generic. It should not contain Windows- or PS2-specific branches.

## Build Request Shape

The shared `PlatformBuildRequest` remains the handoff object between editor and builder.

The request should continue to carry:

- the resolved manifest
- the requested target variants
- the requested cook profiles
- the output root
- the working root

The editor may still stage selected scenes and assets into a request root before invoking the builder, but the platform builder owns the rest of the build execution.

## Error Handling

The editor should fail early and clearly when:

- the platform entry is missing
- `builderAssemblyPath` is empty for a requested buildable platform
- the builder assembly file does not exist
- the assembly does not expose a valid `IPlatformAssetBuilder`
- the builder descriptor does not match the requested platform or engine version
- the builder definition does not provide the metadata required by the editor UI

The failure message should identify the platform and the resolved assembly path so build problems are easy to trace back to the repo that owns them.

The builder should continue to emit structured diagnostics for request-level issues such as missing staged payloads or invalid target/cook-profile combinations.

## Testing

Add or update tests for:

- Windows platform entries resolving builder assembly paths from `platforms.json`
- PS2 platform entries resolving builder assembly paths from `platforms.json`
- the shared loader loading both Windows and PS2 builder assemblies
- the router dispatching both platform ids through the same in-process pattern
- the editor rejecting a buildable platform entry that does not provide a builder assembly path
- the Windows builder assembly smoke test
- the PS2 builder assembly smoke test
- the editor no longer constructing a Windows-specific executor directly in `EditorSession`

## Files in Scope

- `engine/helengine.editor/EditorSession.cs`
- `engine/helengine.editor/managers/project/EditorBuildExecutorRouter.cs`
- `engine/helengine.editor/managers/project/EditorPlatformAssetBuilderLoader.cs`
- `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- `engine/helengine.editor.tests/EditorBuildExecutorRouterTests.cs`
- `user_settings/platforms.json`
- `helengine-windows/builder/*`
- `helengine-ps2/builder/*`

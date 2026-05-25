# Docker Target Builders Design

## Summary

HelEngine should use Docker as the standard local build execution contract for native target builds. Each concrete target id maps to a dedicated builder image that contains the exact tools required to generate, configure, compile, stage, and package that target.

This design extends the existing deployment-root generated-build model rather than replacing it. Generated source remains target-scoped under `GeneratedSource/<target-id>`, native intermediates remain target-scoped under `Intermediate/<target-id>`, and final packaged output continues to merge into the shared `Build` folder.

The first supported Docker target is `windows-directx`, using a Windows-capable builder image and local Docker execution. Later targets such as `windows-vulkan`, `linux`, `mac`, `ps2`, `gamecube`, `wii`, and external package-owned console builders can adopt the same contract with different images.

## Goals

- Standardize native builds behind one local builder contract.
- Keep toolchains isolated from the user machine and the editor process.
- Make target builds reproducible by binding each target id to an explicit builder image.
- Preserve the deployment-root directory model:
  - `GeneratedSource/<target-id>`
  - `Intermediate/<target-id>`
  - shared `Build`
- Support future multi-target shared deployment roots.
- Keep the contract generic enough to support remote builders later without redesigning build definitions.

## Non-Goals

- Building every target from the same host operating system.
- Designing remote build dispatch in phase 1.
- Collapsing all target toolchains into one universal builder image.
- Replacing platform host repositories such as `helengine-windows`.

## Key Constraint

Docker standardizes the build interface, but it does not eliminate host operating system requirements.

In particular:
- `windows-directx` requires a Windows-capable Docker environment running Windows containers.
- Linux Docker images are suitable for Linux-native targets and many cross-toolchain targets, but not for a real Windows DirectX/MSVC build.

The build system must therefore treat image selection and host compatibility as part of the target definition.

## Builder Contract

The build system should invoke one Docker builder per concrete target.

Required inputs:
- `TargetId`
- `DeploymentRoot`
- project root or project file path
- resolved build feature profile
- build configuration
- selected scene or content inputs already defined by the build workflow

Required mounted paths:
- project source
- deployment root
- optional shared Docker volume for caches or SDK/toolchain data

Required outputs:
- generated source under `GeneratedSource/<target-id>`
- native intermediates and staged payload under `Intermediate/<target-id>`
- merged output under shared `Build`

The editor and deployment system should depend on this contract, not on raw `cmake` or host-specific tool discovery.

## Target-to-Image Mapping

Images should map to concrete target ids rather than broad platform names.

Examples:
- `windows-directx` -> `helengine-builder:windows-directx`
- `windows-vulkan` -> `helengine-builder:windows-vulkan`
- `linux` -> `helengine-builder:linux`
- `mac` -> `helengine-builder:mac`
- `ps2` -> `helengine-builder:ps2`

This keeps each image minimal and honest about the toolchain it actually contains.

The editor build system should store and resolve this mapping explicitly. It should not infer the image from the host machine or guess tools from the local environment.

## Container Responsibilities

Each builder container should own the entire target build flow for that target:

1. Run transpilation and generation into `GeneratedSource/<target-id>`.
2. Run native configure and compile steps into `Intermediate/<target-id>`.
3. Stage target outputs under `Intermediate/<target-id>`.
4. Merge staged outputs into shared `Build` using the deployment-root merge rules.
5. Emit manifests and logs describing what happened.

Keeping both generation and native build inside the container is the correct default because:
- target-specific generation can depend on target profile details
- the builder image owns the exact runtime and tools needed for that target
- local host drift is reduced

## Deployment-Root Interaction

This design builds directly on the deployment-root generated-build design.

For a given `TargetId` and `DeploymentRoot`, the builder writes to:
- `<DeploymentRoot>/GeneratedSource/<TargetId>`
- `<DeploymentRoot>/Intermediate/<TargetId>`
- `<DeploymentRoot>/Build`

Multiple targets may share the same deployment root. They do not share generated source or intermediates, but they do merge into the same final `Build` tree after staging.

Builders must not compile directly into the final `Build` tree. They must stage under `Intermediate/<TargetId>` first and then apply merge rules.

## Builder Entrypoint

Each image should expose one thin builder entrypoint script or command.

The entrypoint should:
- validate required inputs and mounted folders
- resolve the target-specific generation profile
- invoke the code generator
- invoke the native host build system
- stage outputs
- merge into shared `Build`
- emit manifests and logs
- return a non-zero exit code on failure

The entrypoint should not own project policy. It executes the build requested by the orchestrator. The editor or deployment system still owns:
- which targets are selected
- which deployment root is used
- which features are enabled or disabled
- whether multiple targets share a deployment root

## Windows DirectX Phase 1

Phase 1 should implement only `windows-directx`.

Required characteristics:
- local Docker execution only
- Windows-capable Docker environment
- one Windows builder image
- one explicit entrypoint
- one editor path that invokes Docker for `windows-directx`

The Windows builder image should contain:
- `dotnet` runtime or SDK needed by code generation
- `cmake`
- MSBuild or Ninja plus the selected Visual C++ toolchain path
- Windows SDK
- any additional build-time components required by `helengine-windows`

The image should not contain speculative support for future targets.

## Editor and Orchestrator Changes

The editor-side build executor should stop assuming direct host execution of `dotnet` and `cmake` for target builds.

Instead it should:
- resolve the concrete `TargetId`
- resolve the builder image for that target
- validate Docker availability and host compatibility
- run the builder container with the required mounts and arguments
- surface builder logs and failures in build diagnostics

The editor should treat Docker as the standard local execution path for supported targets.

## Host Compatibility Rules

The build system must validate that the current machine can run the required container type.

Examples:
- `windows-directx` requires a Windows Docker host capable of Windows containers
- `linux` requires a Docker host capable of Linux containers

If the host cannot run the required image type, the build must fail early with a direct diagnostic. It must not fall back silently to host-native tool discovery.

## Caching and Reuse

Docker images should be reusable across builds. Generated output remains disposable per target build.

Recommended cache layers:
- Docker image layers for tool installations
- optional named Docker volumes for package caches or SDK caches
- persistent deployment-root `Intermediate/<target-id>` folders for incremental native rebuilds

`GeneratedSource/<target-id>` should still be fully regenerated per build to avoid stale files.

## Build Diagnostics and Reporting

The builder contract should emit machine-readable diagnostics alongside normal logs.

Recommended outputs:
- build invocation manifest
- selected image tag
- target id
- host compatibility result
- generated feature report
- staged output manifest
- merge manifest

This keeps Docker-based builds inspectable and consistent with the existing generated-source visibility goals.

## Risks and Mitigations

### Risk: Docker hides host constraints

Mitigation:
- encode host compatibility rules per target
- validate them before build start
- report direct errors rather than attempting host fallback

### Risk: Windows builder image becomes too large or hard to maintain

Mitigation:
- keep images target-specific
- start with one `windows-directx` image only
- avoid bundling speculative future toolchains

### Risk: container build logic diverges from editor expectations

Mitigation:
- keep one explicit builder contract
- keep one thin entrypoint per image
- have the editor pass arguments rather than reproducing build logic locally

### Risk: multi-target shared build output becomes non-deterministic

Mitigation:
- always stage under `Intermediate/<target-id>`
- only merge into `Build` after conflict and identical-file checks
- continue writing merge manifests

## Implementation Direction

1. Add a Docker builder execution contract to the editor build executor.
2. Add target-to-image resolution for concrete targets.
3. Add Windows host compatibility validation for `windows-directx`.
4. Add a Windows builder image and entrypoint under `helengine-windows`.
5. Route the existing deployment-root build flow through `docker run` for `windows-directx`.
6. Preserve the current `GeneratedSource`, `Intermediate`, and shared `Build` layout.
7. Leave remote builder support for a later design.

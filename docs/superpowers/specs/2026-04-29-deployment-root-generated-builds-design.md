# Deployment-Root Generated Builds Design

## Summary

This design defines how HelEngine platform builds should use a shared deployment root while keeping generated source disposable and inspectable. The native platform host repositories, such as `helengine-windows`, remain source-only. Generated C++ output is emitted per target into the deployment root, and final packaged outputs from multiple targets can be merged into a single shared `Build` folder.

The design supports these goals:
- generated code is auto-generated and disposable
- users can inspect and rebuild generated output manually if they want
- platform hosts stay clean and source-controlled
- multiple targets can contribute to one deployment output tree
- identical outputs can be shared in the final package
- target-specific generated code stays isolated from other targets

## Goals

- Use the deployment path selected by the build/deployment system as the owning root for generated output and packaged build output.
- Keep generated code outside platform host repositories.
- Allow multiple compatible targets to share one deployment root.
- Keep generated source target-scoped, because emitted code can differ by platform or backend.
- Keep final packaged output deployment-scoped, so one build tree can contain Windows, Mac, Linux, and retro-console payloads together.
- Preserve user visibility into generated code, feature decisions, and build metadata.

## Non-Goals

- Defining the full per-platform packaging layout for every target.
- Defining a full artifact deduplication algorithm beyond required collision checks and identical-file sharing rules.
- Replacing the existing platform host repositories with generated code.

## Directory Model

The deployment root is provided by the build/deployment system.

Example layout:

```text
<DeploymentRoot>/
  GeneratedSource/
    windows-directx/
    windows-vulkan/
    mac/
    linux/
    ps2/
  Intermediate/
    windows-directx/
    windows-vulkan/
    mac/
    linux/
    ps2/
  Build/
```

### GeneratedSource

`GeneratedSource` contains target-specific auto-generated source trees.

Each target id gets its own subfolder, for example:
- `windows-directx`
- `windows-vulkan`
- `mac`
- `linux`
- `ps2`

Each target source tree contains:
- transpiled C++ source
- generated runtime support files
- generated configuration headers
- feature manifest
- conversion report
- build feature report
- handoff manifest for the native host build

### Intermediate

`Intermediate` contains target-specific native build intermediates and staging output. This folder is not the final user-facing package. It exists so each target can compile and stage safely without corrupting the shared `Build` tree.

Each target id gets its own subfolder.

### Build

`Build` is the shared final output tree for the deployment root.

If multiple targets are built into the same deployment root, they merge into this single folder. Shared files may be emitted once when they are byte-identical. Platform-specific binaries or loaders are added alongside shared payloads according to packaging rules.

## Ownership Boundaries

### Deployment System

The deployment/build system owns:
- the selected `DeploymentRoot`
- the set of targets included in that root
- orchestration of generation, native compile, staging, and merge
- rebuild versus clean policies

### cs2.cpp

`cs2.cpp` owns:
- generation into `GeneratedSource/<target-id>`
- generation of reports and manifests describing the emitted output
- feature-pruned output for the requested target profile

`cs2.cpp` does not own native packaging policy or the final shared `Build` merge.

### Platform Host Repositories

Platform host repositories, such as `C:\dev\helworks\helengine-windows`, own:
- native source code
- toolchain/build scripts
- backend-specific runtime code, such as DirectX or Vulkan
- consumption of an external generated source root

They do not own the location of generated output and do not copy generated code into the repo.

## Build Contract

The old `WindowsHandoffOutputFolder` model should be replaced by a general deployment-root contract.

The orchestrator provides:
- `DeploymentRoot`
- `TargetId`
- project/build inputs
- resolved feature profile

Derived paths:
- `GeneratedSourceRoot = <DeploymentRoot>/GeneratedSource/<TargetId>`
- `IntermediateRoot = <DeploymentRoot>/Intermediate/<TargetId>`
- `BuildRoot = <DeploymentRoot>/Build`

Native platform hosts consume the external generated source root and the intermediate/build locations passed by the orchestrator.

## Lifecycle Rules

### GeneratedSource Regeneration

`GeneratedSource/<TargetId>` is fully regenerated per build.

Before generation:
- if the target folder exists, clear its contents
- regenerate from scratch

This prevents stale generated files from surviving feature-pruned or target-specific output changes.

### Intermediate Reuse

`Intermediate/<TargetId>` may be reused between builds to support incremental native compilation.

Recommended default:
- keep the intermediate folder between builds
- clear it on explicit rebuild requests or when toolchain, target profile, or backend changes invalidate it

### Build Merge

Targets do not compile directly into the final `Build` tree.

Instead:
1. compile and stage under `Intermediate/<TargetId>`
2. merge staged outputs into `Build`
3. deduplicate identical files
4. fail on conflicting same-path outputs that are not identical unless an explicit packaging rule resolves them

## Generated Metadata

Each `GeneratedSource/<TargetId>` tree should include enough metadata for inspection and reproducibility.

Required outputs:
- conversion report
- build feature report
- target/compiler/runtime profile summary
- source project identity
- generation timestamp or build stamp
- handoff manifest for the native host

This allows users to inspect generated code and rebuild it manually if they choose.

## Multi-Target Deployment Behavior

A single deployment root may include multiple targets.

Example:
- Windows DirectX
- Windows Vulkan
- Mac
- Linux

Those targets share:
- one deployment root
- one final `Build` tree

They do not share:
- generated source folders
- native intermediates

This separation is required because generated source and runtime contracts may differ by platform or graphics backend.

## Collision Policy

The final `Build` merge must enforce deterministic rules.

Required policy:
- if two targets produce the same relative output path and the files are byte-identical, keep one shared copy
- if two targets produce the same relative output path and the files differ, treat it as a merge conflict unless an explicit packaging rule resolves it
- record shared files and conflicts in a deployment manifest

This keeps the multi-platform-disc scenario safe and inspectable.

## Windows Host Integration

`helengine-windows` should consume:
- an external generated source root for the selected target id
- an intermediate/build directory supplied by the orchestrator

It should not assume generated output lives inside its repo.

For the Windows DirectX target, the relevant generated source root will typically be:
- `<DeploymentRoot>/GeneratedSource/windows-directx`

## Implementation Direction

1. Replace `WindowsHandoffOutputFolder` with `DeploymentRoot` plus `TargetId` in the generator-facing build contract.
2. Make `cs2.cpp` emit directly into `GeneratedSource/<TargetId>`.
3. Make native build orchestration use `Intermediate/<TargetId>` for staging and compilation.
4. Merge staged outputs into the shared `Build` tree with identical-file deduplication and conflict checks.
5. Keep platform host repositories stateless with respect to generated code locations.

## Risks and Mitigations

### Risk: stale generated files

Mitigation:
- fully clear `GeneratedSource/<TargetId>` before every generation

### Risk: target outputs overwrite each other in the final package

Mitigation:
- require per-target staging in `Intermediate/<TargetId>`
- merge into `Build` only after conflict checks

### Risk: deployment root becomes platform-specific by accident

Mitigation:
- keep `Build` deployment-scoped
- keep `GeneratedSource` and `Intermediate` target-scoped
- keep deployment policy in the build orchestrator, not the platform host repo

### Risk: users cannot understand what was generated

Mitigation:
- emit reports, manifests, and target/profile metadata into each generated source tree

## Success Criteria

This design is successful when:
- generated code lives under the deployment root, not inside platform host repositories
- each target gets its own generated source and intermediate folders
- multiple targets can contribute to one shared `Build` folder
- identical files can be shared safely in the final package
- conflicting outputs are detected deterministically
- users can inspect generated code and rebuild it manually from the generated tree if desired

# Unified Platform Runtime Build System Design

## Status

This document replaces the narrower shared build-graph direction as the primary source of truth for platform build architecture.

Older design docs about dynamic builders, generated-core regeneration, and the shared build graph should be read as contributing slices, not as the final top-level architecture.

## Summary

HelEngine should use one editor-owned, multi-platform build system that can hold all target platforms together under one architecture.

That system must support:

- many target platforms
- multiple runtime storage profiles
- per-platform code module behavior
- shared logical artifacts across compatible targets
- platform-specific cooked variants when needed
- future physical repetition and byte-placement control
- future loose files, packfiles, segmented packfiles, disc-style layouts, and shared-media aggregation

The build graph is editor-owned. Platform builders provide typed metadata and consume prepared build inputs. They do not own editor-side cooking, generated-core regeneration, or project-side dependency discovery.

The runtime/player is specialized by:

- platform
- storage/runtime profile

This keeps startup/runtime code as small as possible for the produced build.

## Goals

- Define one build system architecture that scales across all current and future platforms.
- Make the editor the single orchestrator for generated core, asset cooking, code cooking, variant resolution, layout, container writing, and packaging.
- Keep platform knowledge out of the editor’s orchestration layer.
- Support stable logical asset identity across all targets.
- Support platform/profile-specific cooked variants under those stable logical ids.
- Support future physical asset repetition and exact byte placement without redesigning cooking.
- Support authored code module manifests with nested folder boundaries.
- Support per-platform overrides for module grouping, residency, and runtime emission behavior.
- Make scene order authoritative for both startup selection and future layout priority.
- Ensure the player only carries runtime/storage code needed for the chosen platform plus storage profile.

## Non-Goals

- Implement every future platform now.
- Implement the full runtime streaming system now.
- Implement dynamic code loading/unloading now.
- Implement packfiles or segmented packfiles immediately.
- Implement shared-medium aggregation immediately.
- Finalize patching or DLC architecture in this pass.

Those concerns must remain possible under this architecture, but they are not required for the first implementation rollout.

## Design Principles

## One Build Graph, Not Many Pipelines

The editor should not grow separate Windows, PS2, GameCube, Mac, Linux, and console-specific orchestration paths.

There is one build graph.

Platforms plug into it through typed metadata and builder contracts.

## Editor Owns the Graph

The editor owns:

- target selection
- scene order
- generated-core regeneration
- asset cooking
- authored code cooking
- variant resolution
- media layout
- container writing
- platform packaging invocation

Platform builders consume prepared inputs and emit target packages.

## Runtime Code Depends on How the Game Was Cooked

The player/runtime must not contain startup/storage logic for formats it does not use.

If a build uses loose files, it should not carry packfile bootstrap code.

If a build uses segmented packfiles or disc-style layout, the runtime specialization should include only the necessary storage bootstrap and access behavior for that profile.

## Logical Identity and Physical Placement Are Different

The system must distinguish between:

- logical asset identity
- cooked artifact variants
- physical placement entries
- final container layout

This is mandatory if the engine will later support:

- repeated physical copies of assets
- strict byte placement control
- optical-media locality tuning
- HDD locality tuning
- packfile segmentation

## Per-Platform Configuration Is Real

Different targets should be allowed to vary in:

- build profile
- graphics profile
- codegen profile
- storage/runtime profile
- media profile
- code module grouping and residency policy

The architecture must treat per-platform configuration as normal, not exceptional.

## Top-Level Build Axes

Each target build is defined by the following axes:

- platform
- build profile
- graphics profile
- codegen profile
- storage/runtime profile
- media profile
- per-platform module configuration

The first two axes that affect compiled runtime/player code are:

- platform
- storage/runtime profile

The other axes may influence generated code, cooking behavior, layout behavior, and packaging behavior, but they do not automatically require a new runtime code family unless proven necessary later.

## Build Graph

The shared editor-owned build graph has seven phases.

### 1. Regenerate Generated Core

The editor regenerates generated core every build.

This regeneration uses:

- platform metadata
- selected codegen profile
- selected codegen options
- selected storage/runtime profile when it affects runtime contract

Generated core is not treated as a stale repository snapshot. It is a fresh build product.

### 2. Cook Assets

The editor cooks all required runtime content into logical `*.hasset` outputs.

Cooking produces logical artifacts, not final media placement.

Scenes are cooked assets too. They remain `*.hasset`, with scene identity stored in metadata rather than extension.

Authoring files such as `.obj` or editor-side scene source files must not appear in final runtime outputs.

### 3. Cook Authored Code Modules

User-authored code goes through the build graph too.

The editor discovers authored code modules from folder-scoped manifests and emits runtime-ready code artifacts through `csharpcodegen`.

The first implementation may emit coarse modules, but the architecture must preserve the path to later loadable/unloadable module units.

### 4. Resolve Variants

The graph resolves whether cooked outputs are shared or target-specific.

Rule:

- same logical asset id
- same runtime format
- same cooked bytes

means one shared logical cooked artifact.

If format or bytes differ, the target gets its own cooked variant.

This applies to both assets and code artifacts.

### 5. Compute Physical Layout

This phase maps logical cooked artifacts to physical placement entries.

It is the boundary where future streaming/layout systems can decide:

- what repeats
- how many times it repeats
- what should be near what
- which container or segment should receive each copy

The first implementation can keep placement simple, but the contract must already represent:

- one artifact placed once
- one artifact placed many times
- placement priority
- target container or segment assignment

### 6. Write Containers

This phase turns placements into final storage structures.

Supported architecture targets include:

- loose file trees
- one large packfile
- several segmented packfiles
- disc-oriented filesystem/layout outputs

The first rollout may stay loose-file based, but the graph must not assume loose files are the only final representation.

### 7. Package and Compile Target Outputs

Platform builders consume:

- generated core output
- cooked asset artifacts
- cooked code module artifacts
- resolved manifests
- placement/container outputs

They then produce:

- target runtime package roots
- native executables or equivalent outputs
- target-side metadata

## Runtime Storage Profiles

Storage/runtime profile is a per-target build setting selected in the editor.

Examples:

- `loose-files`
- `single-packfile`
- `segmented-packfiles`
- `disc-layout`

Each build target selects exactly one storage/runtime profile.

One build medium must use one storage/runtime profile family.

Mixed storage models on the same output medium are not supported. If the user wants mixed storage strategies, they perform multiple builds.

## Runtime and Player Specialization

Compiled runtime/player behavior is specialized by:

- platform
- storage/runtime profile

Examples:

- `windows + loose-files`
- `windows + segmented-packfiles`
- `ps2 + disc-layout`
- `gamecube + segmented-packfiles`

This means the startup/runtime code only contains what is needed for the chosen platform plus storage/runtime profile.

Media profile remains data/config initially. It can influence layout and duplication policy without automatically requiring a new compiled runtime family.

## Media Profiles

Media profile is a per-target build setting that influences physical layout behavior.

Media profiles should control things such as:

- whether physical duplication is allowed
- whether locality is preferred over deduplication
- future packfile sizing/segmentation defaults
- future placement heuristics or container class selection

Important:

- Windows must not be hardcoded as “never duplicate”
- PS2 must not be hardcoded as “always duplicate”
- duplication/locality remains a user-facing configuration concern through media profiles

The first implementation can keep the defaults simple, but the contract must remain open to future deep configurability.

## Shared-Medium Aggregation

The architecture should support future shared-medium aggregation, but it should not be the first packaging workflow.

Recommended model:

- one multi-target build graph underneath
- per-target package outputs first
- optional later aggregation into one shared medium

That preserves optimization opportunities in shared cooking and variant resolution without forcing the first UI and packaging path to solve combined-media complexity immediately.

A shared medium may only combine targets that are compatible with the same storage/runtime profile family.

## Scene Order and Startup

Scene order in the editor build UI has two responsibilities.

### Startup Selection

The first scene in build order is the startup scene.

The startup scene is embedded into build metadata or runtime bootstrap metadata directly.

No runtime path should depend on hardcoded special filenames such as `startup.helen`.

### Layout Priority

Scene order is also a layout hint.

Earlier scenes in build order may later influence:

- clustering priority
- container ordering
- duplication priority
- physical placement near the beginning of a disc or packfile

## Startup Loading Behavior

The runtime bootstrap should default to minimal loading:

- load the startup scene
- load the startup scene’s required startup modules
- load the startup scene’s required startup assets

It should not implicitly load the whole game unless a target/profile explicitly chooses an eager residency strategy.

Windows may later decide to keep all modules resident at startup, but that should be a target/profile policy choice, not the architecture default.

## Asset Identity and Variants

Every asset has one stable logical asset id.

That stable logical id exists independently of:

- platform
- codegen profile
- storage/runtime profile
- media profile

Under that stable logical id, the build graph may emit:

- one cooked variant
- several cooked variants when format or bytes differ

Physical placement references a selected cooked variant, not the original authored path directly.

## Asset Folder Metadata

The first architecture does not require strong authored asset folder manifests.

Asset placement policy depends on a future streaming system that does not exist yet.

So this design only requires that the layout layer remain ready for later streaming-driven decisions.

That future streaming system should be able to decide:

- which artifacts repeat
- how many times
- which artifacts cluster together
- which bytes go into which segments or positions

without needing the cooking model redesigned.

## Code Module Model

Authored code uses folder-scoped manifests, similar in spirit to Unity’s assembly-definition model.

Rules:

- a module manifest lives in a folder
- code files under that folder belong to the module recursively by default
- a nested manifest creates a new authored module boundary
- authored manifests define logical module ownership and dependencies

This authored graph is not the final platform/runtime grouping by itself.

## Per-Platform Module Configuration

Per-platform configuration can override how authored modules are emitted.

That means a platform-specific module config may:

- merge authored modules
- split authored modules further
- change residency policy
- change load scope
- choose different emission/layout behavior for the selected runtime/storage profile

This is the right place for platform-specific module behavior because the editor already has the concept of per-platform configuration.

## Module Residency and Load Scopes

The architecture should preserve support for module load scopes such as:

- always loaded
- scene loaded
- area loaded
- explicitly requested

The first implementation does not need to implement dynamic unloading, but it must not block it.

Windows can still use multiple modules even if it loads them all at startup. That keeps authoring boundaries consistent and useful for future constrained targets.

## UI and Configuration Model

Configuration layers should be split like this:

### 1. Platform-Independent Authored Project Data

- scenes
- assets
- logical asset ids
- authored code-module manifests

### 2. Per-Target Build Configuration

- build profile
- graphics profile
- codegen profile
- storage/runtime profile
- media profile
- per-platform module configuration
- scene order

### 3. Future Shared-Medium Aggregation Configuration

Only needed once combined-medium outputs are implemented.

The first UI rollout should keep package outputs per target while allowing the underlying graph to support multi-target execution.

## `helengine.files` Boundary

The `helengine.files` split still stands under this architecture.

Recommended responsibility split:

- `helengine.core`
  - runtime models
  - read-side packaged-content loading
  - runtime deserializers
- `helengine.files`
  - write-side serialization
  - cooked asset writers
  - scene/package writers
  - future container writers
- `helengine.editor`
  - orchestration
  - build graph
  - authored dependency discovery
  - per-platform build configuration

## Platform Builder Role

Platform builders should provide:

- typed platform metadata
- build profiles
- graphics profiles
- codegen profiles
- media profile definitions
- asset requirement definitions
- component compatibility rules
- packaging/native build backend

Platform builders should not own:

- editor-side cooking
- project-side module discovery
- generated-core regeneration orchestration
- final decision making about authored project graph structure

## Windows and PS2 in the First Rollout

Windows and PS2 should both conform to this architecture from the start of the new implementation direction.

That means:

- same editor-owned build graph
- same generated-core regeneration ownership model
- same cooked manifest contract
- same startup-scene contract
- same storage/runtime profile model
- same per-platform module configuration model

Windows may remain the first fully operational end-to-end target, but PS2 should not remain architecturally separate.

## First PS2 Vertical Slice

The first PS2 end-to-end slice should prove that the shared build graph can emit a bootable PS2 runtime package without introducing a second architecture.

Success for this slice means:

- the editor drives a PS2 build through the shared graph
- the PS2 builder uses Docker, not a host-local toolchain
- the export emits a PS2 ELF plus cooked runtime assets
- the PS2 runtime loads the packaged startup scene from the cooked output

This slice does not require final material authoring support. It only needs the runtime to reach packaged scene load on the same build contract as Windows.

### PS2 Build and Packaging Rules

The PS2 builder must be Docker-only for reproducibility.

The editor should prepare generated core, cooked assets, cooked code modules, and generated native runtime metadata before invoking the PS2 builder.

The PS2 builder should then:

- stage the generated C++ that the native build will compile
- build the ELF through the existing Docker plus `make` flow
- copy the resulting `helengine_ps2.elf` into the final export root
- copy cooked runtime assets into the same export root under `cooked/`

The first physical storage layout is loose files. This is only a transport choice for the first slice.

The storage model must still remain disc-oriented in the higher-level architecture. The next storage step is one mounted container file with seek-based reads, not a different runtime contract.

### PS2 Runtime Startup Contract

The PS2 runtime must follow the same no-JSON rule as the Windows runtime package.

That means:

- startup scene path is compiled into generated native source
- code-module residency metadata is compiled into generated native source
- the ELF does not depend on runtime JSON manifests

The startup scene should resolve to the cooked runtime scene path, using the same startup-scene contract already established by build order.

### PS2 Runtime Integration Constraints

The PS2 host must not continue depending on the removed legacy input stack.

It should move to the portable input system by feeding pad state through a PS2-specific backend bridge. This keeps PS2 aligned with the new input architecture and avoids reintroducing platform-only keyboard or mouse assumptions.

Asset resolution must use the cooked runtime layout directly. The PS2 runtime should not introduce Windows-style path assumptions, editor-only bootstrap names, or temporary fallback rules.

### Validation Project

The validation project for this slice is `C:\\dev\\helprojs\\city`.

For the current milestone, that project acts as a small smoke scene. The expected runtime payload is simple, which makes it appropriate for proving scene-load correctness before deeper PS2 rendering and material work.

### Out of Scope for This Slice

The first PS2 vertical slice does not need to solve:

- authored material specialization
- final disc-container mounting
- streaming
- dynamic code loading or unloading
- general content-performance optimization

Those concerns should remain enabled by the architecture, but they should not delay the first proof that the PS2 ELF can be exported and can load its packaged startup scene.

## Future Streaming System

The streaming system is future work, but this design must preserve room for it.

That future system should be able to drive:

- asset repetition
- placement priority
- segment balancing
- clustering
- locality optimization

without changing:

- logical asset ids
- cooked variant identity
- authored module ownership

## Future Distribution and Patching

Patching, DLC, and incremental update distribution are not part of the first implementation scope.

However, the architecture should leave room for them by keeping:

- stable logical ids
- explicit variant identity
- explicit placement identity
- explicit container identity

These are the right primitives for later patch and expansion systems.

## First Rollout Boundary

The first implementation should focus on:

- shared editor-owned build graph
- generated-core regeneration every build
- asset cooking to logical `*.hasset`
- authored code module manifest support
- per-platform module configuration model
- startup scene from scene order
- runtime specialization by platform plus storage/runtime profile
- Windows and PS2 on the same high-level contract

The first rollout does not need to fully implement:

- packfile containers
- segmented packfiles
- streaming heuristics
- shared-medium aggregation
- runtime module unload/reload

But it must not hardcode assumptions that prevent those features.

## Consequences

### Positive

- One architecture can scale across many platforms.
- Runtime code stays smaller because storage/runtime behavior is specialized.
- Code and asset identity stay stable even as runtime packaging grows more complex.
- Future locality, duplication, and packfile work can be added without redoing cooking.
- Windows, PS2, and future targets can stay aligned under one build contract.

### Tradeoffs

- More up-front architecture work.
- More explicit configuration concepts in the editor.
- More build-graph data structures than a simple single-target export path.
- Some features must be designed before they are implemented to avoid painting the system into a corner.

## Recommendation

Use this document as the replacement source of truth for HelEngine’s platform build and runtime packaging architecture.

All future implementation plans should decompose this design into phased rollout slices, beginning with:

- graph foundation
- runtime/profile contracts
- asset cooking
- code module manifests
- Windows and PS2 builder alignment

and explicitly preserving future support for:

- repetition/locality
- packfiles
- segmented containers
- shared-medium builds
- streaming-driven placement

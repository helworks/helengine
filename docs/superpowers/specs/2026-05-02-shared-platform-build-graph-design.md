# Shared Platform Build Graph Design

## Summary

The editor should own one platform-agnostic build graph that can build any number of targets in one invocation. Windows and PS2 are the first concrete targets, but the graph must scale to many future platforms without introducing platform-specific build branches inside the editor.

Every build target runs through the same high-level phases:

1. regenerate generated core
2. cook assets
3. compile authored code modules
4. resolve shared versus platform-specific variants
5. lay out media
6. package and compile target outputs

The user still sees one build action, but the build system records separate logs and artifacts for each phase and each target so failures stay isolated.

## Goals

- Make the editor own the complete multi-platform build graph.
- Regenerate generated core output on every build.
- Cook all runtime assets into `*.hasset` outputs before packaging.
- Support building multiple targets in one invocation.
- Share cooked assets across targets when the cooked bytes and runtime format are identical.
- Duplicate artifacts only when a platform requires a different cooked format or media placement.
- Support authored project code as cooked runtime code modules.
- Support future loadable and unloadable code modules for low-memory targets.
- Support platform-specific media layouts, including disc-oriented repeated asset placement for streaming.
- Remove runtime dependence on special startup scene filenames such as `startup.helen`.

## Non-Goals

- Implementing every future platform now.
- Designing the full runtime code-module loader in this pass.
- Replacing project authoring formats with cooked formats inside the editor workspace.
- Forcing every platform to share assets when the runtime contract differs.
- Making the editor understand platform-specific native toolchains beyond the build graph contract.

## Design Principles

### One Build Graph, Many Targets

The editor should not have a Windows build path, a PS2 build path, and later ten more unrelated paths. It should have one build graph. Platform builders provide metadata and handlers that plug into that graph.

Windows and PS2 should both move onto this graph. They are the first validation that the graph is actually platform-agnostic.

### Editor Owns Orchestration

The editor owns:

- selecting targets
- reading build order
- regenerating generated core
- cooking assets
- compiling authored code modules
- deciding shared versus platform-specific artifact reuse
- driving media layout
- invoking the platform packager and native builder

Platform builders consume prepared inputs and publish typed metadata. They do not own generated-core regeneration or editor-side asset cooking.

### Logical Sharing and Physical Placement Are Different

The system must distinguish between:

- logical sharing: multiple targets reference the same cooked artifact because the cooked bytes and format are identical
- physical placement: a media layout duplicates or reorders artifacts to improve locality, streaming, or seek behavior

This distinction is required for disc-based and streaming-sensitive targets. A shared artifact may still appear more than once physically inside a packaged medium.

## Build Graph

The shared build graph has six phases.

### 1. Regenerate Generated Core

The editor regenerates generated core output for every selected target set on every build.

This phase:

- uses the platform builder's typed codegen metadata
- uses the configured codegen tool path
- emits fresh generated output into a disposable build workspace
- never reuses a stale repository snapshot as the authoritative runtime source

Generated core output is a target artifact, not a repo-maintained build input.

### 2. Cook Assets

All authored assets that are needed by the build are cooked into runtime outputs before packaging.

The cook step:

- reads scene dependency manifests and other authored references
- imports source assets such as models, textures, fonts, audio, and shaders
- emits runtime `*.hasset` outputs
- records the cook format and artifact identity for later sharing decisions

The final runtime package should not contain raw authoring assets such as `.obj` or editor-scene source files.

### 3. Compile Authored Code Modules

User-authored project code is part of the build graph and should be processed through `csharpcodegen` just like shared generated core.

The authored-code step:

- discovers project-authored code
- reads the project code-module manifest
- compiles or emits target-ready runtime code outputs per module
- records module dependencies and load metadata for later packaging

This phase should be designed so the first implementation may still ship coarse modules, while later work can split them into smaller unloadable units.

### 4. Resolve Variants

The build graph determines whether cooked outputs are shared or duplicated.

The rule is:

- if two targets produce the same runtime format and the same cooked bytes, they share one logical artifact
- if either the format or bytes differ, each target gets its own variant

Variant resolution applies to:

- asset artifacts
- code module artifacts
- generated core outputs when codegen contracts differ

### 5. Media Layout

Media layout is separate from cooking and variant resolution.

This phase decides:

- install-tree layout for desktop targets
- disc or ISO layout for console targets
- whether repeated physical copies are allowed
- whether locality clustering or deduplication is preferred
- whether one packaged medium can contain artifacts for multiple targets

This is the phase that enables later DVD or optical-media builds that intentionally repeat assets to improve streaming performance.

### 6. Package and Compile

The final phase:

- writes platform manifests
- stages target-specific package roots
- invokes native builders where required
- emits final runtime outputs such as executables, disc images, or other target packages

The native builder should consume:

- fresh generated core output
- cooked assets
- cooked code modules
- resolved manifests
- media layout inputs

## Target Model

The build graph works on selected targets, not a single platform singleton.

Each target definition should provide:

- platform id
- display name
- codegen profile metadata
- cook rules
- variant compatibility rules
- package layout rules
- media profile
- compile backend contract
- output artifact definitions

The editor may build:

- one target
- several targets
- all selected targets in one invocation

Each target gets:

- its own output root
- its own per-phase logs
- its own package manifest

## Shared Asset Pool

The build graph should maintain a shared cooked artifact pool across the selected targets in one build.

The pool stores:

- runtime `*.hasset` outputs
- cooked code module outputs
- metadata describing the cook format and producer

Targets reference pooled artifacts by manifest when sharing is valid.

When a target requires a variant, the pool stores a distinct artifact keyed by that variant contract.

This pool is logical. The media-layout phase may later choose to copy the same artifact into several physical locations.

## Runtime Asset Format

The runtime package should use one cooked asset extension:

- `*.hasset`

This includes:

- general cooked assets
- cooked scenes
- cooked fonts
- cooked models
- any other player-consumed runtime data

Authoring files may keep their own editor-side formats. The runtime output should not depend on those names.

## Startup Scene Rule

The build menu's scene order is authoritative.

The first scene in build order is the startup scene.

The package should record that launch scene directly in build metadata or executable metadata. Runtime startup should not depend on a hardcoded file such as `startup.helen`.

Changing scene order in the build menu changes the next build's launch scene.

## Authored Code Modules

Authored code should be modeled using a project-level assembly and module manifest.

The project assembly is the authoring container. The manifest defines runtime modules inside that project.

The manifest should support:

- module name
- source roots or membership
- module dependencies
- load scope
- residency rules
- target inclusion rules

Load scope examples:

- always loaded
- scene loaded
- area loaded
- explicitly requested

This allows a simple starting point where one project assembly maps to one runtime module, while preserving a path to future low-memory modular loading.

## Windows and PS2 in the First Rollout

The first rollout should move both Windows and PS2 onto the shared graph.

Windows should use:

- an install-tree media profile
- a desktop-oriented package root
- native compile outputs such as executable and debug symbols

PS2 should use:

- its own target-specific codegen profile
- cooked runtime assets prepared by the editor
- a media profile that can later evolve toward disc-sensitive placement

Doing both together prevents the build graph from becoming Windows-shaped and validates the abstraction against two already-real targets.

## Logs and Artifacts

The user should still run one build command, but the system should emit separate logs and artifacts per phase and per target.

At minimum:

- `regen.log`
- `cook.log`
- `code.log`
- `layout.log`
- `package.log`
- `native-build.log`

Per-target build summaries should point to:

- selected target id
- output root
- failed phase
- log file path

This keeps the user workflow simple while keeping failures diagnosable.

## Failure Handling

The graph should fail at the earliest invalid phase and report the exact target and phase that failed.

Examples:

- generated-core regeneration failure stops the target before cooking proceeds
- asset cook failure stops packaging for that target
- authored-code module failure stops code packaging for that target
- media-layout failure stops final packaging for that target

Multi-target invocations may either stop immediately or continue unaffected targets depending on build policy, but the default should be explicit and deterministic.

The first implementation should choose one policy and document it clearly. The recommended initial policy is:

- fail only the affected target
- continue other independent targets
- mark the overall build result as failed if any selected target failed

## Testing

The build graph should be verified with:

1. Graph orchestration tests
- prove the editor runs phases in the correct order
- prove Windows and PS2 both execute through the same graph

2. Generated-core freshness tests
- prove every target build gets fresh generated output
- prove stale generated trees are not reused

3. Asset cooking tests
- prove runtime output uses `*.hasset`
- prove raw authoring files do not leak into final packages
- prove logically identical cooked artifacts are shared across targets
- prove differing cooked artifacts produce target-specific variants

4. Code-module tests
- prove authored code is discovered and processed through the codegen path
- prove module manifests drive runtime code packaging

5. Media-layout tests
- prove install-tree targets keep a simple flat layout
- prove media profiles can request repeated physical asset placement without changing logical sharing

6. Startup-scene tests
- prove the first scene in build order becomes the launch scene
- prove no runtime dependence remains on special startup filenames

## Risks

The largest risk is mixing target-specific rules back into editor orchestration. That would recreate a Windows-first build system and make future platforms expensive.

Another risk is conflating logical sharing with physical duplication. If that boundary is not explicit, disc-sensitive targets will become difficult to support.

A third risk is making authored code modularity too coarse in the first pass. The design should allow coarse first modules without preventing later finer-grained module boundaries.

## Acceptance Criteria

This work is complete when:

- the editor owns one shared build graph for Windows and PS2
- generated core is regenerated every build
- cooked runtime outputs use `*.hasset`
- authored project code is part of the build graph
- Windows and PS2 can both run through the same graph with target-specific metadata
- identical cooked artifacts can be shared logically across targets
- differing artifacts produce target-specific variants
- media layout is a distinct phase from cooking and packaging
- the first scene in build order is the startup scene
- runtime packages no longer depend on `startup.helen` or other special startup filenames

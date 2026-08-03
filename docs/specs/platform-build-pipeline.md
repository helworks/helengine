# Platform Build Execution Pipeline

Status: living document — reflects the headless/editor CLI build path under `engine/helengine.editor` (`EditorCliBuildRunner`, `EditorPlatformBuildGraphRunner`, `EditorPlatformBuildExecutor`) and the shared contracts in `engine/helengine.baseplatform` as built. Update this file in the same change that alters the behavior it describes.

Scope: what actually happens when a platform build runs — from CLI invocation through the seven-phase build graph to a packaged output. The user-facing wrapper script and its exit-code contract are already documented in the top-level `README.md` and are not repeated here. The `.heproj` project-file format and the platform installation/SDK registry (`helengine.projectfile`, `helengine.platforms` discovery/installation) are out of scope — they get their own spec.

## 1. Entry points, preconditions, and per-invocation isolation

Two distinct headless entry points exist, both driven from `dotnet run` against the editor CLI (see `README.md`'s `build-platform.ps1` wrapper):

- **`EditorCliCommandRunner`** — executes one project-authored editor command headlessly (builds project scripts in `EditorFull` mode, loads them, invokes the named command by id).
- **`EditorCliBuildRunner`** — executes a full platform build for a project. This is the entry point this spec covers in depth.

`EditorCliBuildRunner.Run` fails fast on missing configuration rather than defaulting: it requires an existing `EditorBuildConfigDocument` (persisted by the interactive editor) and a platform-specific config entry for the requested `PlatformId` — a project that has never been configured through the editor UI cannot be built headlessly, by design ("Open the editor and configure a build first").

**Ordered prebuild commands run before the cook.** `ExecuteEditorPrebuildCommands` resolves the ordered command-id list for the selected build profile (`EditorBuildPrebuildCommandResolver`) and runs each one via a fresh `EditorCliCommandRunner` invocation — i.e. prebuild commands execute as full editor-authored commands (`EditorFull` script mode), not as part of the runtime-only cook. The first failing command aborts the build before any cook work begins.

**Every invocation is isolated by construction**, not just by convention. `EditorBuildIsolationPathResolver` derives a stable per-project root (SHA-256 of the absolute project path, truncated to 16 bytes, hex-encoded) under `%TEMP%/helengine-builds/<project-hash>/<platform-id>/`, and every build-graph *execution* additionally gets its own GUID-suffixed subdirectory — so re-running the same queued build item twice never reuses a prior execution's generated code, workspace, or logs. A build host can redirect the isolation root off the OS temp directory entirely via the `HELENGINE_BUILD_WORKSPACE_ROOT` environment variable, which also switches to a flatter path shape (platform id + execution id only, skipping the stable project-hash nesting) — this is the intended seam for hosts that must not write build state under `%TEMP%`.

### Invariants — do not break

- A headless build must never silently invent build configuration; missing `EditorBuildConfigDocument`/platform config must fail with a clear message, not fall back to defaults.
- Prebuild command failure must abort before the cook starts — a failed prebuild command must never be treated as a warning that lets packaging continue.
- Every build-graph execution must resolve to a unique, previously-unused directory tree (via the GUID execution id), even for repeated runs of the identical queued item — this is what lets concurrent/repeated builds coexist without clobbering each other's generated code or logs.

## 2. Module resolution mode: RuntimeOnly vs EditorFull

The README documents two script-module build modes (`EditorFull` includes runtime + editor modules + sibling test projects; `RuntimeOnly` includes only runtime production modules and never discovers test folders or loads editor commands). In code, which mode applies is not a user choice at build time — it is hardcoded per call site:

- `EditorCliBuildRunner.ResolveProjectScriptCompilationMode()` always returns `EditorScriptCompilationMode.RuntimeOnly` for the actual platform cook's script build — "platform cooks must not require editor tools or tests" (from the method's own doc comment).
- `EditorCliCommandRunner` and prebuild-command execution always build in `EditorFull` mode, since they need to load and invoke arbitrary editor-authored commands.

### Invariants — do not break

- The platform-cook script build must stay hardcoded to `RuntimeOnly`. Making this configurable (or defaulting to `EditorFull`) would let editor-only code or test assemblies leak into a packaged build.

## 3. The seven-phase build graph

`EditorPlatformBuildGraphRunner.Execute` runs one queued build item (`EditorBuildQueueItemDocument`) through a fixed sequence of phases, each writing to its own subdirectory of an `EditorPlatformBuildGraphWorkspace` and its own log file:

| Phase | Workspace subdirectory | Log file | What it does |
|---|---|---|---|
| `RegenerateCore` | `generated-core/` | `regen.log` | Regenerates the generated-core C++ translation unit for the selected codegen profile. |
| `CookAssets` | `cooked/` | `cook.log` | Runs the loaded platform builder's asset cook, producing the initial `PlatformBuildManifest`. |
| `CompileCode` | `code/` | `code.log` | Compiles gameplay code modules natively for the target platform, producing `PlatformBuildCodeModule[]`. |
| `ResolveVariants` | `variants/` | `variants.log` | Resolves platform build target variants against the cooked manifest. |
| `LayoutMedia` | `layout/` | `layout.log` | Lays out final media/storage placement per the selected storage and media profiles. |
| `WriteContainers` | (uses layout/variant output) | `container.log` | Writes the physical container files (e.g. disc image, package archive) per the container write plan. |
| `PackagePlatform` | `package/` | `package.log` | Runs the platform builder's final packaging step. |

Additional fixed workspace directories: `builder/` (scratch space handed to the platform builder) and `logs/` (holds all phase logs plus the phase-marker log below).

**The manifest is replaced, not mutated, across phases.** `PlatformBuildManifest` is treated as immutable data: each phase that changes it (e.g. `ReplaceCodeModules`, `ApplyRuntimeFeatureManifest`) constructs a new `PlatformBuildManifest` instance carrying forward the unchanged fields rather than setting properties on the existing one.

**A durable, timestamped phase-marker log exists specifically for postmortem debugging of headless builds.** After every phase, `WritePhaseMarker` appends a UTC-timestamped line (`workspace-ready`, `boot-scene-prepared`, `generated-core-ready`, `assets-cooked`, `code-compiled`, `generated-core-finalized`, `variants-resolved`, `media-laid-out`, `containers-written`, `platform-packaged`) to `logs/build-phases.log` and echoes it to the console. This exists because a headless build can crash or be killed before it can report a structured result — the phase log is the only artifact left behind that tells you which phase it reached.

### Invariants — do not break

- Phase order must not change without checking downstream phase assumptions (e.g. `CompileCode` depends on the manifest `CookAssets` produced; `WriteContainers` depends on `ResolveVariants` and `LayoutMedia` having already run).
- Every phase must continue to write its marker to `build-phases.log`, including on the failure path where practical — removing this would eliminate the only postmortem signal for a build that dies mid-flight.
- `PlatformBuildManifest` mutations must continue to go through "construct a new instance," never in-place property mutation, so that earlier-phase manifest references held elsewhere in the pipeline are never unexpectedly changed underneath their holder.

## 4. The platform-builder plugin boundary

**`IPlatformAssetBuilder`** (`helengine.baseplatform.Builders`) is the interface every target platform implements to plug into the shared build graph: `Descriptor` (implementation identity + supported engine version range), `Definition` (the `PlatformDefinition` describing build/graphics/codegen/media/storage profiles and asset requirements exposed to the editor UI), `CookMaterial` (translates one editor-authored material schema payload into cooked bytes plus shader dependencies), and `BuildAsync` (executes the platform content build for one fully-resolved `PlatformBuildRequest`, streaming progress/diagnostics, returning a `PlatformBuildReport`).

**Builders are loaded dynamically, from a separate assembly**, resolved via `AvailablePlatformDescriptor.BuilderAssemblyPath` (and a required `CodegenToolPath` for the platform's C# codegen tool) rather than being statically referenced by the editor. This is the seam that lets new platforms (PS2, PS Vita, Windows/DirectX11, etc.) be added as independent assemblies without modifying `helengine.editor` or the shared build-graph runner.

**`PlatformBuildRequest` is validated at construction, not at use.** Its constructor throws if: any required string (output root, working root) is blank; target variants or cook profiles are empty or contain nulls; target variant ids or cook profile ids collide; or a target variant references a cook profile id that isn't among the supplied cook profiles. A `PlatformBuildRequest` a builder receives is therefore already internally consistent — builders do not need to re-validate cross-references between their own target variants and cook profiles.

**`PlatformBuildReport` is the builder's uniform return contract**: an overall `Succeeded` flag, an array of `PlatformBuildDiagnostic`s, and separate per-item outcome arrays for scenes (`SceneOutcomes`) and loose assets (`LooseAssetOutcomes`) — every array is validated non-null and null-entry-free at construction.

### Invariants — do not break

- New platforms must be added as separate builder assemblies implementing `IPlatformAssetBuilder`, not by adding platform-specific branches into the shared build-graph runner.
- `PlatformBuildRequest`'s constructor-time validation (no duplicate ids, every target variant's cook profile must be present) must stay a hard throw — a builder must be able to trust an accepted request's internal consistency without its own re-validation.

## 5. Cook work items & cacheability

**`PlatformCookWorkItem`** is the atomic unit of one asset cook: a source asset path/kind, the target platform id and target artifact kind, the final output-relative path and a stable logical artifact id, plus **`SourceContentHash`** and **`SettingsHash`** — two independent hashes (one over the source content, one over the resolved platform cook settings) that exist specifically so a builder or an external cache can decide whether a given work item's output can be reused instead of re-cooked. The constructor requires every one of these fields to be present (non-blank strings, non-null metadata array) — a work item with a missing hash or output path cannot be constructed.

### Invariants — do not break

- `SourceContentHash` and `SettingsHash` must each capture only what they name — content hash must not fold in settings (and vice versa), or cache invalidation logic built on top of these two independent axes would silently break.

## 6. Runtime feature manifest validation gate

Platform builds aggregate a **required-runtime-feature** list from independent collectors (currently a physics3D codegen feature requirement collector, and a code-requirement discovery service that inspects loaded gameplay script types) into one `PlatformBuildRuntimeFeatureManifest`, attached to the build manifest (`ApplyRuntimeFeatureManifest`).

**This is a hard build-time gate, not a warning.** `EditorRuntimeFeatureManifestValidationService.Validate` cross-references the aggregated required-feature list against the set of runtime features the user explicitly force-disabled (via a codegen option value, `PlatformCodegenSettingIds.ForcedDisabledFeatures`, a delimited list normalized case-insensitively). If any required feature is in the disabled set, it throws `InvalidOperationException` with a message naming every conflicting feature, what required it (source kind/id), and why — the build stops. A report of the resolved manifest and any disabled ids is always written to the workspace logs (`RuntimeFeatureManifestReportWriter`) before validation runs, so the report exists even for builds that fail this gate.

### Invariants — do not break

- A build must never silently ship with a required runtime feature disabled — the validation must remain a thrown exception that stops packaging, not a logged warning.
- The runtime-feature report must be written before validation can abort the build, so failed builds still leave a diagnostic artifact behind explaining what conflicted.

## Open questions for follow-up specs

- The `.heproj` project-file format (`helengine.projectfile`) and `code.module.json` module declarations are referenced only implicitly here (as what a build's script compilation operates over); they deserve their own spec.
- The platform installation/SDK registry (`helengine.platforms`: `AvailablePlatformProviderResolver`, `InstalledPlatformProvider`, `DevelopmentPlatformProvider`, `PlatformDescriptorStore`) — i.e. how `AvailablePlatformDescriptor` and its `BuilderAssemblyPath` get resolved in the first place — is out of scope here and belongs in a platform-registry/installation spec.
- Individual phase internals (asset cooking specifics, variant resolution rules, media/storage layout planning, container writing) are each deep enough to warrant their own follow-up notes if they become a source of drift.

# Unified Asset Cook Graph Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task, subject to the user's orchestration approval and existing modernization worker constraints. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate scene dependency discovery and asset cooking into one deterministic graph while preserving current platform build behavior.

**Architecture:** Implement the existing unified cook-graph design as a migration over current packager and platform-builder seams. Canonical authoring resolves inputs; typed graph nodes discover dependencies; platform builders execute target-specific leaf work; a single artifact manifest feeds the existing build graph.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine renderer, authoring, workspace, and platform-builder contracts.

**Spec:** [Unified asset cook graph design](../specs/2026-08-26-unified-asset-cook-graph-design.md) and [existing implementation plan](2026-08-26-unified-asset-cook-graph.md). This document refines the migration against the September working tree; retain the existing design's full node/key/manifest schema.

## Execution constraints and baseline

- Work only in `C:\dev\helworks\helengine` or a deliberately created worktree of that repository. Never use `F:\dev\helengine`, and never cherry-pick its September refactor commits.
- Planning baseline: `main` at `118a0057`, plus the existing dirty working tree inspected on September 14. Before execution record `git status --short` and review the current diff. Do not stash, reset, stage, or overwrite unrelated edits.
- This document authorizes planning only. Obtain implementation authorization before executing it. Follow AGENTS.md and the existing modernization execution requirements; do not silently substitute a different worker model or launch an independent review.
- One class per file; substantive XML documentation on all members; PascalCase fields; no tuples, local functions, nullable annotations, or partial-class extraction.
- Preserve behavior and current persisted formats. No compatibility readers, generated-code rewriting, global service lookup, general DI framework, or silent fallback.
- Use explicit project/scene/operation ownership. A service must not accept the entire EditorSession merely to reach its dependencies.
- Put new test fixtures, logs, and artifacts under `C:\dev\helworks\builds\helengine\refactors\<task>`; do not introduce new uses of Path.GetTempPath. Existing fixtures that write there must be redirected before running those cases.
- Characterization tests should pass before extraction. A NEW boundary contract test should fail before implementation, then pass. Do not demand an artificial failing characterization test.
- Commit each verified task with explicit paths only. Do not merge, publish, update project engine pins, or deploy as part of these plans.

## Validation commands

From the correct repository, run the task's filter against:
```powershell
dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "<task filter>" --logger "console;verbosity=minimal"
dotnet build helengine.ui/helengine.editor.app/helengine.editor.app.csproj --no-restore -v quiet
git diff --check
```
Replace the filter with the exact expression provided in each task. Record test counts and failures; a zero-test run is not a pass. Preserve the repository's current output conventions for existing projects. For new projects use explicit workspace build paths. Capture baseline failures and distinguish them from regressions; do not broaden this work to fix unrelated failures.

## Baseline gate: preserve current work

Before implementation, review and establish the current dirty changes in EditorGeneratedBootScenePreparationService, EditorPhysics3DCodegenFeatureSymbolService, EditorPlatformAssetCookService, EditorPlatformBuildGraphRunner, ModelTessellationProcessor and ShaderMaterialAssetBinarySerializer, plus their modified tests. Record their expected behavior in the relevant regression cases. Do not commit or discard them as part of a planning change.

The file EditorWindowsBuildScenePackager.cs already declares EditorPlatformBuildScenePackager. Rename the file to match the class when migrating; do not introduce a new Windows-only implementation. The class still has 3,236 lines of scene rewrites, dependencies and cook work-item handling. EditorPlatformAssetCookService and SceneComponentPackagingTransformService remain migration inputs.

## Contract ownership

Use the node/request/manifest types and exact key inputs defined in the linked August design; do not create parallel graph models. If a type already exists at execution time, modify it rather than duplicate it. Place new graph files under engine/helengine.editor/managers/project/cooking and tests under the matching editor.tests directory.

Public entry point is one graph execution invoked by EditorPlatformAssetCookService.Cook using its existing parameters/results. Preserve PlatformBuildManifest and platform-builder request boundaries. Graph input resolves source path against its owning project OR generated-source root, never whichever directory happens to be current.

Keys include source hashes, dependency hashes, serializer/cook versions, relevant settings, exact engine pin, schema and target capability inputs. A node changes only when an input affecting its bytes changes. Output manifest ordering is deterministic.

### Task 1: Lock in current build behavior and graph contracts

**Files**
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`, `EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs`, `EditorPhysics3DRuntimeFeatureRequirementCollectorTests.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetCookGraphParityTests.cs`
- Create: graph request/node/key/manifest files specified by the August cook-graph design under `engine/helengine.editor/managers/project/cooking/`

**Interface and ownership contract:** Graph adapter initially delegates leaf behavior to current services; root resolution and deterministic key contracts follow the linked design. No active runtime path switches until parity fixture coverage exists.

**Regression cases:** Shared texture across two scenes, generated boot-scene source roots, platform existence overrides, model tessellation/material serialization, physics feature symbols, font atlas references, no-op second run and failed source lookup.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Capture current packaged manifest and output hashes for small mixed-asset fixtures with fake platform builders; freeze fixture source bytes and expected semantic references.
- [ ] Add graph key tests: same inputs yield same key; source/dependency/settings/engine/capability changes yield a different key; irrelevant output-directory change does not.
- [ ] Add missing-dependency and cycle diagnostics before builder invocation. Report the complete dependency chain, not a generic packaging failure.
- [ ] Review the pending changes with these tests before moving any packaging implementation.
- [ ] Run `FullyQualifiedName~EditorAssetCookGraphParityTests|FullyQualifiedName~EditorPlatformAssetCookServiceTests|FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests|FullyQualifiedName~EditorPhysics3DRuntimeFeatureRequirementCollectorTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `test: characterize asset cook graph migration`.

### Task 2: Centralize dependency discovery and scene transformation

**Files**
- Modify/rename: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs` to `EditorPlatformBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookDependencyResolver.cs`, `EditorAssetCookGraphBuilder.cs`
- Modify: `engine/helengine.editor.tests/managers/project/SceneComponentPackagingTransformServiceTests.cs`, `EditorWindowsBuildScenePackagerTests.cs`

**Interface and ownership contract:** Dependency resolver consumes canonical authoring references and emits the design's typed nodes. Graph builder deduplicates by node identity, detects cycles and orders dependencies before consumers. Scene transforms are pure with respect to source assets and must not publish outputs.

**Regression cases:** Root scenes keep requested ordering; shared dependencies deduplicate; transforms respect target existence/component overrides; generated materials/fonts/models retain their owning source root; authored scenes are unchanged after packaging.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move reference traversal from RewriteSceneAsset/RewriteEntityAsset and per-asset Rewrite* methods into typed dependency discovery.
- [ ] Extract pure component/transform application from both current paths into one implementation invoked during graph construction. Keep platform leaf encoders in platform builders.
- [ ] Use the old packaging fixture as oracle for semantic results while migration is in progress; do not compare against output generated by the new code itself.
- [ ] Remove each old traversal branch when its graph equivalent is wired; do not retain two complete traversal implementations after this task.
- [ ] Run `FullyQualifiedName~EditorAssetCookGraphParityTests|FullyQualifiedName~SceneComponentPackagingTransformServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: centralize cooked asset dependency discovery`.

### Task 3: Execute nodes and publish artifacts atomically

**Files**
- Create: `engine/helengine.editor/managers/project/cooking/EditorAssetCookGraphExecutor.cs`, `EditorAssetCookArtifactStore.cs`
- Modify: `engine/helengine.editor/managers/project/EditorPlatformCookedArtifactPool.cs`, `EditorPlatformCookWorkItemFactory.cs`
- Create: `engine/helengine.editor.tests/managers/project/cooking/EditorAssetCookGraphExecutorTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformCookWorkItemFactoryTests.cs`

**Interface and ownership contract:** Executor consumes the graph and existing platform builder contracts; artifact store publishes immutable verified artifacts and their provenance. Existing cooked pool becomes an adapter or is removed after callers migrate; it must not retain separate key policy.

**Regression cases:** One cook for shared dependency, byte-identical no-op outputs, dependency-only invalidation, platform-specific key separation, cancellation/failure before publication, retry after partial output, corrupt artifact detection.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Execute topologically; forward existing typed leaf requests with cancellation and diagnostics. Preserve serialized prebuild ownership rather than adding concurrency.
- [ ] Write each node to an operation staging directory under the workspace build root; publish only complete validated output. Never mark a failed node reusable.
- [ ] Build manifest entries from successfully published artifacts in stable order. Assert second-run builder invocation count is zero and output hashes remain equal.
- [ ] Ensure cancellation cleans only operation-owned staging paths; never delete source assets, other builds or shared valid artifacts.
- [ ] Run `FullyQualifiedName~EditorAssetCookGraphExecutorTests|FullyQualifiedName~EditorPlatformCookWorkItemFactoryTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: execute deterministic asset cook nodes`.

### Task 4: Switch the build entry point and retire duplicate packaging

**Files**
- Modify: `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`, `EditorPlatformBuildGraphRunner.cs`
- Modify/remove migrated implementation: `engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs`, `SceneComponentPackagingTransformService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`
- Modify: `docs/specs/platform-build-pipeline.md`

**Interface and ownership contract:** Keep existing Cook return type and build phase transitions; replace its internal packaging path with graph execution. Native compilation/SDK/media stages remain outside this graph.

**Regression cases:** Editor and CLI entry points produce equivalent manifests; Windows and a fake external platform use the same traversal; physics/generated boot scene cases remain passing; packaging failure prevents later build stages.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Route all EditorPlatformAssetCookService.Cook execution through the graph. Trace direct packager callers in engine, tools and scripts and migrate them to the canonical service.
- [ ] Delete obsolete duplicate rewrites/cook-key logic and update file/type names in tests. Keep only behaviorally distinct platform leaf code.
- [ ] Run the entire focused cook/packaging suite and the build-graph tests; run one authorized local Windows package smoke if SDKs are available. Report missing toolchains without treating them as passes.
- [ ] Document graph ownership, key/provenance diagnostics and generated-root resolution in platform-build-pipeline.md. Do not invoke portable publication scripts or install/register a new engine version.
- [ ] Run `FullyQualifiedName~Cook|FullyQualifiedName~Packaging|FullyQualifiedName~Packager|FullyQualifiedName~EditorPlatformBuildGraphRunnerTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: route platform packaging through cook graph`.

## Completion
One dependency traversal and one cook-key/artifact policy serve all build entry points. Current source-root/physics changes are retained and tested. Repeated unchanged builds avoid recooking, failure never publishes partial nodes, and target-specific bytes still come from target-specific builders.

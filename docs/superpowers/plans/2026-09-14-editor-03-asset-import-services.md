# Asset Import Services Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task, subject to the user's orchestration approval and existing modernization worker constraints. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace AssetImportManager's mixed responsibilities with focused services while preserving import bytes, asset identity and public authoring behavior.

**Architecture:** Separate registration, settings, execution, runtime resolution and invalidation. Canonical project authoring continues to own transactions and identities. Extract audio processing as a leaf service before replacing orchestration; native/platform encoding stays behind a typed codec boundary.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine renderer, authoring, workspace, and platform-builder contracts.

**Spec:** [Editor modularization design](../specs/2026-08-26-editor-modularization-design.md), Asset Import Decomposition; [deterministic authoring design](../specs/2026-08-26-deterministic-editor-authoring-design.md).

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

## Preconditions and migration boundary

AssetImportManager is 4,954 lines and IAssetImporterRegistration.Register currently accepts that manager. Preserve extension/importer identity selection, cache keys, serialization versions and runtime texture ownership. Constructor must remain free of project mutation before recovery/lock acquisition. Verify the parent modularization prerequisites before migration of orchestration; codec characterization can start independently.

Keep existing ITextureImporter, IModelImporter, IAudioImporter and IFontImporter contracts. Do not invent one untyped importer interface. Registration migration changes the sink to AssetImporterRegistry and updates all implementers atomically.

Create the five named services under engine/helengine.editor/managers/asset. Place target-independent audio transforms in its processing directory. DS framing gets a separate codec implementation; do not delete supported DS behavior or assume the sibling platform repository can be changed within this task.

## Audio leaf contract

```csharp
public interface IEditorAudioPayloadEncoder {
    string EncodingFamilyId { get; }
    byte[] Encode(short[] samples);
}
```
Create one file per type: IEditorAudioPayloadEncoder.cs, Pcm16AudioPayloadEncoder.cs, NintendoDsImaAdpcmAudioPayloadEncoder.cs, EditorAudioSampleProcessor.cs. Initially keep these in the shared processing directory to preserve host registration compatibility; moving the DS implementation to the owning platform package requires a separately verified platform-builder contract. The manager must cease containing codec tables or dispatch branches.

### Task 1: Extract audio processing without changing output

**Files**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Create: the four processing types and interface listed above
- Create: `engine/helengine.editor.tests/managers/asset/EditorAudioSampleProcessorTests.cs`, `EditorAudioPayloadEncoderTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerAudioTests.cs`

**Interface and ownership contract:** Copy DecodePcm16Samples, ConvertAudioChannels and ResampleAudioSamples into EditorAudioSampleProcessor with unchanged argument/return types. Encoders implement the contract above; registry lookup selects by existing encoding family ID.

**Regression cases:** Golden bytes for empty, one-sample, alternating min/max PCM, mono/stereo conversion, resampling boundaries, DS predictor/header/nibble order. Unknown encoding keeps explicit failure; no PCM fallback.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Capture current algorithm outputs into small checked-in fixture arrays before moving code; compare arrays byte-for-byte with Assert.Equal(expectedBytes, actualBytes). Include known DS framing values rather than generating expected data with the new encoder.
- [ ] Move PCM encoding and DS tables/encoder into their implementations. Make sample processing depend on registered encoder IDs.
- [ ] Keep cache identifiers and processor settings unchanged. Update manager calls to the extracted processor and remove copied helpers/tables.
- [ ] Run `FullyQualifiedName~EditorAudioSampleProcessorTests|FullyQualifiedName~EditorAudioPayloadEncoderTests|FullyQualifiedName~EditorWindowsBuildScenePackagerAudioTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: extract editor audio processing`.

### Task 2: Separate registrations and settings persistence

**Files**
- Create: `engine/helengine.editor/managers/asset/AssetImporterRegistry.cs`, `AssetImportSettingsRepository.cs`
- Modify: `engine/helengine.editor/managers/asset/IAssetImporterRegistration.cs`, `AssetImportManager.cs`
- Modify: `helengine.ui/helengine.editor.app/EditorHostImporterFactory.cs` and every tracked implementation of `IAssetImporterRegistration` found by `rg -l 'IAssetImporterRegistration' engine helengine.ui tools`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`, `AssetImportManagerModelTests.cs`
- Create: `engine/helengine.editor.tests/managers/asset/AssetImporterRegistryTests.cs`

**Interface and ownership contract:** Change registration to void Register(AssetImporterRegistry registry). Move existing typed Register* methods into the registry without changing parameter types. Registry has Freeze(); registration after Freeze throws InvalidOperationException. Settings repository owns current typed Load/Save/validation methods and paths, not imported runtime assets.

**Regression cases:** Default vs explicit importer selection, ambiguous extensions, missing importer, unsupported extension, freeze enforcement, sidecar round-trip, no filesystem changes during construction.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Copy registration maps and extension selection into AssetImporterRegistry; preserve current case sensitivity and precedence. Freeze after host bootstrap.
- [ ] Move typed settings loading/validation/saving into AssetImportSettingsRepository. Keep identities and mutation journal coordination in the authoring session.
- [ ] Migrate registration implementations and fixtures together; do not leave a second manager-based registration overload.
- [ ] In the registry test, call registry.Freeze(), then assert that an existing typed registration operation throws InvalidOperationException; reuse the typed importer fixture from AssetImportManagerTests.
- [ ] Run `FullyQualifiedName~AssetImporterRegistryTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~AssetImportManagerModelTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: separate importer registry and settings`.

### Task 3: Extract execution, runtime resolution and invalidation

**Files**
- Create: `engine/helengine.editor/managers/asset/AssetImportExecutionService.cs`, `ImportedAssetRuntimeResolver.cs`, `AssetImportInvalidationService.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`, `EditorProjectAuthoringSession.cs`
- Modify: `engine/helengine.editor/EditorSession.cs` or plan 02's project service graph after it lands
- Create: `engine/helengine.editor.tests/managers/asset/AssetImportExecutionServiceTests.cs`, `ImportedAssetRuntimeResolverTests.cs`, `AssetImportInvalidationServiceTests.cs`

**Interface and ownership contract:** Execution owns existing import-key and processor invocation methods; runtime resolver owns current TryLoadCached* and runtime texture reattachment; invalidation observes source/settings changes and schedules execution. Carry existing typed signatures into each service rather than introducing object-based results.

**Regression cases:** Unchanged input reuses cache; changed source/settings invalidates exactly affected imports; corrupt cache follows current recovery policy; failed/cancelled processing publishes no partial artifact; source deletion during import is reported; runtime assets disposed once.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move import execution with cache publication into the execution service and retain the current transaction/recovery boundary supplied by authoring.
- [ ] Move cache reads and renderer attachment into runtime resolver. Preserve font texture ownership and model/material dependency resolution.
- [ ] Move invalidation subscriptions into its disposable service; cancel outstanding work and detach observers before project teardown.
- [ ] Migrate canonical authoring session and all manager callers using rg references. Delete AssetImportManager only when no production caller remains; update tests to services while retaining authoring-level integration tests.
- [ ] Keep plan 05's cook graph consuming the public authoring session, not the newly split internal services.
- [ ] Run `FullyQualifiedName~AssetImportExecutionServiceTests|FullyQualifiedName~ImportedAssetRuntimeResolverTests|FullyQualifiedName~AssetImportInvalidationServiceTests|FullyQualifiedName~EditorAuthoring` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: replace asset import manager with scoped services`.

## Completion
One canonical import pipeline; no duplicate registry/cache policy; no AssetImportManager facade remains after migration. Golden audio bytes, stable import identities, no-op cache reuse, and disposal cases pass. API migration must include tool callers as well as the desktop host.

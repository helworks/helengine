# Component Inspector Editing Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task, subject to the user's orchestration approval and existing modernization worker constraints. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Separate inspector presentation from persistent mutations, platform overrides and history recording.

**Architecture:** Use existing ComponentEditorRegistry providers and current authoring/history contracts. A project/scene-scoped edit controller executes typed requests; focused UI editors display state and issue those requests. Preserve the current one-edit/one-history-unit behavior and generated-asset ownership.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine renderer, authoring, workspace, and platform-builder contracts.

**Spec:** [Editor modularization design](../specs/2026-08-26-editor-modularization-design.md), UI Decomposition; [deterministic authoring design](../specs/2026-08-26-deterministic-editor-authoring-design.md).

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

## Scope and dependencies

ComponentPropertiesView currently has 5,277 lines, including property writes, mesh modifiers, override persistence and history snapshots. Reuse ComponentEditorRegistry and IComponentPropertyEditorProvider; do not add a competing inspector registration mechanism.

Start after canonical authoring and scene-history ownership are stable. Plan 02's history coordinator may supply the owner, but the edit controller must accept the narrow existing history/authoring dependencies, not the full coordinator graph. Do not simultaneously edit ComponentPropertiesView from multiple workstreams.

## Command contract

Create ComponentPropertyEditRequest.cs and ComponentPropertyEditController.cs under managers/inspection. Request fields use existing Component, string memberName, object value and EditorOverrideScope types; object is permitted at the existing reflected-field boundary only. Separate mesh operations into MeshModifierEditRequest and MeshModifierEditController, reusing existing MeshComponentModifier values. Requests capture their target and scope at creation, not from a later global selection.

Controller entry point:
```csharp
public void Apply(ComponentPropertyEditRequest request)
```
Order is validate target/scope/read-only, capture existing history state, apply via current authoring path, persist current override state, record one mutation, then refresh presentation. On validation failure no mutation/history entry occurs. Do not add a second journal or use reflection to reach application internals.

### Task 1: Centralize property edits and history

**Files**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Create: `engine/helengine.editor/managers/inspection/ComponentPropertyEditRequest.cs`, `ComponentPropertyEditController.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`, `ComponentPropertiesViewGeneratedAssetTests.cs`
- Create: `engine/helengine.editor.tests/managers/inspection/ComponentPropertyEditControllerTests.cs`

**Interface and ownership contract:** Apply(request) owns the existing CaptureCurrentEntityHistoryState / SetValue / PersistPlatformOverrideIfNeeded / RecordRowMutation sequence; request includes read-only context and exact target/scope.

**Regression cases:** Scalar edit undo/redo, asset pick persistence, generated asset references, inherited/platform/environment override, read-only rejection, deleted target, failed validation and unchanged-value no-op.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Characterize both scene serialization and history entry count using current fixtures. Assert state after edit, undo and redo, not calls to helper methods.
- [ ] Move mutation sequence into the controller and invoke it from every row change/picker completion path. Keep UI row types out of the request.
- [ ] Ensure asynchronous picker completion cannot apply to a different newly selected target. Reject invalid disposed targets using current lifetime state.
- [ ] Remove direct row.Property.SetValue mutations from view handlers after migration; retain reflection only in the metadata/value adapter.
- [ ] Run `FullyQualifiedName~ComponentPropertyEditControllerTests|FullyQualifiedName~ComponentPropertiesViewScenePersistenceTests|FullyQualifiedName~ComponentPropertiesViewGeneratedAssetTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: centralize inspector property edits`.

### Task 2: Extract mesh-modifier editor and command handling

**Files**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Create: `engine/helengine.editor/components/ui/inspection/MeshModifierPropertyEditor.cs`
- Create: `engine/helengine.editor/managers/inspection/MeshModifierEditRequest.cs`, `MeshModifierEditController.cs`
- Modify: `engine/helengine.editor/managers/inspection/ComponentEditorRegistry.cs`
- Create: `engine/helengine.editor.tests/managers/inspection/MeshModifierEditControllerTests.cs`

**Interface and ownership contract:** Use the existing provider extension contract for rendering mesh fields. MeshModifierEditRequest carries target mesh, scope, operation (Add/Remove/Move/SetValue), index and typed modifier payload; define its enum in a separate file. Controller owns index validation and history transaction.

**Regression cases:** Add/remove/reorder modifier, stale index, inherited stack overrides, preview rebuild on committed edit, cancelled picker, undo restoring the complete prior stack.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move AddMeshComponentModifierRows and modifier display/summary helpers into the focused editor.
- [ ] Replace encoded member-name parsing in UI event handlers with explicit operation/index fields populated when creating the request.
- [ ] Move SetMeshComponentModifierRowValue and stack mutation into controller; reuse current preview update and history services.
- [ ] Preserve modifier order and serialized values; compare complete saved component state before/after undo with Assert.Equal(expectedSerializedState, actualSerializedState).
- [ ] Run `FullyQualifiedName~MeshModifierEditControllerTests|FullyQualifiedName~ComponentPropertiesView` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: extract mesh modifier inspector`.

### Task 3: Split presentation sections and remove row lifetime leaks

**Files**
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Create: `engine/helengine.editor/components/ui/inspection/ReflectedPropertyEditor.cs`, `AssetReferencePropertyEditor.cs`, `CollectionPropertyEditor.cs`, `ComponentOverrideSection.cs`
- Modify: `engine/helengine.editor/managers/inspection/ComponentEditorRegistry.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewDynamicInspectorTests.cs`
- Create: `engine/helengine.editor.tests/managers/inspection/InspectorSectionLifetimeTests.cs`

**Interface and ownership contract:** Focused sections own their controls/subscriptions, implement IDisposable, consume existing provider metadata, and issue edit requests. ComponentPropertiesView retains ShowComponents/Hide/UpdateLayout and section composition only.

**Regression cases:** Repeated selection changes do not multiply handlers; Hide/dispose closes pickers and releases controls; override outline/read-only state matches current UI; collections and custom providers remain available.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move reflected rows, asset picker rows, collection rows and override chrome into the listed sections, preserving current control metrics and ordering.
- [ ] Migrate ClearActiveRows/ClearActiveSections ownership with the corresponding controls; detach callbacks before disposing their targets.
- [ ] Retain custom provider precedence from ComponentEditorRegistry and existing dynamic inspector tests.
- [ ] Run a desktop smoke: select components, edit scalar, pick asset, change override scope, undo, switch selection and reopen inspector. Record visible behavior without taking screenshots unless authorized.
- [ ] Run `FullyQualifiedName~ComponentPropertiesViewDynamicInspectorTests|FullyQualifiedName~InspectorSectionLifetimeTests|FullyQualifiedName~ComponentPropertyEditControllerTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: compose inspector from focused sections`.

## Completion
The view no longer implements persistence/history or mesh-stack mutation. All edits route through the existing canonical mutation boundary, and focused editors have testable lifetimes. No appearance redesign, new asset format or new undo stack.

# Editor Session Lifetimes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task, subject to the user's orchestration approval and existing modernization worker constraints. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reduce EditorSession to composition-facing coordination while preserving scene, panel, history and cleanup behavior.

**Architecture:** Extract project and scene responsibilities around existing controllers, construction ledger and authoring session. Each extraction migrates its callers and removes its old implementation; lifecycle ownership is explicit, with no mutable process-global coordination.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine renderer, authoring, workspace, and platform-builder contracts.

**Spec:** [Editor modularization design](../specs/2026-08-26-editor-modularization-design.md), Editor Host and Lifetimes and EditorSession Decomposition.

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

## Preconditions and file map

EditorSession currently has 6,649 lines; existing controllers and cleanup ledger must be reused. Follow the parent design's prerequisites: current-format contracts, canonical authoring session, publication stability and unified cooking must be verified, not inferred from the presence of classes. Plan 05 must own cooking before extracting build coordination. Backend constructor changes from plan 01 land first.

New coordinators live in engine/helengine.editor/hosting; tests in engine/helengine.editor.tests/hosting. Keep MainForm a host, not a new home for the session's feature logic. Existing EditorSession public methods retain their behavior and delegate to the extracted owner.

## Lifetime invariants

Host owns renderer/input/importer registrations. Project owns authoring/workspace/build services. Scene owns selection/history and viewport bindings. Operation owns import/build cancellation. Event detachment precedes entity/native asset disposal; construction failure unwinds only successfully registered resources.

Reuse EditorSessionConstructionLedger and its phase ordering. Do not replace tested cleanup with a generic reverse IDisposable list.

### Task 1: Extract project service construction and cleanup

**Files**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor/hosting/EditorProjectServiceGraph.cs`, `EditorProjectLifecycleCoordinator.cs`
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionConstructionLedgerTests.cs`
- Create: `engine/helengine.editor.tests/hosting/EditorProjectLifecycleCoordinatorTests.cs`

**Interface and ownership contract:** EditorProjectServiceGraph : IDisposable owns the existing authoring, generated asset and renderer-resource services, with typed properties. Its constructor receives the current individual inputs from EditorSession, not EditorSession. EditorProjectLifecycleCoordinator owns exactly one graph and disposes it on project close.

**Regression cases:** Failure after each construction phase releases prior resources once; double close is harmless; reopening creates fresh project state; no assets or directories are created before existing recovery/locking boundary.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Extract the constructor service-creation block, preserving the current order and ledger registrations. Separate borrowed Core/renderer resources from owned project resources explicitly.
- [ ] Move RegisterSessionCleanupActions and project-owned disposal into the graph; retain scene cleanup phases separately.
- [ ] Keep EditorSession constructor as graph consumer and wire MainForm to its owner. Add a recording disposable fixture with assertions equivalent to Assert.Equal(expectedCleanupOrder, actualCleanupOrder).
- [ ] Run `FullyQualifiedName~EditorSessionConstructionLedgerTests|FullyQualifiedName~EditorProjectLifecycleCoordinatorTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: extract editor project lifetime`.

### Task 2: Extract scene workspace, selection and history

**Files**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor/hosting/EditorSceneWorkspaceCoordinator.cs`, `EditorSelectionCoordinator.cs`, `EditorHistoryCoordinator.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`, `EditorSessionSceneSaveTests.cs`, `EditorSessionUndoRedoIntegrationTests.cs`
- Create: `engine/helengine.editor.tests/hosting/EditorSceneLifetimeTests.cs`

**Interface and ownership contract:** Move current scene-open/save signatures unchanged into EditorSceneWorkspaceCoordinator. Selection coordinator owns selected entity/asset state and its change events; history coordinator owns existing history transactions and dirty revisions. Keep existing history model types; do not create a second command stack.

**Regression cases:** Open A, edit, undo/redo, save, open B: no selection or undo command from A survives; failed scene load preserves the documented current scene behavior; entity removal clears selection before disposal.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move scene load/save/active scene transitions together with their teardown hooks, delegating serialization to existing services.
- [ ] Move hierarchy/viewport/property selection synchronization to one scene-scoped owner. Migrate subscriptions as a pair: attach and detach.
- [ ] Move history event wiring and dirty-state calculation without changing transaction grouping. Assert equal serialized scene state before and after extraction using existing integration fixtures.
- [ ] Delete relocated method bodies from EditorSession; forwarding public host methods may remain, but must contain only delegation.
- [ ] Run `FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionUndoRedoIntegrationTests|FullyQualifiedName~EditorSceneLifetimeTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: isolate scene workspace lifetime`.

### Task 3: Extract panels, dialogs and command/build coordination

**Files**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor/hosting/EditorPanelCoordinator.cs`, `EditorDialogCoordinator.cs`, `EditorBuildCoordinator.cs`, `EditorToolCommandCoordinator.cs`
- Modify: `engine/helengine.editor/managers/workspace/EditorWorkspacePanelRegistry.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionWorkspaceTests.cs`, `EditorSessionAddMenuTests.cs`
- Create: `engine/helengine.editor.tests/hosting/EditorCoordinatorLifetimeTests.cs`

**Interface and ownership contract:** Panel coordinator owns registry instances and attach/detach; dialog coordinator owns scale-sensitive dialog lifecycle. Build coordinator delegates to existing build graph; tool coordinator consumes IEditorProjectAuthoringSession, not a full session. Preserve current synchronous/asynchronous public signatures during extraction.

**Regression cases:** Create/close/reopen multiple panels; UI scale change detaches old dialogs; build cancellation releases operation resources; commands target the active project; disposing project prevents later UI updates.

- [ ] Add the specified characterization cases using existing test fixtures, with fixture output redirected to the workspace build directory. Run the filter below and record the baseline.
- [ ] Add boundary assertions for the new contract and verify they fail for the intended missing behavior before implementing it.
- [ ] Move InitializePanelRegistry, CreateWorkspacePanelInstance and Wire/UnwireWorkspacePanelEvents together to panel coordinator; keep specialized controllers in place.
- [ ] Move scale-sensitive dialog creation, hide, detach and dispose as one ownership unit.
- [ ] After plan 05 validation, move build/menu wiring into coordinators, retaining existing queue serialization and cancellation behavior.
- [ ] Audit new coordinators for Core.Instance, static mutable paths and EditorSession fields. Replace reach-through with exact constructor dependencies.
- [ ] Record responsibility-to-owner mapping in the existing modularization documentation; verify no feature logic was transferred into MainForm.
- [ ] Run `FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorSessionAddMenuTests|FullyQualifiedName~EditorCoordinatorLifetimeTests` through the test command above. Run the editor-host build after changing composition or project references.
- [ ] Inspect the diff for duplicate old implementations, lifetime changes, and accidental fixture/output files. Commit only the listed task paths with message `refactor: delegate editor panel and command coordination`.

## Completion
Each owned resource/subscription has one lifecycle owner. Session tests continue to cover host behavior; coordinator tests cover failure and teardown without constructing the entire UI. Line-count reduction is evidence, not the acceptance criterion; no partial-class split counts as completion.

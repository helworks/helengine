# Editor Modularization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task. Every implementation worker must be `gpt-5.6-luna` with reasoning effort `xhigh`. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Turn the editor's oversized session, import manager, and views into explicit project- and scene-scoped services while preserving current behavior and deleting superseded implementations.

**Architecture:** `EditorHost` constructs a project service graph with explicit lifetimes. `EditorSession` becomes a thin UI coordinator over lifecycle, workspace, selection, history, command, and build coordinators. Asset importing becomes five focused services. Large controls become shells over presentation models and focused child controls. Constructor dependencies and owned events replace mutable global coordination.

**Tech Stack:** C#/.NET 9, xUnit, existing Helengine UI primitives, current editor authoring/cook/publishing contracts.

**Spec:** `docs/superpowers/specs/2026-08-26-editor-modularization-design.md`

## Global Constraints

- Sol coordinates/reviews only; GPT-5.6 Luna `xhigh` performs all implementation edits.
- Stop if Luna `xhigh` cannot be spawned.
- Complete the four prerequisite modernization plans before this plan.
- Preserve user-visible behavior and current public APIs; do not add format or feature changes.
- Add characterization tests before moving each responsibility.
- Temporary forwarding members must be deleted inside the same task; no permanent compatibility facade remains.
- Do not use partial classes to satisfy line ceilings.
- Extracted services must not accept `EditorSession`, read global project paths, or subscribe to process-global mutable event hubs.
- Events have one owner and every project/scene subscription is removed on disposal.
- Read the TDD skill and `writing-good-tests.md` before modifying tests.

---

### Task 1: Host Service Graph and Project Lifecycle

**Files:**
- Create: `engine/helengine.editor/hosting/EditorProjectServiceGraph.cs`
- Create: `engine/helengine.editor/hosting/EditorProjectLifecycleCoordinator.cs`
- Create: `engine/helengine.editor/hosting/IEditorProjectLifecycleCoordinator.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: editor host/composition files that currently construct `EditorSession`
- Create: `engine/helengine.editor.tests/hosting/EditorProjectServiceGraphTests.cs`
- Create: `engine/helengine.editor.tests/hosting/EditorProjectLifecycleCoordinatorTests.cs`

**Interfaces:**
- Consumes: host-lifetime renderer/input/platform/importer registrations and a project path.
- Produces: one owned project service graph with deterministic reverse-order disposal.

- [ ] **Step 1: Characterize host and project lifetime behavior**

Test successful open, invalid project rejection, close, reopen, two simultaneous hosts, and failure during construction. Assert project state is not shared, partial construction is disposed, operations stop before project services, and a second close is harmless.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorProjectServiceGraphTests|FullyQualifiedName~EditorProjectLifecycleCoordinatorTests" -v:minimal
```

- [ ] **Step 3: Introduce explicit lifetime contracts**

Implement:

```csharp
public interface IEditorProjectLifecycleCoordinator : IDisposable {
    EditorProjectServiceGraph Open(string projectFilePath);
    void Close();
    bool IsOpen { get; }
}
```

`EditorProjectServiceGraph` owns project settings, `IEditorProjectAuthoringSession`, workspace state, build services, and later coordinators. Its constructor receives already-created host-lifetime dependencies. Keep composition manual and visible; do not add a dependency-injection framework.

- [ ] **Step 4: Route `EditorSession` project open/close through the coordinator**

Move validation, platform bootstrap, service creation, and disposal out of `EditorSession`. Delete the moved fields and methods after all callers use the coordinator. Keep only UI-facing state forwarding needed by `MainForm`.

- [ ] **Step 5: Run tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorProjectServiceGraphTests|FullyQualifiedName~EditorProjectLifecycleCoordinatorTests|FullyQualifiedName~EditorSessionProjectLibraryStartupTests|FullyQualifiedName~EditorSessionStartupSceneTests" -v:minimal
rtk git add -- engine/helengine.editor/hosting engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests
rtk git commit -m "Extract editor project lifecycle"
```

### Task 2: Scene Workspace, Selection, and History Coordinators

**Files:**
- Create: `engine/helengine.editor/managers/scene/EditorSceneWorkspaceCoordinator.cs`
- Create: `engine/helengine.editor/managers/scene/IEditorSceneWorkspaceCoordinator.cs`
- Create: `engine/helengine.editor/managers/scene/EditorSelectionCoordinator.cs`
- Create: `engine/helengine.editor/managers/scene/IEditorSelectionCoordinator.cs`
- Create: `engine/helengine.editor/managers/scene/EditorHistoryCoordinator.cs`
- Create: `engine/helengine.editor/managers/scene/IEditorHistoryCoordinator.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: current scene hierarchy, viewport selection, and history bridge callers
- Create: matching coordinator tests under `engine/helengine.editor.tests/managers/scene`

**Interfaces:**
- Consumes: current scene persistence, authoring session, workspace document, and scene mutation commands.
- Produces: scene-scoped active document, selection, undo/redo, dirty revision, and owned notifications.

- [ ] **Step 1: Add characterization tests**

Cover scene open/save/close, active-scene switching, hierarchy-to-viewport selection, clearing selection during teardown, undo/redo order, dirty revision changes, failed save, and event unsubscription. Assert every persisted mutation is either recorded in history or explicitly submitted as non-undoable.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSceneWorkspaceCoordinatorTests|FullyQualifiedName~EditorSelectionCoordinatorTests|FullyQualifiedName~EditorHistoryCoordinatorTests" -v:minimal
```

- [ ] **Step 3: Implement and route the three coordinators**

Keep workspace ownership separate from selection and history. Use typed instance events. Each coordinator must be testable without a renderer or full `EditorSession`. Route scene, selection, undo/redo, dirty-state, and mutation callers through the interfaces; delete duplicate session state, history bridges, and static forwarding events.

- [ ] **Step 4: Run regression tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests|FullyQualifiedName~EditorSessionSceneTeardownSelectionSourceTests|FullyQualifiedName~EditorSessionUndoRedoIntegrationTests|FullyQualifiedName~EditorSessionWorkspaceTests|FullyQualifiedName~EditorViewportPicker" -v:minimal
rtk git add -- engine/helengine.editor/managers/scene engine/helengine.editor/EditorSession.cs engine/helengine.editor/components engine/helengine.editor.tests
rtk git commit -m "Extract editor scene coordination"
```

### Task 3: Project Tool Commands and Build Coordination

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorToolCommandCoordinator.cs`
- Create: `engine/helengine.editor/managers/project/IEditorToolCommandCoordinator.cs`
- Create: `engine/helengine.editor/managers/project/EditorBuildCoordinator.cs`
- Create: `engine/helengine.editor/managers/project/IEditorBuildCoordinator.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: current project-menu and build-queue callers
- Create: matching coordinator tests under `engine/helengine.editor.tests/managers/project`

**Interfaces:**
- Consumes: project command descriptors, `IEditorCommandContext.Authoring`, platform discovery, cook graph, and build executor.
- Produces: scoped command execution and observable build queue state.

- [ ] **Step 1: Add command and build characterization tests**

Assert command discovery order, enabled-state evaluation, exception diagnostics, cancellation, and disposal. Assert build request validation, FIFO queueing, single active execution, log ordering, cancellation, failure recovery, and manifest handoff to the selected platform.

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorToolCommandCoordinatorTests|FullyQualifiedName~EditorBuildCoordinatorTests" -v:minimal
```

- [ ] **Step 3: Extract and route both coordinators**

The tool coordinator creates a command scope containing the current public authoring session; it never exposes session or serializer/importer internals. The build coordinator owns queue mutations, validation, cook invocation, results/logs, and cancellation. Views issue commands and observe immutable state snapshots. Delete moved session and view implementations.

- [ ] **Step 4: Run regression tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorSessionProjectMenuTests|FullyQualifiedName~EditorTitleBarToolsMenuTests|FullyQualifiedName~EditorSessionBuildQueueTests|FullyQualifiedName~EditorTitleBarBuildMenuTests|FullyQualifiedName~BuildDialog" -v:minimal
rtk git add -- engine/helengine.editor/managers/project engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/EditorTitleBar.cs engine/helengine.editor.tests
rtk git commit -m "Extract editor command and build coordination"
```

### Task 4: Decompose Asset Importing and Delete `AssetImportManager`

**Files:**
- Create: `engine/helengine.editor/managers/asset/AssetImporterRegistry.cs`
- Create: `engine/helengine.editor/managers/asset/AssetImportSettingsRepository.cs`
- Create: `engine/helengine.editor/managers/asset/AssetImportExecutionService.cs`
- Create: `engine/helengine.editor/managers/asset/ImportedAssetRuntimeResolver.cs`
- Create: `engine/helengine.editor/managers/asset/AssetImportInvalidationService.cs`
- Create: matching focused interfaces under `engine/helengine.editor/managers/asset`
- Modify: editor host registration, project authoring-session implementation, cook graph, properties, and preview callers
- Delete: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Replace: `engine/helengine.editor.tests/managers/asset/AssetImportManagerTests.cs` with focused service tests

**Interfaces:**
- Consumes: immutable importer registrations, current typed settings, source hashes, and current imported-cache contracts.
- Produces: selected importers, validated settings, imported artifacts, runtime objects, and invalidation notifications.

- [ ] **Step 1: Partition manager characterization tests by responsibility**

Create focused failing suites for registry selection, settings path/read/write, import-key computation and cancellation, runtime resolution, and source/settings invalidation. Preserve contractual errors and public authoring behavior.

- [ ] **Step 2: Run the new suites and verify RED**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImporterRegistryTests|FullyQualifiedName~AssetImportSettingsRepositoryTests|FullyQualifiedName~AssetImportExecutionServiceTests|FullyQualifiedName~ImportedAssetRuntimeResolverTests|FullyQualifiedName~AssetImportInvalidationServiceTests" -v:minimal
```

- [ ] **Step 3: Implement services with one-way dependencies**

The registry is immutable after bootstrap. Settings performs no imports. Execution publishes current immutable results and owns cancellation. Runtime resolution cannot initiate settings writes. Invalidation schedules execution but owns no UI state.

- [ ] **Step 4: Switch every caller and delete the manager**

Wire services into `EditorProjectServiceGraph`, the authoring session, cook graph, property editors, and preview paths. Delete `AssetImportManager`, aliases, and tests coupled to its internal shape. Do not retain a forwarding wrapper.

- [ ] **Step 5: Run regression tests and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~AssetImport|FullyQualifiedName~ImportedAssetRuntimeResolver|FullyQualifiedName~EditorSessionGeneratedAssetTests|FullyQualifiedName~EditorSessionModelAssetSelectionTests|FullyQualifiedName~EditorProjectAuthoringSession" -v:minimal
rg -n "AssetImportManager" engine helengine.ui -g '*.cs'
rtk git add -- engine/helengine.editor/managers/asset engine/helengine.editor/hosting engine/helengine.editor.tests helengine.ui
rtk git commit -m "Decompose editor asset importing"
```

Expected source search: no production or test reference remains.

### Task 5: Split Component and General Property Views

**Files:**
- Create: `engine/helengine.editor/components/ui/properties/ComponentPropertiesViewModel.cs`
- Create: focused controls under `engine/helengine.editor/components/ui/properties/component`
- Create: asset, entity, scene, and project controls under `engine/helengine.editor/components/ui/properties/surfaces`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanelUpdater.cs`
- Create: focused tests under `engine/helengine.editor.tests/components/ui/properties`

**Interfaces:**
- Consumes: selection snapshots, inspector descriptors, typed edit commands, and history coordination.
- Produces: routed property surfaces and typed edits with correct undo/dirty behavior.

- [ ] **Step 1: Characterize visible responsibilities**

Cover transform, common metadata, reflected fields, asset references, collections, platform overrides, dynamic inspectors, selection changes, scroll preservation, generated assets, scene persistence, undo/redo, and disposal. Assert controls submit typed commands instead of mutating and separately marking dirty.

- [ ] **Step 2: Run focused tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ComponentPropertiesView|FullyQualifiedName~PropertiesPanel" -v:minimal
```

- [ ] **Step 3: Extract component editors, surfaces, and presentation state**

Make `ComponentPropertiesView` a shell over focused controls sharing a view model. Make `PropertiesPanel` responsible only for selection-to-surface routing and layout. Child controls receive narrow presentation interfaces and an edit-command sink. Delete moved methods and direct infrastructure access.

- [ ] **Step 4: Verify ceilings and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~ComponentPropertiesView|FullyQualifiedName~PropertiesPanel|FullyQualifiedName~EditorSessionUndoRedoIntegrationTests" -v:minimal
rtk git add -- engine/helengine.editor/components/ui engine/helengine.editor.tests
rtk git commit -m "Split editor property views"
```

Required result: each shell is at most 1,200 physical lines, is not partial, and does not depend on `EditorSession` or concrete project infrastructure.

### Task 6: Split Build Dialog, Viewport, and Title Bar

**Files:**
- Create: `engine/helengine.editor/components/ui/build/BuildDialogViewModel.cs`
- Create: focused build selection, queue, validation, and log presentation components
- Create: focused viewport input, overlay, render-coordination, and toolbar components
- Create: focused title-bar project, add, tools, and build command presenters
- Modify: `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify: `engine/helengine.editor/components/ui/EditorViewport.cs`
- Modify: `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify: corresponding tests under `engine/helengine.editor.tests`

**Interfaces:**
- Consumes: build coordinator state/commands, viewport presentation interfaces, and tool-command descriptors.
- Produces: thin views that construct widgets, bind state, forward commands, and dispose bindings.

- [ ] **Step 1: Characterize the three views**

For build cover selection, validation, queue construction, copy settings, logs, enabled state, cancellation, and disposal. For viewport/title bar cover input capture, focus, picking, camera controls, overlays, grid/settings toggles, and add/project/tools/build commands.

- [ ] **Step 2: Run focused tests**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BuildDialog|FullyQualifiedName~EditorViewport|FullyQualifiedName~EditorTitleBar" -v:minimal
```

- [ ] **Step 3: Extract and route presentation components**

Move build validation, queue construction, state transitions, and enablement to the view model/controllers. Separate viewport input, overlays, rendering coordination, and toolbar commands. Separate title-bar menu presenters by command group. Views retain widget construction, binding, forwarding, and owned disposal only.

- [ ] **Step 4: Run regressions and commit**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~BuildDialog|FullyQualifiedName~EditorViewport|FullyQualifiedName~EditorTitleBar|FullyQualifiedName~EditorSessionKeyboardFocusIntegrationTests|FullyQualifiedName~EditorSessionInputCaptureLifecycleSourceTests" -v:minimal
rtk git add -- engine/helengine.editor/components engine/helengine.editor/managers engine/helengine.editor.tests
rtk git commit -m "Split editor build and viewport views"
```

Required result: `BuildDialog.cs` is at most 1,200 physical lines and is not partial; extracted controls never accept `EditorSession`.

### Task 7: Dependency Guards and End-to-End Verification

**Files:**
- Create: `engine/helengine.editor.tests/EditorModularizationSourceContractTests.cs`
- Modify: `engine/helengine.editor/EditorSession.cs` only for final dead-code removal

**Interfaces:**
- Consumes: production source graph and completed editor composition.
- Produces: permanent architecture guards and demodisc behavior proof.

- [ ] **Step 1: Add source-contract tests**

Assert all four named shells are at most 1,200 physical lines and are not partial; `AssetImportManager` is absent; focused constructors do not accept `EditorSession`; extracted services reference neither static mutable editor event hubs nor global project-path providers; and runtime/core projects do not reference editor assemblies. Report exact files and lines, and do not broaden exclusions to make failures pass.

- [ ] **Step 2: Run guards and remove violations**

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore --filter "FullyQualifiedName~EditorModularizationSourceContractTests" -v:minimal
```

- [ ] **Step 3: Run full verification**

```powershell
rtk dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.core.tests\helengine.core.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.baseplatform.tests\helengine.baseplatform.tests.csproj --no-restore -v:minimal
rtk dotnet test engine\helengine.platforms.tests\helengine.platforms.tests.csproj --no-restore -v:minimal
```

- [ ] **Step 4: Run demodisc smoke workflow**

Use the public editor CLI to generate/open/save the project and import a model, then run:

```powershell
rtk dotnet test C:\dev\helprojs\demodisc\city.sln --no-restore -v:minimal
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\build-platform.ps1 -Project C:\dev\helprojs\demodisc\project.heproj -Platform windows -Output C:\dev\helworks\builds\demodisc-modularization\windows -Configuration Debug
rtk git diff --check
```

Verify reopen and shutdown leave no locked project, cache, or platform-publication files.

- [ ] **Step 5: Commit final guards**

```powershell
rtk git add -- engine/helengine.editor engine/helengine.editor.tests
rtk git commit -m "Enforce modular editor architecture"
```

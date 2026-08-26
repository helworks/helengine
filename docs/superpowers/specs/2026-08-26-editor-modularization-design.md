# Editor Modularization Design

## Summary

After persistence, authoring, publishing, and cooking contracts stabilize, Helengine will decompose its largest editor classes into focused services and view components. The work is behavior-preserving: it changes ownership and dependencies, not user-visible features or persisted formats.

Composition replaces process-global reach-through and oversized orchestration classes. The public editor host creates project-scoped services, `EditorSession` coordinates them, and UI views depend on narrow models and controllers.

## Goals

- Make `EditorSession` a composition and coordination boundary rather than the implementation of every editor feature.
- Split `AssetImportManager` into registry, settings, execution, and runtime-resolution responsibilities.
- Split large property and build views into focused controls backed by testable presentation models.
- Remove mutable global project-path and event coordination from extracted paths.
- Give each service an explicit constructor contract and lifetime.
- Preserve current behavior and public APIs established by earlier workstreams.
- Reduce the context required to understand, test, and change one editor subsystem.

## Non-Goals

- Redesigning UI appearance.
- Changing authored or packaged formats.
- Adding new asset, build, or scene features.
- Keeping obsolete facades after callers migrate.
- Splitting files into partial classes without changing responsibility boundaries.
- Introducing a general dependency-injection framework.

## Preconditions

This work starts only after:

- current-format-only readers and writers are complete;
- `EditorProjectAuthoringSession` is the canonical project authoring boundary;
- local engine/platform publication is stable; and
- the unified asset cook graph owns asset cooking.

Those contracts are inputs to modularization and are not redesigned here.

## Editor Host and Lifetimes

`EditorHost` owns the service graph for one opened project. Lifetimes are explicit:

- host lifetime: renderer, input, platform discovery, importer registrations;
- project lifetime: authoring session, project settings, build coordination, workspace state;
- scene lifetime: scene document, selection, history, viewport bindings; and
- operation lifetime: import jobs, generation transactions, and build executions.

Services receive dependencies through constructors. Extracted code does not fetch mutable coordination state through `Core.Instance`, static editor event hubs, or global project paths.

## EditorSession Decomposition

`EditorSession` remains the UI-facing coordinator but delegates to these focused project services:

### `EditorProjectLifecycleCoordinator`

Owns project open/close, project document validation, platform bootstrap, service lifetime creation, and orderly disposal. It does not own scene editing or build execution.

### `EditorSceneWorkspaceCoordinator`

Owns open scenes, active scene changes, scene load/save, workspace restoration, and scene-level dirty-state integration. It delegates authoring and persistence to current services.

### `EditorSelectionCoordinator`

Owns selected entities/assets, selection change notifications, and synchronization between hierarchy, viewport, and property views. It is scene scoped and testable without a full editor host.

### `EditorHistoryCoordinator`

Owns undo/redo transactions, dirty revision tracking, and mutation notifications. All current scene mutation paths become history-backed or explicitly non-undoable; no fallback “untracked legacy mutation” path remains.

### `EditorBuildCoordinator`

Owns build queue requests, selected platform/profile validation, build execution state, logs, cancellation, and result presentation. It delegates cooking to the cook graph and platform discovery to the current platform service.

### `EditorToolCommandCoordinator`

Owns project-contributed menu command discovery and execution scopes. Each command receives the current public authoring session instead of global paths or a full `EditorSession` reference.

`EditorSession` wires UI events to these coordinators and exposes only the small surface required by `MainForm`. Feature logic does not move into `MainForm`.

## Asset Import Decomposition

The current `AssetImportManager` responsibilities become:

### `AssetImporterRegistry`

Stores current importer registrations and selects a compatible importer by asset kind and extension. Registration is immutable after project bootstrap.

### `AssetImportSettingsRepository`

Loads, validates, creates, and saves current typed import settings. It owns settings paths and serializers but performs no import processing.

### `AssetImportExecutionService`

Computes current import keys, runs the selected importer and processor, publishes imported cache artifacts, and returns typed results. It owns operation cancellation and diagnostics.

### `ImportedAssetRuntimeResolver`

Loads imported current runtime models, textures, audio, and other supported runtime values from validated import results. It is the implementation behind public authoring-session runtime-loading methods.

### `AssetImportInvalidationService`

Observes source and current settings changes and schedules affected imports. It does not own UI state.

After callers migrate, `AssetImportManager` is deleted rather than retained as a facade.

## UI Decomposition

Large views are split by visible responsibility and presentation state.

### Component properties

`ComponentPropertiesView` becomes a shell containing focused editors for transforms, common component metadata, reflected fields, asset references, collections, and platform overrides. Each editor consumes a presentation model and emits typed edit commands.

### Properties panel

`PropertiesPanel` owns selection-to-editor routing and layout only. Asset, entity, scene, and project property surfaces are separate controls.

### Build dialog

`BuildDialog` becomes a view over `BuildDialogViewModel`. Platform/profile selection, queue-item construction, validation, log state, and button enablement move into focused models/controllers. The view retains widget construction and event forwarding.

### Editor viewport and title bar

Viewport input, overlays, scene rendering coordination, and toolbar/title commands are separate components. Controls do not reach into unrelated editor subsystems through the whole session object.

## Dependency Rules

- UI may depend on editor application services and neutral engine primitives.
- Editor services may depend on core, files, platforms, and base-platform contracts.
- Runtime/core assemblies never depend on editor assemblies.
- Project tools depend only on declared public editor APIs.
- Coordinators depend on focused services, not on views.
- Views depend on presentation interfaces, not concrete project infrastructure.
- No extracted service accepts `EditorSession` as a dependency.

A source-contract test enforces forbidden project-reference and namespace edges where project structure permits.

## Extraction Method

Each extraction follows the same behavior-preserving sequence:

1. add characterization tests around the current behavior;
2. introduce the focused interface and implementation;
3. move behavior without changing its inputs or outputs;
4. inject the service through the existing composition root;
5. switch all callers;
6. delete the old fields, methods, static event path, or manager; and
7. run focused and full editor tests.

Temporary forwarding methods may exist within one task while callers move, but are deleted before that task's commit. The repository never lands a permanent parallel path.

## State and Events

Project- and scene-scoped state uses owned observable services. Events have one clear owner and unsubscribe during scope disposal. Cross-service commands use direct interfaces or typed event contracts; they do not use process-global static delegates.

State transitions that affect persistence or dirty state are explicit command results. UI controls do not mutate engine objects and separately guess that a scene changed.

## Testing Strategy

### Characterization tests

Before each extraction, capture current behavior for project opening, scene switching, selection, undo/redo, importing, build queuing, property editing, viewport commands, and disposal.

### Unit tests

Each focused service is constructed with fakes for its direct dependencies. Tests cover success, validation, failure propagation, cancellation, state transitions, event ordering, and disposal.

### Composition tests

- `EditorHost` creates one service instance per declared lifetime;
- opening a second host shares no mutable project or scene state;
- project close disposes operations before project services;
- views receive the expected presentation interfaces; and
- no focused service receives the whole `EditorSession`.

### Source-contract tests

- `EditorSession` contains at most 1,200 physical lines after extraction;
- `ComponentPropertiesView`, `PropertiesPanel`, and `BuildDialog` each contain at most 1,200 physical lines;
- `AssetImportManager` is absent;
- no production partial-class split substitutes for decomposition;
- no extracted service uses global editor project paths or static mutable event hubs; and
- runtime assemblies have no editor dependency.

### End-to-end tests

- launch the editor and open demodisc;
- generate and save scenes through the authoring session;
- edit properties with undo/redo and correct dirty state;
- import a model through the public authoring API;
- queue and complete a Windows build through the cook graph; and
- close and reopen without leaked locks, watchers, or event handlers.

## Delivery Slices

1. establish host lifetime and project lifecycle coordinator;
2. extract scene workspace, selection, and history;
3. extract tool-command and build coordination;
4. decompose asset importing and delete `AssetImportManager`;
5. split component and general properties views;
6. split build dialog, viewport, and title-bar coordination; and
7. enforce dependency and size guards.

Each slice is committed independently and leaves tests green. Large mechanical moves are separated from behavior fixes so review can distinguish them.

## Success Criteria

- `EditorSession` coordinates focused services and no longer implements subsystem internals.
- `AssetImportManager` is replaced by focused current-format services.
- The largest UI classes are shells over focused controls and presentation models.
- Project and scene state have explicit lifetimes and disposal.
- Static mutable coordination and global project paths are absent from extracted behavior.
- Existing editor behavior, deterministic authoring, platform publishing, and cooking tests remain unchanged and passing.

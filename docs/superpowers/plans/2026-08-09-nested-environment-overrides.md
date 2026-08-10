# Nested Environment Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add project-defined build environments and optional environment overrides nested under each platform override, with `debug` and `release` protected by default. A selected platform resolves base content, its platform override, then its selected environment override; every existing cookable payload follows that rule.

**Architecture:** Store the environment registry in project settings and use a shared `EditorOverrideScope` (`platformId`, optional `environmentId`) throughout authoring, persistence, and cooking. Keep existing platform payloads as the compatibility layer and attach environment payloads only beneath their owning platform. Propagate the selected environment through local build settings, queued builds, CLI invocation, cook/build requests, and compile symbols. Editor surfaces retain their platform tabs, add an explicit `+` affordance to opt a platform into environment overrides, and then expose only that platform's nested environments.

**Tech Stack:** C#/.NET 9, Helen engine editor UI, System.Text.Json project settings, engine binary asset serializers, NUnit editor tests, Windows build runner.

---

## Working rules

- Work directly on `main`, as requested. Preserve the existing unrelated modification in `engine/helengine.editor.tests/ModelTessellationProcessorTests.cs` and never stage it.
- Keep legacy platform-only project files and scene/assets valid. Missing environment metadata means `release` for build selection and no environment payload for authored data.
- Treat environment IDs as case-insensitive, normalized non-empty identifiers. `debug` and `release` are canonical and protected from rename/delete.
- Do not add runtime environment metadata to cooked payloads. Environment selection is an editor/cooker concern; cooked files contain only the resolved values.

## 1. Establish environment registry and validation

**Files:**
- Create `engine/helengine.editor/managers/project/EditorProjectEnvironmentDefinition.cs`
- Create `engine/helengine.editor/managers/project/EditorProjectEnvironmentsDocument.cs`
- Create `engine/helengine.editor/managers/project/EditorProjectEnvironmentsService.cs`
- Create `engine/helengine.editor.tests/managers/project/EditorProjectEnvironmentsServiceTests.cs`

- [ ] Write failing service tests first for a missing `settings/environments.json`, malformed JSON, duplicate/case-variant IDs, blank IDs, and persistence round trips.
- [ ] Test that `Load()` always returns `debug` and `release` in stable order, marks both protected, preserves custom-environment order, and repairs legacy/malformed data on the next save.
- [ ] Test add, rename, and delete validation: protected IDs cannot be renamed/deleted; a custom name cannot collide with protected or existing names; IDs are normalized deterministically.
- [ ] Add the lightweight definition/document types and a JSON service patterned after `EditorProjectPlatformsService`. Keep the settings file strictly project-shared and write it as `settings/environments.json`.
- [ ] Expose `CreateDefaultDocument`, normalization, and protected-ID helpers only as needed by the editor and command-line validation paths.
- [ ] Run `dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter FullyQualifiedName~EditorProjectEnvironmentsServiceTests` and confirm the new tests pass.
- [ ] Commit only the environment-registry implementation and its tests.

## 2. Add an explicit Tool → Environments workflow

**Files:**
- Modify `engine/helengine.editor/components/ui/EditorTitleBar.cs`
- Modify `engine/helengine.editor/EditorSession.cs`
- Create `engine/helengine.editor/components/ui/EnvironmentsDialog.cs`
- Create `engine/helengine.editor/components/ui/EnvironmentsDialogRow.cs`
- Create `engine/helengine.editor/model/EnvironmentsDialogSelection.cs`
- Create `engine/helengine.editor.tests/EnvironmentsDialogTests.cs`
- Modify `engine/helengine.editor.tests/EditorSessionPreferencesTests.cs` or create `engine/helengine.editor.tests/EditorSessionEnvironmentsTests.cs`

- [ ] Write dialog tests before UI code: the default rows render as protected; add creates a custom draft row; protected rows do not expose rename/delete; custom rename/delete state is captured; duplicate/invalid IDs show validation feedback; cancel returns no selection.
- [ ] Add a `Tools` top-level menu to `EditorTitleBar` and put `Environments...` in it, rather than repurposing the existing Build menu. Add an `EnvironmentsRequested` event and focused keyboard/menu lifecycle coverage.
- [ ] Build a modal using the same lifecycle, focus handling, row pooling, footer controls, and UI metrics approach as `PlatformsDialog`. Include Add, Rename, Delete, explicit confirmation for destructive custom-environment removal, Save, and Cancel.
- [ ] In `EditorSession`, own the dialog/service, create and dispose them with the other modal workflows, block global shortcuts while open, subscribe/unsubscribe events, and reload/normalize the registry after confirmation.
- [ ] Add source/integration tests proving the title-bar action reaches the session and a saved selection writes the shared project settings file.
- [ ] Run the focused dialog/session tests, then commit only these UI/session changes and tests.

## 3. Introduce the nested override scope model and preserve scene compatibility

**Files:**
- Create `engine/helengine.editor/model/EditorOverrideScope.cs`
- Modify `engine/helengine.core/assets/raw/scene/SceneEntityPlatformExistenceOverrideAsset.cs`
- Modify `engine/helengine.core/assets/raw/scene/SceneEntityPlatformTransformOverrideAsset.cs`
- Modify `engine/helengine.core/assets/raw/scene/SceneEntityPlatformComponentOverrideAsset.cs`
- Modify `engine/helengine.editor/components/persistence/EntitySaveComponent.cs`
- Modify `engine/helengine.editor/components/persistence/EntityComponentSaveState.cs`
- Modify `engine/helengine.editor/components/persistence/EntityComponentPlatformOverrideState.cs`
- Modify `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify `engine/helengine.editor.tests/EntitySaveComponentTests.cs`
- Modify `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify `engine/helengine.editor.tests/serialization/scene/ComponentPlatformOverridePayloadServiceTests.cs`

- [ ] First add failing tests for resolving a base entity/component payload, then platform payload, then an environment payload owned by that same platform. Include the case where a different platform's environment payload must not apply.
- [ ] Add `EnvironmentId` (empty for legacy platform-only records) to serialized scene override assets and `EditorOverrideScope` validation that forbids an environment without a platform.
- [ ] Replace flat editor dictionaries with platform-keyed containers that expose both legacy platform APIs and explicit scope APIs. Environment entries must be stored below their platform container, not in a project-global environment map.
- [ ] Update scene save/load and component payload serialization to round-trip `EnvironmentId`, normalize empty environment IDs as platform-only records, and reject duplicate `(platformId, environmentId)` records.
- [ ] Add a pure resolver helper used by cooking: apply common/base state, then the platform-only state, then the matching `(platform, environment)` state. Make absence a no-op so partial environment payloads inherit all unspecified values.
- [ ] Verify old scene fixtures still deserialize without migration and new scoped fixtures serialize deterministically.
- [ ] Run the focused entity and scene serialization tests, then commit the scope model, serializers, and tests.

## 4. Make scene/entity authoring select platform first, then opt into an environment

**Files:**
- Create `engine/helengine.editor/components/ui/OverrideScopeTabStripView.cs` (or extend `PlatformTabStripView` only if its public API remains coherent)
- Modify `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Modify `engine/helengine.editor/managers/scene/ComponentPlatformEditingService.cs`
- Modify `engine/helengine.editor/managers/scene/EntityPlatformExistenceEditingService.cs`
- Modify `engine/helengine.editor/managers/scene/EntityPlatformTransformEditingService.cs`
- Modify `engine/helengine.editor/tests/PropertiesPanelMutationTests.cs`
- Modify `engine/helengine.editor.tests/PlatformSceneAuthoringHelperServiceTests.cs`

- [ ] Add failing UI/service tests showing platform-only authoring is unchanged by default and no environment selector appears until the user presses the `+` at the right side of the selected platform strip.
- [ ] When `+` is used, present the project registry environments for that selected platform and create/select an environment payload only after the user chooses one. The active scope label must identify both platform and environment.
- [ ] Make existence, transform, component add/remove, component property values, and component asset references read/write through `EditorOverrideScope` and retain the current base → platform → environment projection behavior while editing.
- [ ] Preserve the common-transform snapshot behavior when switching scopes, and ensure deleting an environment override restores projection to the platform layer rather than base directly.
- [ ] Add mutation/history tests for environment-scoped entity changes and verify undo/redo does not mutate sibling environments or the base/platform records.
- [ ] Run the focused authoring/property tests and commit the scene authoring slice.

## 5. Apply the same scope to imported assets, materials, and animation

**Files:**
- Modify `engine/helengine.editor/managers/asset/AssetProcessorSettings.cs`
- Modify `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify `engine/helengine.editor/managers/asset/MaterialAssetPlatformOverrideDocument.cs`
- Modify `engine/helengine.editor/serialization/MaterialAssetPlatformOverrideDocumentBinarySerializer.cs`
- Modify `engine/helengine.editor/components/ui/MaterialAssetPlatformPanel.cs`
- Modify `engine/helengine.core/assets/raw/animation/AnimationClipPlatformOverrideAsset.cs`
- Modify `engine/helengine.editor/components/ui/AnimationClipAssetView.cs`
- Modify `engine/helengine.editor/components/ui/AnimationClipAssetPlatformPanel.cs`
- Modify `engine/helengine.editor/managers/project/AnimationClipPlatformResolutionService.cs`
- Modify `engine/helengine.editor.tests/AnimationClipPlatformOverrideSerializationTests.cs`
- Modify `engine/helengine.editor.tests/AnimationClipPlatformResolutionTests.cs`
- Modify `engine/helengine.editor.tests/managers/project/EditorPlatformAssetCookServiceTests.cs`

- [ ] Start with failing resolution tests for texture/model/audio/font processor settings and material/animation payloads: base values are inherited, platform values replace base values, and environment values replace only their selected platform values.
- [ ] Refactor `AssetProcessorSettings.Platforms` into platform containers with optional environment settings and provide compatibility accessors for platform-only clients. Do not share an environment dictionary across platforms.
- [ ] Add `EnvironmentId` to material and animation override records, preserve legacy binary documents, and make serializers/readers reject duplicates in the same platform/environment scope.
- [ ] Reuse `OverrideScopeTabStripView` in every existing platform-aware asset surface. The `+` is next to the platform strip; environments remain hidden until an override exists/gets requested for that platform.
- [ ] Ensure all asset categories using `AssetProcessorSettings`—textures, models, audio, fonts, and material-related processor data—route through the same scope resolver. Do not introduce a category-specific environment convention.
- [ ] Run serialization, resolution, UI mutation, and asset-cook focused tests, then commit the shared asset slice.

## 6. Persist environment selection in build settings and queues

**Files:**
- Modify `engine/helengine.editor/managers/project/EditorBuildPlatformConfigDocument.cs`
- Modify `engine/helengine.editor/managers/project/EditorBuildQueueItemDocument.cs`
- Modify `engine/helengine.editor/managers/project/EditorBuildConfigService.cs`
- Modify `engine/helengine.editor/managers/project/EditorBuildQueueService.cs`
- Modify `engine/helengine.editor/components/ui/BuildDialog.cs`
- Modify `engine/helengine.editor/components/ui/BuildDialogQueueRow.cs`
- Modify `engine/helengine.editor/EditorSession.cs`
- Modify `engine/helengine.editor.tests/BuildDialogTests.cs`
- Modify `engine/helengine.editor.tests/EditorSessionBuildQueueTests.cs`

- [ ] Write failing tests that old `user_settings/build_config.json` documents load as `release`, a queue item snapshots `SelectedEnvironmentId`, and a queue item remains stable if the active UI selection changes later.
- [ ] Add an environment combo box after platform selection in Build. It loads project environments, defaults to `release`, and keeps the chosen environment for ordinary build profiles.
- [ ] Establish canonical build-profile behavior: choosing the built-in Debug profile selects `debug`; choosing built-in Release selects `release`; custom profiles retain an explicitly chosen environment. Keep the legacy `DebugBuild` flag as a migration input until all callers use `SelectedEnvironmentId`.
- [ ] Include the selected environment in the queue row/status text and copy-settings behavior so users can audit exactly what will cook.
- [ ] Validate saved/queued environment IDs against the registry, repairing stale local state to `release` while leaving shared authored payloads untouched.
- [ ] Run focused build dialog/queue tests and commit the persistence/UI slice.

## 7. Carry environments through CLI, build graph, and code generation

**Files:**
- Modify `engine/helengine.editor/EditorCliArgumentParser.cs`
- Modify `engine/helengine.editor/EditorCliBuildOptions.cs`
- Modify `engine/helengine.editor/EditorCliBuildRunner.cs`
- Modify `engine/helengine.editor/managers/project/EditorPlatformBuildGraphRunner.cs`
- Modify `engine/helengine.editor/managers/project/EditorPlatformAssetCookService.cs`
- Modify `engine/helengine.editor/managers/project/EditorPlatformBuildScenePackager.cs`
- Modify `engine/helengine.editor/managers/project/EditorPlatformCodeCookService.cs`
- Modify `engine/helengine.baseplatform/Requests/PlatformBuildRequest.cs`
- Modify `engine/helengine.editor.tests/EditorCliBuildRunnerTests.cs`
- Modify `engine/helengine.editor.tests/EditorCliBuildRunnerCompilationModeTests.cs`
- Modify `engine/helengine.editor.tests/managers/project/EditorPlatformBuildGraphRunnerTests.cs`

- [ ] Add parser tests for `--environment <id>`, default-to-release behavior, and an explicit error naming the unknown ID and available environments. Ensure `--build-profile debug/release` maps to its canonical environment only when no explicit `--environment` is provided.
- [ ] Extend local config, queued items, cook calls, `PlatformBuildRequest`, and build reports with `SelectedEnvironmentId`; make constructors/source compatibility explicit rather than silently dropping the value.
- [ ] Pass one `EditorOverrideScope` into scene packaging, asset cooking, material cooking, animation resolution, and generated-code cooking. Resolve source data at that point and write only the effective result into cooked assets/manifests.
- [ ] Publish a sanitized compile symbol for arbitrary identifiers (for example `HELEN_ENV_DEBUG`) and retain existing debug/release native compilation behavior. Never generate a symbol from an invalid ID.
- [ ] Add build-graph tests proving the cook receives the selected scope and the manifest/request record it for build diagnostics without embedding it in runtime asset payloads.
- [ ] Run the focused CLI/build graph tests and commit this end-to-end propagation slice.

## 8. Handle deletion, migration, and project-wide validation

**Files:**
- Create `engine/helengine.editor/managers/project/EditorEnvironmentOverrideCleanupService.cs`
- Create `engine/helengine.editor/managers/project/EditorEnvironmentOverrideValidationService.cs`
- Modify `engine/helengine.editor/EditorSession.cs`
- Modify `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify asset settings/override persistence services from steps 3–5
- Create `engine/helengine.editor.tests/managers/project/EditorEnvironmentOverrideCleanupServiceTests.cs`
- Create `engine/helengine.editor.tests/managers/project/EditorEnvironmentOverrideValidationServiceTests.cs`

- [ ] Write failing tests for custom-environment deletion: all matching nested scene and asset overrides are removed, build settings and queue items reset to `release`, and protected environments cannot reach cleanup.
- [ ] Implement a project scan that finds nested environment records in scenes and asset settings, reports stale environment IDs, and performs cleanup only after the dialog confirmation path has succeeded.
- [ ] Make registry load, scene load, and asset load tolerate unknown legacy environment IDs for diagnostics; block selecting an invalid explicit environment for a build.
- [ ] Validate that no environment entry can be applied without its owner platform, and that cleanup cannot remove or rewrite platform-only data.
- [ ] Run cleanup/validation tests with temp projects, then commit this migration and safety slice.

## 9. Prove the approved debug-level-label consumer in DemoDisc

**Files:**
- Modify `C:/dev/helprojs/demodisc/assets/codebase/game.tools/GameSceneFactory.cs`
- Modify/add DemoDisc source tests if that repository has an existing compatible test project
- Modify `C:/dev/helprojs/demodisc/.helenui/profiles/helenui.json` only if a new UI target becomes necessary

- [ ] Add a compile-symbol-gated, top-right text label that shows the selected DemoDisc level name only for the `debug` environment, excluding DS and 3DS code paths. Release/custom builds must not create the label.
- [ ] Build DemoDisc for Windows with the `debug` environment using the engine’s normal build path; verify the generated source receives the debug environment symbol.
- [ ] Use only HelenUI (no screenshots) to navigate every DemoDisc section, including the console level selector, and confirm the debug label identifies the live level after selection.
- [ ] Run the smallest relevant engine and DemoDisc test/build commands, record exact output, and commit engine and DemoDisc changes separately.

## 10. Final verification and handoff

- [ ] Run focused test groups from each step, then the full `engine/helengine.editor.tests` suite after dependencies are restored. If it exceeds the command time limit, run it through the project’s supported background/test runner and report the exact outcome rather than assuming success.
- [ ] Build the Windows editor/project path and execute one release and one debug DemoDisc build; inspect cooked output with HelenUI only for interaction validation.
- [ ] Review `git diff --check`, `git status --short`, and staged file lists before each commit to ensure the pre-existing `ModelTessellationProcessorTests.cs` edit is never included.
- [ ] Document migration behavior and user-facing usage: Tool → Environments, platform tab → `+`, environment selection, and CLI `--environment`.
- [ ] Request a code review after the implementation is complete and address only verified findings.

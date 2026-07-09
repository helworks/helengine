# Blueprint Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add first-class `Blueprint` assets to Helengine as reusable single-root authored hierarchies that can be edited as source assets, embedded into scenes through instance roots, shown as inherited read-only subtrees in the editor, and expanded into ordinary packaged scene data for every target platform.

**Architecture:** Reuse the existing scene subtree payload model instead of inventing a second graph format. A new raw `BlueprintAsset` stores one `SceneEntityAsset` root plus scene-style asset references. The editor gains a blueprint document load/save path parallel to scene documents, an editor-only `BlueprintInstanceComponent` for scene embedding, marker components for inherited read-only nodes, and one expansion service used by both scene authoring and packaging. Packaged scenes remain ordinary expanded scene entity trees, so runtime scene loading stays on `RuntimeSceneLoadService` and no host/runtime repo changes are required in v1.

**Tech Stack:** C#, .NET 9, xUnit, `helengine.core`, `helengine.editor`, `helengine.editor.tests`

---

## File Map

### Core raw asset files

- Create: `C:\dev\helworks\helengine\engine\helengine.core\assets\raw\blueprint\BlueprintAsset.cs`
  Defines the one-root blueprint asset container and the dedicated blueprint file extension constant.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\assets\EditorAssetBinaryValueKind.cs`
  Registers the new editor asset value kind for blueprint payloads.
- Modify: `C:\dev\helworks\helengine\engine\helengine.core\assets\EditorAssetBinarySerializer.cs`
  Adds blueprint binary read/write support by reusing the existing scene-entity and asset-reference payload readers/writers.

### Editor asset registration and document files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\LoadedEditorBlueprintDocument.cs`
  Represents one loaded blueprint source document with exactly one editable root entity.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintLoadService.cs`
  Rebuilds editor entities from a `BlueprintAsset` payload using the existing component persistence registry and reference resolver.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintFileLoadService.cs`
  Loads one blueprint asset file from disk and materializes a source document.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintSaveService.cs`
  Saves one editable blueprint root entity back into a `BlueprintAsset`.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintSavePathResolver.cs`
  Normalizes save paths beneath the project assets root using the blueprint extension.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\blueprint\BlueprintValidationService.cs`
  Enforces one-root and no-nested-blueprint invariants before save/build.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\content\EditorContentProcessorIds.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\content\EditorContentManagerConfiguration.cs`
  Registers blueprint asset loading beside scenes and other editor-authored asset types.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorFileTemplateKind.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorFileTemplateRegistry.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorFileTemplateService.cs`
  Adds creation support for new blank blueprint assets.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\AssetEntryKind.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\asset\EditorAssetManager.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\asset\AssetBrowserView.cs`
  Gives blueprint files their own asset-browser classification, icon, and generated-filter behavior.

### Editor scene embedding and UX files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor\components\authoring\BlueprintInstanceComponent.cs`
  Stores the blueprint asset path on a scene-owned instance root and acts as the authored embedding marker.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\components\authoring\BlueprintInheritedEntityComponent.cs`
  Marks inherited editor entities as blueprint-owned/read-only and preserves source authored ids for future overrides.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\components\authoring\BlueprintInheritedComponentMarker.cs`
  Marks inherited live components as read-only in the properties UX without mutating the source component type.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\BlueprintEditorExpansionService.cs`
  Materializes inherited child entities below a blueprint instance root and refreshes them when the source blueprint changes.
- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\scene\BlueprintSceneSaveFilterService.cs`
  Helps scene serialization ignore inherited children and persist only the instance root.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneLoadService.cs`
  Detects `BlueprintInstanceComponent` during scene load and delegates inherited subtree expansion.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneSaveService.cs`
  Persists only blueprint instance roots and rejects accidental inherited-child flattening.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\SceneHierarchyPanel.cs`
  Renders inherited blueprint rows distinctly and blocks reparent on inherited nodes.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\PropertiesPanel.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\components\ui\ComponentPropertiesView.cs`
  Allows instance-root transform/path editing while disabling inherited child/component mutation.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\EditorSession.cs`
  Adds blueprint document open/save state, asset-browser activation, and refresh integration beside scene documents.

### Packaging files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\BlueprintPackagedSceneExpansionService.cs`
  Resolves referenced blueprint assets, applies platform overrides, expands them into ordinary scene entity trees, and strips authoring-only blueprint markers.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\EditorWindowsBuildScenePackager.cs`
  Invokes blueprint expansion before final packaged scene serialization.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`
  Reuses existing component/reference rewriting on expanded blueprint content.

### Test files

- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\blueprint\BlueprintAssetBinarySerializerTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\blueprint\BlueprintFileLoadServiceTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\blueprint\BlueprintSaveServiceTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\asset\BlueprintAssetBrowserIntegrationTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BlueprintSceneEmbeddingTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\BlueprintPropertiesPanelTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\SceneHierarchyBlueprintTests.cs`
- Create: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\BlueprintPackagedSceneExpansionTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneSaveServiceTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneFileLoadServiceTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorSessionSceneOpenTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\EditorSessionSceneSaveTests.cs`

---

## Task 1: Add the raw blueprint asset type and failing serializer coverage

**Files:**
- Create: `engine/helengine.core/assets/raw/blueprint/BlueprintAsset.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinaryValueKind.cs`
- Modify: `engine/helengine.core/assets/EditorAssetBinarySerializer.cs`
- Create: `engine/helengine.editor.tests/serialization/blueprint/BlueprintAssetBinarySerializerTests.cs`

- [ ] Write failing tests that prove:
  - one-root blueprint payloads round-trip through `AssetSerializer`
  - blueprint asset references round-trip
  - platform existence, transform, and component override payloads round-trip
  - invalid zero-root and multi-root blueprint payloads fail clearly
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintAssetBinarySerializerTests" -v minimal`
  Expected: FAIL because `BlueprintAsset` and serializer support do not exist yet.
- [ ] Add `BlueprintAsset` as one `Asset` subclass with:
  - one dedicated file-extension constant
  - one `SceneEntityAsset RootEntity`
  - one `SceneAssetReference[] AssetReferences`
  - one `ReleaseOwnedValuesForNativeDelete`-compatible ownership model matching existing raw assets
- [ ] Extend `EditorAssetBinaryValueKind` and `EditorAssetBinarySerializer` so blueprint payloads reuse the existing scene subtree readers and writers instead of duplicating component serialization logic.
- [ ] Re-run the focused serializer tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.core/assets/raw/blueprint engine/helengine.core/assets/EditorAssetBinaryValueKind.cs engine/helengine.core/assets/EditorAssetBinarySerializer.cs engine/helengine.editor.tests/serialization/blueprint/BlueprintAssetBinarySerializerTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: add blueprint asset serialization"`

## Task 2: Register blueprint files as first-class editor assets

**Files:**
- Modify: `engine/helengine.editor/content/EditorContentProcessorIds.cs`
- Modify: `engine/helengine.editor/content/EditorContentManagerConfiguration.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorFileTemplateKind.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorFileTemplateRegistry.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorFileTemplateService.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetEntryKind.cs`
- Modify: `engine/helengine.editor/managers/asset/EditorAssetManager.cs`
- Modify: `engine/helengine.editor/components/ui/asset/AssetBrowserView.cs`
- Create: `engine/helengine.editor.tests/managers/asset/BlueprintAssetBrowserIntegrationTests.cs`

- [ ] Write failing asset-browser and file-template tests that prove:
  - the editor content manager can deserialize blueprint assets by extension
  - the asset browser classifies blueprint files distinctly from scenes and materials
  - creating a new blueprint file emits a valid blank blueprint asset under `assets/`
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintAssetBrowserIntegrationTests" -v minimal`
  Expected: FAIL because blueprint registration and template creation do not exist yet.
- [ ] Add one new editor processor id for `BlueprintAsset`.
- [ ] Register the blueprint processor and extension beside scene asset registration in `EditorContentManagerConfiguration`.
- [ ] Extend the file-template enum/registry/service so the editor can create blank blueprint files without special-case manual serialization code in the UI layer.
- [ ] Extend `AssetEntryKind`, `EditorAssetManager`, and `AssetBrowserView` so blueprint files have their own icon/category and are visible in filters where appropriate.
- [ ] Re-run the focused asset-browser tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/content engine/helengine.editor/managers/asset engine/helengine.editor/components/ui/asset/AssetBrowserView.cs engine/helengine.editor.tests/managers/asset/BlueprintAssetBrowserIntegrationTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: register blueprint assets in editor"`

## Task 3: Add blueprint document load/save services

**Files:**
- Create: `engine/helengine.editor/serialization/blueprint/LoadedEditorBlueprintDocument.cs`
- Create: `engine/helengine.editor/serialization/blueprint/BlueprintLoadService.cs`
- Create: `engine/helengine.editor/serialization/blueprint/BlueprintFileLoadService.cs`
- Create: `engine/helengine.editor/serialization/blueprint/BlueprintSaveService.cs`
- Create: `engine/helengine.editor/serialization/blueprint/BlueprintSavePathResolver.cs`
- Create: `engine/helengine.editor/serialization/blueprint/BlueprintValidationService.cs`
- Create: `engine/helengine.editor.tests/serialization/blueprint/BlueprintFileLoadServiceTests.cs`
- Create: `engine/helengine.editor.tests/serialization/blueprint/BlueprintSaveServiceTests.cs`

- [ ] Write failing tests that prove:
  - blueprint files load into exactly one editable root entity
  - blueprint saves preserve stable entity ids, component keys, asset references, and platform overrides
  - nested blueprint instances inside blueprint sources are rejected in v1
  - save rejects zero-root and multi-root authoring states
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintFileLoadServiceTests|FullyQualifiedName~BlueprintSaveServiceTests" -v minimal`
  Expected: FAIL because the blueprint authoring document path does not exist yet.
- [ ] Build `BlueprintLoadService` and `BlueprintSaveService` by adapting the current scene load/save logic around one root entity instead of many roots plus scene settings.
- [ ] Keep `ComponentPersistenceRegistry`, `ComponentPlatformOverridePayloadService`, and `SceneAssetReferenceInferenceService` as the shared persistence backbone.
- [ ] Add validation that blueprint sources may not contain `BlueprintInstanceComponent` in v1.
- [ ] Re-run the focused blueprint load/save tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/serialization/blueprint engine/helengine.editor.tests/serialization/blueprint`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: add blueprint document load save services"`

## Task 4: Add scene embedding through blueprint instance roots

**Files:**
- Create: `engine/helengine.editor/components/authoring/BlueprintInstanceComponent.cs`
- Create: `engine/helengine.editor/components/authoring/BlueprintInheritedEntityComponent.cs`
- Create: `engine/helengine.editor/components/authoring/BlueprintInheritedComponentMarker.cs`
- Create: `engine/helengine.editor/managers/scene/BlueprintEditorExpansionService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneLoadService.cs`
- Modify: `engine/helengine.editor/serialization/scene/SceneSaveService.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs`
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- Create: `engine/helengine.editor.tests/BlueprintSceneEmbeddingTests.cs`

- [ ] Write failing tests that prove:
  - scene load expands a blueprint instance into inherited editor children
  - inherited children preserve blueprint-authored ids for future override targeting
  - scene save writes only the instance root and never duplicates inherited children into `SceneEntityAsset.Children`
  - missing blueprint files and cyclic/nested blueprint references fail loudly
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintSceneEmbeddingTests|FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests" -v minimal`
  Expected: FAIL because blueprint instances are not recognized by scene load/save.
- [ ] Add `BlueprintInstanceComponent` as the authored scene marker storing the blueprint-relative path.
- [ ] Add inherited-node markers that distinguish:
  - the scene-owned instance root
  - inherited blueprint entities
  - inherited live components
- [ ] Implement `BlueprintEditorExpansionService` so it:
  - resolves the source blueprint asset
  - clones its subtree into editor entities under the instance root
  - marks every inherited node/component read-only
  - blocks nested blueprint expansion in v1
- [ ] Update `SceneLoadService` to expand blueprint instances after the instance root components are restored.
- [ ] Update `SceneSaveService` to persist only the instance root and blueprint reference, rejecting accidental inherited-child flattening.
- [ ] Re-run the focused embedding tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/components/authoring engine/helengine.editor/managers/scene/BlueprintEditorExpansionService.cs engine/helengine.editor/serialization/scene engine/helengine.editor.tests/BlueprintSceneEmbeddingTests.cs engine/helengine.editor.tests/serialization/scene/SceneSaveServiceTests.cs engine/helengine.editor.tests/serialization/scene/SceneFileLoadServiceTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: embed blueprint instances into scenes"`

## Task 5: Lock inherited blueprint content in hierarchy and properties UX

**Files:**
- Modify: `engine/helengine.editor/components/ui/SceneHierarchyPanel.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Modify: `engine/helengine.editor/components/ui/ComponentPropertiesView.cs`
- Create: `engine/helengine.editor.tests/SceneHierarchyBlueprintTests.cs`
- Create: `engine/helengine.editor.tests/BlueprintPropertiesPanelTests.cs`

- [ ] Write failing UI tests that prove:
  - inherited blueprint rows are visibly distinguished in the hierarchy
  - reparent is unavailable for inherited nodes
  - inherited child transforms and components cannot be edited
  - the scene-owned instance root remains editable for transform and blueprint-reference changes
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneHierarchyBlueprintTests|FullyQualifiedName~BlueprintPropertiesPanelTests" -v minimal`
  Expected: FAIL because the current hierarchy and properties shell treat all loaded entities as fully editable scene-owned content.
- [ ] Update `SceneHierarchyPanel` row binding so inherited blueprint rows get a distinct label treatment and skip reparent actions.
- [ ] Update `PropertiesPanel` so inherited entities disable name/existence/transform/add-component mutation surfaces while keeping inspection active.
- [ ] Update `ComponentPropertiesView` so inherited components render read-only fields instead of live editable controls wherever mutation would otherwise be possible.
- [ ] Re-run the focused UI tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/components/ui/SceneHierarchyPanel.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor/components/ui/ComponentPropertiesView.cs engine/helengine.editor.tests/SceneHierarchyBlueprintTests.cs engine/helengine.editor.tests/BlueprintPropertiesPanelTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: mark inherited blueprint content read only in editor"`

## Task 6: Teach EditorSession to open and save blueprint source documents

**Files:**
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor/components/ui/asset/OpenFileDialog.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs`
- Modify: `engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`

- [ ] Write failing editor-session tests that prove:
  - activating a blueprint asset opens it as a source document
  - save routes to `BlueprintSaveService` when the current document is a blueprint
  - dirty-state and reload logic work for both scenes and blueprints
  - opening a blueprint does not require scene settings
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests" -v minimal`
  Expected: FAIL because `EditorSession` only tracks scene documents today.
- [ ] Introduce a minimal document-kind split in `EditorSession` rather than forcing blueprints through fake scene state.
- [ ] Wire `BlueprintFileLoadService`, `BlueprintSaveService`, and blueprint asset-browser activation into the existing session lifecycle.
- [ ] Keep the current scene-open dialog scene-only in v1 unless the implementation work shows a generic authored-document dialog is cheaper and cleaner.
- [ ] Re-run the focused editor-session tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/EditorSession.cs engine/helengine.editor/components/ui/asset/OpenFileDialog.cs engine/helengine.editor.tests/EditorSessionSceneOpenTests.cs engine/helengine.editor.tests/EditorSessionSceneSaveTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: support blueprint documents in editor session"`

## Task 7: Expand blueprint instances during packaging for every platform

**Files:**
- Create: `engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs`
- Modify: `engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs`
- Modify: `engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs`
- Create: `engine/helengine.editor.tests/managers/project/BlueprintPackagedSceneExpansionTests.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`

- [ ] Write failing packaging tests that prove:
  - blueprint references resolve during scene packaging
  - platform overrides inside the blueprint asset are applied before expansion
  - the packaged scene contains only ordinary expanded entities and no authoring-only blueprint markers
  - Windows and DS-target packaging both preserve the expected entity/component outcomes because they share the same packager transform path
- [ ] Run: `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~BlueprintPackagedSceneExpansionTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests" -v minimal`
  Expected: FAIL because the build path currently serializes only plain scene-owned entity trees.
- [ ] Implement `BlueprintPackagedSceneExpansionService` so it:
  - scans scene roots for `BlueprintInstanceComponent`
  - loads the referenced `BlueprintAsset`
  - validates no nested blueprint instances are present
  - applies target-platform entity/component overrides from the blueprint source
  - materializes ordinary `SceneEntityAsset` children below the instance root
  - strips editor-only blueprint authoring components before the packaged scene is written
- [ ] Invoke that service from `EditorWindowsBuildScenePackager` before final `AssetSerializer.Serialize` of the cooked scene.
- [ ] Reuse `SceneComponentPackagingTransformService` on the expanded entities instead of building a second asset-rewrite path.
- [ ] Re-run the focused packaging tests and make them pass.
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine add -- engine/helengine.editor/managers/project/BlueprintPackagedSceneExpansionService.cs engine/helengine.editor/managers/project/EditorWindowsBuildScenePackager.cs engine/helengine.editor/managers/project/SceneComponentPackagingTransformService.cs engine/helengine.editor.tests/managers/project/BlueprintPackagedSceneExpansionTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs`
- [ ] Commit: `rtk git -C C:\dev\helworks\helengine commit -m "feat: expand blueprint instances during packaging"`

## Task 8: Run final verification and capture repo state

- [ ] Run the focused blueprint suites:
  - `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Blueprint" -v minimal`
- [ ] Run the scene regression suites touched by blueprint integration:
  - `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~SceneSaveServiceTests|FullyQualifiedName~SceneFileLoadServiceTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~EditorSessionSceneOpenTests|FullyQualifiedName~EditorSessionSceneSaveTests" -v minimal`
- [ ] Run one targeted packaged-scene runtime regression if a blueprint-backed fixture was added:
  - `rtk dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PackagedSceneRuntime" -v minimal`
- [ ] Run: `rtk git -C C:\dev\helworks\helengine status --short`
  Expected: only the intentional blueprint implementation changes remain.

## Spec Coverage Check

- standalone blueprint asset file: covered by Tasks 1 through 3
- exactly one root entity: covered by Tasks 1 and 3 validation
- pure-reference scene instances with root transform overrides only: covered by Tasks 4 and 5
- no nested blueprints in v1: covered by Tasks 3, 4, and 7 validation
- full platform override support inside blueprints: covered by Tasks 1 and 7
- inherited read-only children in editor: covered by Tasks 4 and 5
- source edits propagate on reload and rebuild: covered by Tasks 4, 6, and 7
- build-time expansion with no new runtime loader: covered by Task 7 and the architecture choice above
- future override readiness via stable source ids: covered by Task 4 marker plumbing

## Placeholder Check

- no `TODO`, `TBD`, or deferred follow-up placeholders remain
- every task lists exact engine/editor files and verification commands
- no cross-repo runtime host work is planned because v1 resolves blueprints entirely before packaged runtime load

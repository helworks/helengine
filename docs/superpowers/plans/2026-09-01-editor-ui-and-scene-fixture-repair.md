# Editor UI and Scene Fixture Repair Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement this plan task-by-task.

**Goal:** Update nine stale editor tests to the current constructor, layout, lifecycle, asset-path, and generated-resource contracts without weakening production validation.

**Architecture:** Keep the current production APIs and lifecycle rules. Tests will construct the full interaction/render graph where required, assert disposed state without reading disposed collections, derive scaled title-bar measurements from shared metrics, and use canonical runtime asset paths.

**Tech Stack:** C#/.NET 9, xUnit, editor test interaction graph fixtures

---

### Task 1: Update editor UI fixtures and metrics

**Files:**
- Modify: `engine/helengine.editor.tests/AssetBrowserTabVisibilityTests.cs`
- Modify: `engine/helengine.editor.tests/AssetPickerModalTests.cs`
- Modify: `engine/helengine.editor.tests/BuildSettingsDialogTests.cs`
- Modify: `engine/helengine.editor.tests/SaveFileDialogTests.cs`

- [ ] Construct `AssetBrowserPanel` through its current explicit `Core` and `EditorSessionInteractionServices` seam rather than obsolete reflection arguments.
- [ ] Assert modal/dialog title-bar geometry through `EditorUiMetrics.HostTitleBarHeight`; preserve close-button and backdrop coverage.
- [ ] Assert replaced dynamic platform-row hosts are disposed, then retain manager/render/input absence checks without enumerating disposed component collections.

### Task 2: Remove project-owned catalog expectation and repair initialization fixtures

**Files:**
- Modify: `engine/helengine.editor.tests/EditorComponentAddCatalogTests.cs`
- Modify: `engine/helengine.editor.tests/RenderRegistrationDuplicateRemovalTests.cs`
- Modify: `engine/helengine.editor.tests/ComponentPropertiesViewScenePersistenceTests.cs`

- [ ] Remove the obsolete expectation that engine core exposes City/DemoDisc's `RotateComponent`; do not restore the component to engine.
- [ ] Supply `FakeContentStreamSource` when constructing `ObjectManager` so current initialization validation remains enabled.
- [ ] Build `ComponentPropertiesView` with `TestGeneratedAssetGraph` interaction services and renderer resources before creating model sections.

### Task 3: Align scene-manager tests with deferred validation and canonical paths

**Files:**
- Modify: `engine/helengine.editor.tests/serialization/scene/SceneManagerTests.cs`

- [ ] Assert a missing content stream source fails during `Core.Initialize`, not construction.
- [ ] Lowercase the model/material fixture paths and references required by canonical runtime validation.

### Task 4: Verify and commit

**Files:**
- Modify: none

- [ ] Run the eight affected test classes as one focused filter. Expected: 75/75 passing after deleting the obsolete RotateComponent test.
- [ ] Re-run `CoreContentManagerTests` and `EditorComponentAddCatalogTests` as focused boundary checks.
- [ ] Run `git diff --check` and inspect the exact test-only diff.
- [ ] Commit only the planned test files with message `Update editor UI and scene fixtures`.

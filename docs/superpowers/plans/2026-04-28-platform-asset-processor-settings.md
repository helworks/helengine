# Platform Asset Processor Settings Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Refactor editor asset settings so one `*.hasset` file cleanly separates shared importer settings from per-platform processor settings, persist the editor's active project platform in `settings/project.json`, and add a per-platform model processor boolean named `Flip Winding` that changes processed model output and cache invalidation.

**Architecture:** Keep `*.hasset` as the single asset-settings source of truth, but evolve it into a versioned document with two sections: an importer section shared by all platforms and a processor section keyed by platform id. Add an editor-side project local-settings service to read and write the active platform from `settings/project.json`, drive platform tabs from the current project's `.heproj` `supportedPlatforms`, and keep all UI wiring in the properties panel while asset management and model processing stay in editor services/managers.

**Tech Stack:** C#/.NET 9, existing editor asset serialization, WinForms editor UI, shared `helengine.projectfile` contract library, xUnit, existing `helengine.editor.tests` project

---

## File Map

### Existing files to modify

- `engine/helengine.editor/managers/asset/AssetImportSettings.cs`
  - Expand from three flat fields into importer and processor sections.
- `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
  - Bump the `*.hasset` schema version and read/write the split importer/processor structure.
- `engine/helengine.editor/managers/asset/AssetImportManager.cs`
  - Regenerate unsupported old `*.hasset` files, load/save the new settings model, and make model cache decisions depend on platform processor settings.
- `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
  - Render importer controls, platform tabs, and model processor controls including `Flip Winding`.
- `engine/helengine.editor/components/ui/PropertiesPanel.cs`
  - Pass project-supported platforms and active platform into the asset settings view and forward richer apply events.
- `engine/helengine.editor/EditorSession.cs`
  - Load project-supported platforms from `.heproj`, load/save the active platform from `settings/project.json`, and coordinate apply/reimport flow.
- `engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs`
  - Apply winding reversal when model processor settings request it.
- `engine/helengine.editor.tests/BinarySerializationTests.cs`
  - Add `*.hasset` round-trip coverage for the expanded schema.
- `engine/helengine.editor.tests/AssetImportManagerTests.cs`
  - Cover silent invalidation of unsupported settings versions and settings persistence behavior.
- `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`
  - Cover `Flip Winding`, per-platform processor settings, and cache invalidation.

### New files to create

- `engine/helengine.editor/managers/asset/AssetImporterSettings.cs`
  - Shared source-import settings section inside `*.hasset`.
- `engine/helengine.editor/managers/asset/AssetProcessorSettings.cs`
  - Root processor-settings section keyed by platform id.
- `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
  - One platform's processor settings container.
- `engine/helengine.editor/managers/asset/ModelAssetProcessorSettings.cs`
  - Model-specific processor settings including `FlipWinding`.
- `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
  - Editor-side local project settings model for `settings/project.json`.
- `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
  - Reads and writes the current active platform for the open project.
- `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs`
  - Focused coverage for loading/saving active platform state.
- `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`
  - Headless coverage for platform tabs and processor controls if the current test surface supports it.

## Implementation Notes

- Treat old `*.hasset` versions as unsupported. Do not add a compatibility reader. Regenerate defaults silently and rewrite the file.
- `settings/project.json` remains local state only. It should store the active platform, not canonical project metadata.
- Platform tabs must come from the current project's `.heproj` `supportedPlatforms`.
- `Flip Winding` is a processor setting. It changes processed model output and cache reuse, not renderer behavior.
- Keep MVC boundaries intact:
  - UI classes render tabs, checkboxes, and buttons only.
  - Serialization stays in serializer/service classes.
  - Import/cache logic stays in `AssetImportManager`.
  - Mesh index-order mutation stays in the model conversion path.
- Follow repo conventions strictly:
  - one class per file,
  - XML comments on every class, property, constructor, and method,
  - no local helper functions,
  - no tuples,
  - keep field names PascalCase.

## Task 1: Expand The `*.hasset` Schema

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportSettings.cs`
- Create: `engine/helengine.editor/managers/asset/AssetImporterSettings.cs`
- Create: `engine/helengine.editor/managers/asset/AssetProcessorSettings.cs`
- Create: `engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs`
- Create: `engine/helengine.editor/managers/asset/ModelAssetProcessorSettings.cs`
- Modify: `engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs`
- Modify: `engine/helengine.editor.tests/BinarySerializationTests.cs`

- [ ] **Step 1: Write the failing serializer tests**

Add focused tests in `BinarySerializationTests.cs` for:
- round-tripping importer + per-platform processor settings,
- `FlipWinding` persisting under the `windows` platform,
- unsupported old asset-settings versions throwing from the serializer layer.

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests" -v minimal
```

Expected: FAIL because the expanded settings model and serializer format do not exist yet.

- [ ] **Step 2: Implement the expanded settings model**

Implement the new settings types and refactor `AssetImportSettings` so it owns:
- an `Importer` section,
- a `Processor` section,
- defaults that produce a valid empty processor-settings map.

Implementation requirements:
- `ModelAssetProcessorSettings` must expose `FlipWinding`,
- all new types live one-class-per-file,
- XML comments explain the role of each section clearly.

- [ ] **Step 3: Bump the binary serializer and write the new layout**

Update `AssetImportSettingsBinarySerializer.cs` to:
- bump the current version,
- serialize importer settings separately from processor settings,
- serialize the processor platform map and the nested model processor settings,
- keep unsupported versions as hard failures at the serializer layer.

- [ ] **Step 4: Re-run the serializer tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the schema work**

```bash
rtk git add engine/helengine.editor/managers/asset/AssetImportSettings.cs engine/helengine.editor/managers/asset/AssetImporterSettings.cs engine/helengine.editor/managers/asset/AssetProcessorSettings.cs engine/helengine.editor/managers/asset/AssetPlatformProcessorSettings.cs engine/helengine.editor/managers/asset/ModelAssetProcessorSettings.cs engine/helengine.editor/serialization/AssetImportSettingsBinarySerializer.cs engine/helengine.editor.tests/BinarySerializationTests.cs
rtk git commit -m "Split asset settings into importer and processor sections"
```

## Task 2: Add Editor Project Local Settings For Active Platform

**Files:**
- Create: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs`
- Create: `engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Create: `engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs`

- [ ] **Step 1: Write the failing local-settings tests**

Add tests in `EditorProjectLocalSettingsServiceTests.cs` for:
- loading `ActivePlatform` from `settings/project.json`,
- defaulting to the first supported project platform when the file is missing,
- rewriting the chosen active platform back to disk,
- replacing unsupported or malformed local settings with current defaults.

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected: FAIL because the editor-side service does not exist yet.

- [ ] **Step 2: Implement the editor-side local settings service**

Implement `EditorProjectLocalSettingsDocument` and `EditorProjectLocalSettingsService` with behavior to:
- resolve `settings/project.json` relative to the current project root,
- load and save `ActivePlatform`,
- validate the stored platform against `.heproj` `supportedPlatforms`,
- fall back to the first supported platform and persist that choice when needed.

- [ ] **Step 3: Wire active-platform loading into `EditorSession`**

Update `EditorSession` to:
- read supported platforms from the already-loaded project file,
- create/use the local settings service,
- expose the current active platform for the properties panel flow.

- [ ] **Step 4: Re-run the local-settings tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorProjectLocalSettingsServiceTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the local-settings work**

```bash
rtk git add engine/helengine.editor/managers/project/EditorProjectLocalSettingsDocument.cs engine/helengine.editor/managers/project/EditorProjectLocalSettingsService.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/EditorProjectLocalSettingsServiceTests.cs
rtk git commit -m "Add editor active platform local settings"
```

## Task 3: Refactor The Asset Settings UI For Importer And Processor Sections

**Files:**
- Modify: `engine/helengine.editor/components/ui/AssetImportSettingsView.cs`
- Modify: `engine/helengine.editor/components/ui/PropertiesPanel.cs`
- Create or Modify: `engine/helengine.editor.tests/AssetImportSettingsViewTests.cs`

- [ ] **Step 1: Write the failing UI tests**

Add focused view or panel tests for:
- rendering one tab per supported platform,
- selecting the saved active platform initially,
- showing the model processor `Flip Winding` control in the processor section,
- raising a richer apply event that includes importer id, selected platform, and processor settings.

If the repo’s current UI test harness makes direct view tests impractical, place the coverage at the `PropertiesPanel` level instead and keep the assertions on observable control state/events.

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~PropertiesPanel" -v minimal
```

Expected: FAIL because the view still only supports importer selection.

- [ ] **Step 2: Extend the asset settings view**

Refactor `AssetImportSettingsView` so it can:
- render the importer section separately,
- render platform tabs from the passed supported-platform list,
- render the active platform’s model processor controls,
- surface apply requests as a richer settings payload instead of only a string importer id.

- [ ] **Step 3: Update the properties panel wiring**

Refactor `PropertiesPanel` so it:
- passes supported platforms and active platform into the view,
- relays the richer apply payload to `EditorSession`,
- keeps its responsibility limited to presentation/input forwarding.

- [ ] **Step 4: Re-run the UI tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportSettingsViewTests|FullyQualifiedName~PropertiesPanel" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the UI refactor**

```bash
rtk git add engine/helengine.editor/components/ui/AssetImportSettingsView.cs engine/helengine.editor/components/ui/PropertiesPanel.cs engine/helengine.editor.tests/AssetImportSettingsViewTests.cs
rtk git commit -m "Add platform processor controls to asset settings UI"
```

## Task 4: Wire Apply Flow And Silent `*.hasset` Regeneration

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor/EditorSession.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerTests.cs`

- [ ] **Step 1: Write the failing manager tests**

Add tests in `AssetImportManagerTests.cs` for:
- unsupported `*.hasset` version causing silent default regeneration,
- saving processor settings for one platform without losing importer settings,
- preserving processor settings for other platforms when one platform is edited.

Add or expand `EditorSession`-adjacent tests if needed for:
- apply-flow persistence through the properties panel event.

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests" -v minimal
```

Expected: FAIL because the manager still treats settings as the old flat model.

- [ ] **Step 2: Refactor asset settings load/save behavior**

Update `AssetImportManager` to:
- detect unsupported `*.hasset` versions,
- create a default current-version settings object silently,
- persist the regenerated file before continuing,
- expose load/save helpers that work with the new importer/processor model.

- [ ] **Step 3: Refactor `EditorSession` apply handling**

Update `EditorSession` so the asset-settings apply flow:
- saves importer settings,
- saves processor settings for the selected platform,
- updates the project’s active platform when the tab changes,
- refreshes the properties panel from the canonical saved state after apply.

- [ ] **Step 4: Re-run the manager tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the apply/regeneration work**

```bash
rtk git add engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor/EditorSession.cs engine/helengine.editor.tests/AssetImportManagerTests.cs
rtk git commit -m "Regenerate unsupported asset settings and persist processor changes"
```

## Task 5: Apply `Flip Winding` During Model Processing And Cache Validation

**Files:**
- Modify: `engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs`
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Modify: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`

- [ ] **Step 1: Write the failing model-processing tests**

Add focused tests in `AssetImportManagerModelTests.cs` for:
- `FlipWinding = false` preserving the current triangle order,
- `FlipWinding = true` reversing triangle winding in processed output,
- changing `FlipWinding` invalidating cache reuse and regenerating the processed model,
- different platform processor settings not leaking into each other.

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests" -v minimal
```

Expected: FAIL because processing does not read platform processor settings yet.

- [ ] **Step 2: Apply processor settings in model conversion**

Update `AssimpSceneModelAssetConverter` so model triangle emission uses `FlipWinding` from the current platform’s processor settings. Keep this as conversion behavior, not renderer behavior.

- [ ] **Step 3: Extend cache invalidation**

Update `AssetImportManager` so processed model cache reuse depends on:
- source checksum,
- importer choice,
- relevant processor settings for the active platform.

Changing `FlipWinding` must force a rebuild instead of reusing stale processed output.

- [ ] **Step 4: Re-run the model-processing tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~AssetImportManagerModelTests" -v minimal
```

Expected: PASS.

- [ ] **Step 5: Commit the processor-behavior work**

```bash
rtk git add engine/helengine.editor.fbximporter/AssimpSceneModelAssetConverter.cs engine/helengine.editor/managers/asset/AssetImportManager.cs engine/helengine.editor.tests/AssetImportManagerModelTests.cs
rtk git commit -m "Apply per-platform model processor settings during import"
```

## Task 6: Final Focused Verification

**Files:**
- Verify the full modified set from Tasks 1-5.

- [ ] **Step 1: Run focused editor test coverage**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~BinarySerializationTests|FullyQualifiedName~EditorProjectLocalSettingsServiceTests|FullyQualifiedName~AssetImportManagerTests|FullyQualifiedName~AssetImportManagerModelTests|FullyQualifiedName~AssetImportSettingsViewTests" -v minimal
```

Expected: PASS.

- [ ] **Step 2: Build the editor assemblies**

Run:

```bash
rtk dotnet build engine/helengine.editor/helengine.editor.csproj -v minimal
```

Expected: PASS with `0 errors`.

- [ ] **Step 3: Inspect worktree before final integration**

Run:

```bash
rtk git status --short
```

Expected: only the intended files from this plan are modified.

- [ ] **Step 4: Create the final integration commit**

```bash
rtk git add engine/helengine.editor engine/helengine.editor.fbximporter engine/helengine.editor.tests docs/superpowers/plans/2026-04-28-platform-asset-processor-settings.md
rtk git commit -m "Add per-platform asset processor settings"
```

- [ ] **Step 5: Record completion details**

Before closing the work:
- summarize which files changed,
- note any test scope that could not run in this environment,
- record the final outcome in Graphiti memory for future sessions.

# AssimpNetter Model Importer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an AssimpNetter-backed editor model importer and wire model assets into the existing asset import manager.

**Architecture:** Keep Assimp isolated in the separate importer project and expose it through the existing `IModelImporter` contract. Extend `AssetImportManager` with model registration, cache import, and lazy load paths that mirror texture/text behavior.

**Tech Stack:** C# net9.0, AssimpNetter 6.0.2.1, xUnit, existing editor asset serialization.

---

### Task 1: Model Import Manager Support

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Create: `engine/helengine.editor/managers/asset/ModelImporterRegistration.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`

- [ ] **Step 1: Write failing registration tests**

Add tests that create an `AssetImportManager`, register a fake model importer, and verify `.obj` resolves to the registered model importer while unsupported extensions return no importer ids.

- [ ] **Step 2: Add model registration type**

Create `ModelImporterRegistration` matching the texture/text registration pattern and forwarding to `AssetImportManager.RegisterModelImporter`.

- [ ] **Step 3: Add model importer dictionaries and lookup methods**

Add model importer storage, duplicate-id checks across texture/text/model importers, `GetModelImporterIds`, `GetImporterIdsForExtension` model handling, and `IsModelExtension`.

- [ ] **Step 4: Verify registration tests pass**

Run:
`dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter AssetImportManagerModelTests --no-restore`

Expected: model registration tests pass.

### Task 2: Model Cache Import and Lazy Load

**Files:**
- Modify: `engine/helengine.editor/managers/asset/AssetImportManager.cs`
- Test: `engine/helengine.editor.tests/AssetImportManagerModelTests.cs`

- [ ] **Step 1: Write failing import/cache tests**

Add tests for `ImportModel`, `TryLoadModelAsset`, valid cache reload, invalid cache payload rejection, and missing model cache import.

- [ ] **Step 2: Implement model import/cache methods**

Add `SetDefaultModelImporter`, `ImportModel`, `TryLoadModelAsset`, `ImportModelsMissingCache`, `TryLoadCachedModelAsset`, model output path resolution, and model importer validation helpers.

- [ ] **Step 3: Wire editor startup cache generation**

Update `EditorSession.InitializeAssetImports` to call `ImportModelsMissingCache()` after texture cache import.

- [ ] **Step 4: Verify model manager tests pass**

Run:
`dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter AssetImportManagerModelTests --no-restore`

Expected: all model asset manager tests pass.

### Task 3: AssimpNetter Importer Library

**Files:**
- Modify: `engine/helengine.editor.fbximporter/helengine.editor.assimp.csproj`
- Modify: `engine/helengine.editor.fbximporter/AssimpImporter.cs`
- Test: `engine/helengine.editor.tests/AssimpModelImporterTests.cs`

- [ ] **Step 1: Write failing OBJ importer tests**

Add a readable OBJ fixture in test code and assert the importer returns positions, normals, UVs, and triangle indices.

- [ ] **Step 2: Replace package dependency**

Replace `AssimpNet` with `AssimpNetter` version `6.0.2.1` and disable nullable annotations to match the repo.

- [ ] **Step 3: Implement Assimp conversion**

Use `AssimpContext.ImportFileFromStream` with triangulation, normal generation, UV generation, vertex join, and cache-locality post-processing. Flatten meshes into one `ModelAsset`, reject empty scenes, non-triangle faces, and over-16-bit vertex counts.

- [ ] **Step 4: Verify importer tests pass**

Run:
`dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter AssimpModelImporterTests --no-restore`

Expected: Assimp importer tests pass.

### Task 4: Editor App Registration

**Files:**
- Modify: `helengine.ui/helengine.editor.app/MainForm.cs`
- Modify: `helengine.ui/helengine.editor.app/helengine.editor.app.csproj`

- [ ] **Step 1: Register model importer extensions**

Register `AssimpModelImporterRegistration` in `BuildImporters()` for `.fbx`, `.obj`, `.gltf`, `.glb`, `.dae`, and `.3ds`.

- [ ] **Step 2: Update project reference if needed**

Keep the editor app referencing the Assimp importer project, but ensure the project metadata no longer describes it as FBX-only.

- [ ] **Step 3: Build the editor app**

Run:
`dotnet build helengine.ui\helengine.editor.app\helengine.editor.app.csproj --no-restore`

Expected: build exits 0.

### Task 5: Full Verification and Commit

**Files:**
- Verify all modified source and tests.

- [ ] **Step 1: Run editor tests**

Run:
`dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --no-restore`

Expected: all tests pass.

- [ ] **Step 2: Review status**

Run:
`git status --short`

Expected: intentional source, test, project, and plan changes only. `.dotnet_home/` remains untracked and excluded.

- [ ] **Step 3: Commit implementation**

Commit the implementation with:
`git commit -m "Add AssimpNetter model importer"`

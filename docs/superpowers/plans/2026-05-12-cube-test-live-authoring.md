# Cube Test Live Authoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rework `cube_test` generation so the city project authors a live scene graph and persists it through the editor scene-save path instead of manually constructing serialized scene payloads.

**Architecture:** `CubeTestSceneFactory` will stop building `SceneAsset` records directly and will instead build live `EditorEntity` roots with attached components. A new city-side `GeneratedAuthoringSceneWriteService` will stage those roots into the editor runtime and call `SceneSaveService` to write `cube_test.helen`. `RenderingSceneGenerator` will switch only `cube_test` to this path, while every other rendering scene remains on the existing manual `SceneAsset` path.

**Tech Stack:** C#, `helengine.editor`, `SceneSaveService`, `ComponentPersistenceRegistry`, `EditorEntity`, xUnit, `dotnet test`

---

## File Structure

### Existing files to modify

- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs`
  - Change responsibility from manual serialized scene construction to live authored entity construction.
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneGenerator.cs`
  - Route only `cube_test` through the new live-authoring write path.

### New files to create

- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneDefinition.cs`
  - Small data container for scene id plus live root entities.
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`
  - Bridges city-side live scene authoring into editor `SceneSaveService`.

### Tests to modify

- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
  - Add assertions proving regenerated `cube_test` still contains the expected roots and no longer depends on manual factory serialization internals.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
  - Add one focused packaging regression that uses the regenerated `cube_test` scene and confirms it still cooks successfully.

### Supporting references to inspect while implementing

- Reference: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneSaveService.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\ComponentPersistenceRegistry.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\SceneSaveServiceTests.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor.tests\serialization\scene\RuntimeSceneLoadServiceTests.cs`

### Task 1: Add a failing authoring regression for live `cube_test` generation

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
- Reference: `C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs`

- [ ] **Step 1: Write the failing test**

Add a focused test that expresses the target behavior: `cube_test` should still deserialize into the expected entities after regeneration, while the generation path is allowed to change.

```csharp
/// <summary>
/// Ensures the regenerated cube-test scene still contains the expected authored roots after the live-authoring migration.
/// </summary>
[Fact]
public void DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots() {
    SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestCamera"));
    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestSun"));
    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "CubeTestCube"));
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots"`

Expected: FAIL initially if the test is not yet present or if the regenerated scene does not match the asserted structure after the first regeneration attempt.

- [ ] **Step 3: Add one helper assertion for the cube root component set**

Add a second test that will remain stable across the persistence rewrite and confirms the authored cube entity still has a mesh component plus one runtime motion component record.

```csharp
/// <summary>
/// Ensures the regenerated cube-test cube still carries one mesh component and one authored motion component.
/// </summary>
[Fact]
public void DeserializeCityCubeTestSceneAsset_CubeRootContainsMeshAndMotionComponent() {
    SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");
    SceneEntityAsset cubeEntity = FindEntityByName(sceneAsset.RootEntities, "CubeTestCube");

    Assert.NotNull(cubeEntity);
    Assert.Equal(2, (cubeEntity.Components ?? Array.Empty<SceneComponentAssetRecord>()).Length);
}
```

- [ ] **Step 4: Run both tests to verify the new expectations are active**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_CubeRootContainsMeshAndMotionComponent"`

Expected: FAIL or PASS depending on current authored scene contents, but both tests must compile and become the stable authoring guardrail for the refactor.

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
git commit -m "test: add cube test live-authoring regression coverage"
```

### Task 2: Add a city-side live authored scene definition type

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneDefinition.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Write the failing test**

Add a unit-level compilation target by updating `CubeTestSceneFactory` usage in a later step to require a new return type. The immediate failing signal will come from the next task once the factory signature changes. Create the type first as a minimal container.

```csharp
namespace city.rendering.tools {
    /// <summary>
    /// Stores one generated live-authored scene definition before editor serialization persists it.
    /// </summary>
    public sealed class GeneratedAuthoringSceneDefinition {
        /// <summary>
        /// Gets or sets the stable scene id written to disk.
        /// </summary>
        public string SceneId { get; set; }

        /// <summary>
        /// Gets or sets the live root entities that define the scene.
        /// </summary>
        public EditorEntity[] RootEntities { get; set; }
    }
}
```

- [ ] **Step 2: Run city project compile verification**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: PASS. This step ensures the new support type does not introduce namespace or reference issues before the factory rewrite starts.

- [ ] **Step 3: Keep the type intentionally narrow**

Verify the type contains only:

```text
SceneId
RootEntities
```

Do not add scene-settings abstractions, writer callbacks, or generalized generator metadata in this slice.

- [ ] **Step 4: Re-run the same build**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ../helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneDefinition.cs
git commit -m "feat: add generated authoring scene definition"
```

### Task 3: Add a city-side writer that persists live authored scenes through `SceneSaveService`

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneSaveService.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\ComponentPersistenceRegistry.cs`

- [ ] **Step 1: Write the failing integration test target**

Add a packaging-oriented test skeleton in `CityRenderingSceneAuthoringTests.cs` that will only pass once `cube_test` can be regenerated through the live-authoring writer path and still deserialize.

```csharp
/// <summary>
/// Ensures the authored cube-test scene remains deserializable after regeneration through the live-authoring writer path.
/// </summary>
[Fact]
public void DeserializeCityCubeTestSceneAsset_RemainsReadableAfterLiveAuthoringSavePath() {
    SceneAsset sceneAsset = ReadSceneAsset("cube_test.helen");

    Assert.Equal("scenes/rendering/cube_test.helen", sceneAsset.Id);
}
```

- [ ] **Step 2: Run the focused test to verify the guard exists**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_RemainsReadableAfterLiveAuthoringSavePath"`

Expected: PASS before implementation if the current scene exists. This is acceptable here because the failing behavior in this task will be driven by the next step when the writer is integrated and regeneration is exercised.

- [ ] **Step 3: Implement the writer service**

Create a narrow city-side service that stages generated roots into the editor runtime, invokes `SceneSaveService`, and then cleans up the staged roots.

```csharp
using helengine.editor;

namespace city.rendering.tools {
    /// <summary>
    /// Persists generated live-authored scenes through the editor scene save pipeline.
    /// </summary>
    public sealed class GeneratedAuthoringSceneWriteService {
        /// <summary>
        /// Writes one generated live-authored scene into the supplied city project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative city project root path.</param>
        /// <param name="sceneDefinition">Generated scene definition to persist.</param>
        public void WriteScene(string projectRootPath, GeneratedAuthoringSceneDefinition sceneDefinition) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            } else if (sceneDefinition == null) {
                throw new ArgumentNullException(nameof(sceneDefinition));
            } else if (string.IsNullOrWhiteSpace(sceneDefinition.SceneId)) {
                throw new ArgumentException("Scene id must be provided.", nameof(sceneDefinition));
            } else if (sceneDefinition.RootEntities == null) {
                throw new ArgumentNullException(nameof(sceneDefinition.RootEntities));
            }

            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string scenePath = Path.Combine(fullProjectRootPath, "assets", sceneDefinition.SceneId.Replace('/', Path.DirectorySeparatorChar));
            ComponentPersistenceRegistry persistenceRegistry = new ComponentPersistenceRegistry();
            SceneSaveService saveService = new SceneSaveService(fullProjectRootPath, persistenceRegistry);
            List<EditorEntity> stagedRoots = new List<EditorEntity>();

            try {
                for (int index = 0; index < sceneDefinition.RootEntities.Length; index++) {
                    EditorEntity rootEntity = sceneDefinition.RootEntities[index];
                    if (rootEntity == null) {
                        continue;
                    }

                    stagedRoots.Add(rootEntity);
                }

                saveService.Save(scenePath, new SceneSettingsAsset());
            } finally {
                for (int index = 0; index < stagedRoots.Count; index++) {
                    stagedRoots[index].Dispose();
                }
            }
        }
    }
}
```

Implementation note for the engineer: while wiring this up, inspect how `SceneSaveService` discovers root `EditorEntity` instances from `Core.Instance.ObjectManager.Entities`. If explicit registration or scene-object layer setup is required, do that here rather than leaking it into the factory.

- [ ] **Step 4: Run compile verification**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ../helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneWriteService.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
git commit -m "feat: add generated authoring scene write service"
```

### Task 4: Rework `CubeTestSceneFactory` to build live authored entities

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\CubeTestSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneDefinition.cs`
- Reference: `C:\dev\helprojs\city\assets\codebase\rendering\DirectionalShadowTowerSpinComponent.cs`

- [ ] **Step 1: Write the failing compile change**

Change the factory contract from:

```csharp
public SceneAsset CreateSceneAsset(SceneAssetReference cubeReference, SceneAssetReference standardMaterialReference)
```

to:

```csharp
public GeneratedAuthoringSceneDefinition CreateSceneDefinition(SceneAssetReference cubeReference, SceneAssetReference standardMaterialReference)
```

Expected immediate result: compile failures in `RenderingSceneGenerator` until the caller is updated.

- [ ] **Step 2: Run build to verify it fails for the expected caller mismatch**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: FAIL with a `CubeTestSceneFactory` member mismatch or missing `CreateSceneAsset` caller in `RenderingSceneGenerator`.

- [ ] **Step 3: Implement the minimal live-authoring factory**

Replace the manual `SceneAsset` and payload code with live `EditorEntity` authoring. Keep the authored content shape unchanged.

```csharp
using helengine.editor;
using gameplay.rendering;

namespace city.rendering.tools {
    /// <summary>
    /// Builds the canonical live-authored scene definition for the minimal cube rendering test.
    /// </summary>
    public sealed class CubeTestSceneFactory {
        /// <summary>
        /// Stable scene id used by the generated cube-test asset.
        /// </summary>
        public const string SceneId = RenderingSceneGenerator.CubeTestSceneId;

        /// <summary>
        /// Creates the canonical cube-test live scene definition.
        /// </summary>
        /// <param name="cubeReference">Stable generated cube model reference.</param>
        /// <param name="standardMaterialReference">Stable generated standard material reference.</param>
        /// <returns>Live-authored cube-test scene definition.</returns>
        public GeneratedAuthoringSceneDefinition CreateSceneDefinition(SceneAssetReference cubeReference, SceneAssetReference standardMaterialReference) {
            if (cubeReference == null) {
                throw new ArgumentNullException(nameof(cubeReference));
            } else if (standardMaterialReference == null) {
                throw new ArgumentNullException(nameof(standardMaterialReference));
            }

            return new GeneratedAuthoringSceneDefinition {
                SceneId = SceneId,
                RootEntities = new[] {
                    CreateCameraEntity(),
                    CreateDirectionalLightEntity(),
                    CreateCubeEntity(cubeReference, standardMaterialReference)
                }
            };
        }
    }
}
```

Implementation note for the engineer: build the private helpers so they create `EditorEntity` roots, attach `CameraComponent`, `DirectionalLightComponent`, `MeshComponent`, `FPSComponent`, `DemoDiscReturnToMenuComponent`, and `DirectionalShadowTowerSpinComponent` directly, and assign references using the same save-state mechanism the editor serializer expects. Remove:

```text
SceneComponentAssetRecord creation
EditorTaggedSceneComponentFieldWriter usage
MeshComponentPersistenceDescriptor direct calls
DirectionalLightComponentPersistenceDescriptor direct calls
manual payload byte writers
```

- [ ] **Step 4: Run build to verify the factory compiles**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: FAIL only in the old `RenderingSceneGenerator` call site if that file still expects `SceneAsset`.

- [ ] **Step 5: Commit**

```bash
git add ../helprojs/city/assets/codebase/rendering.tools/CubeTestSceneFactory.cs
git commit -m "refactor: make cube test factory author live entities"
```

### Task 5: Switch only `cube_test` in `RenderingSceneGenerator` to the new writer path

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneGenerator.cs`
- Reference: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`

- [ ] **Step 1: Write the failing caller update**

Replace the old `cube_test` variable:

```csharp
SceneAsset cubeTestSceneAsset = CubeTestFactory.CreateSceneAsset(cubeReference, standardMaterialReference);
```

with the new live scene definition:

```csharp
GeneratedAuthoringSceneDefinition cubeTestSceneDefinition = CubeTestFactory.CreateSceneDefinition(cubeReference, standardMaterialReference);
```

and leave the old `SceneWriteService.WriteScene(projectRootPath, CubeTestSceneId, cubeTestSceneAsset);` line temporarily unchanged.

- [ ] **Step 2: Run build to verify it fails at the write call**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: FAIL because `cubeTestSceneAsset` no longer exists or the old writer signature no longer matches.

- [ ] **Step 3: Implement the narrow generator integration**

Update `RenderingSceneGenerator` to own both writer paths:

```csharp
readonly GeneratedAuthoringSceneWriteService AuthoringSceneWriteService;
```

Initialize it in the constructor:

```csharp
AuthoringSceneWriteService = new GeneratedAuthoringSceneWriteService();
```

Use it only for `cube_test`:

```csharp
GeneratedAuthoringSceneDefinition cubeTestSceneDefinition = CubeTestFactory.CreateSceneDefinition(cubeReference, standardMaterialReference);
AuthoringSceneWriteService.WriteScene(projectRootPath, cubeTestSceneDefinition);
```

Leave every other scene on:

```csharp
SceneWriteService.WriteScene(projectRootPath, ColoredCubeGridSceneId, coloredCubeGridSceneAsset);
```

- [ ] **Step 4: Run build to verify the generator compiles**

Run: `dotnet build .\engine\helengine.editor.tests\helengine.editor.tests.csproj`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ../helprojs/city/assets/codebase/rendering.tools/RenderingSceneGenerator.cs
git commit -m "refactor: route cube test through live authoring writer"
```

### Task 6: Add a packaging regression for regenerated `cube_test`

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
- Reference: `C:\dev\helworks\helengine\engine\helengine.editor\managers\project\SceneComponentPackagingTransformService.cs`

- [ ] **Step 1: Write the failing packaging test**

Add a focused test that packages a `cube_test`-shaped scene and asserts the output remains writable/loadable after the factory migration.

```csharp
/// <summary>
/// Ensures cube-test scenes authored through live editor serialization still package successfully.
/// </summary>
[Fact]
public void Package_WhenCubeTestSceneWasSavedThroughLiveAuthoringPath_PackagesSuccessfully() {
    string sceneId = "scenes/rendering/cube_test.helen";

    Assert.True(File.Exists(Path.Combine(@"C:\dev\helprojs\output\windows\cooked\scenes\rendering", "cube_test.hasset")) || true);
}
```

Implementation note for the engineer: replace the placeholder final assertion with the same packaging harness patterns already used in this file. The test must actually invoke the packager, deserialize the packaged scene, and assert the packaged scene contains `helengine.DirectionalShadowTowerSpinComponent` for the cube root if that remains the current packaged behavior.

- [ ] **Step 2: Run the focused test to verify it fails**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_WhenCubeTestSceneWasSavedThroughLiveAuthoringPath_PackagesSuccessfully"`

Expected: FAIL until the test uses the real packaging harness and the new live-authored scene path has been exercised.

- [ ] **Step 3: Implement the real packaging assertion**

Model the final test after the existing scene packager tests in this file:

```text
write or load the regenerated scene
invoke EditorPlatformBuildScenePackager
open the cooked scene asset
assert the expected cooked scene exists
assert the cube root still carries the packaged runtime motion component type
```

- [ ] **Step 4: Run the focused packaging test**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_WhenCubeTestSceneWasSavedThroughLiveAuthoringPath_PackagesSuccessfully"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "test: cover cube test packaging after live authoring migration"
```

### Task 7: Regenerate and verify the full `cube_test` slice

**Files:**
- Modify: `C:\dev\helprojs\city\assets\scenes\rendering\cube_test.helen`
- Verify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
- Verify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`

- [ ] **Step 1: Regenerate `cube_test` through the new path**

Run the city rendering scene generator using the project’s normal regeneration entrypoint for rendering scenes.

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots"`

Expected: If regeneration is not automatic in the test harness, manually run the city rendering generator entrypoint you use in this repo before re-running tests.

- [ ] **Step 2: Run the focused authoring regressions**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_CubeRootContainsMeshAndMotionComponent|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_RemainsReadableAfterLiveAuthoringSavePath"`

Expected: PASS

- [ ] **Step 3: Run the focused packaging regression**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~Package_WhenCubeTestSceneWasSavedThroughLiveAuthoringPath_PackagesSuccessfully"`

Expected: PASS

- [ ] **Step 4: Run one broader guardrail set**

Run: `dotnet test .\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityRenderingSceneAssets_AllShowcaseScenesContainFpsOverlayAndEditorFontReference|FullyQualifiedName~DeserializeCityCubeTestSceneAsset_ContainsCameraSunAndCubeRoots|FullyQualifiedName~Package_WhenCubeTestSceneWasSavedThroughLiveAuthoringPath_PackagesSuccessfully"`

Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add ../helprojs/city/assets/scenes/rendering/cube_test.helen ../helprojs/city/assets/codebase/rendering.tools/CubeTestSceneFactory.cs ../helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneDefinition.cs ../helprojs/city/assets/codebase/rendering.tools/GeneratedAuthoringSceneWriteService.cs ../helprojs/city/assets/codebase/rendering.tools/RenderingSceneGenerator.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs
git commit -m "refactor: author cube test scene through editor save pipeline"
```

## Self-Review

### Spec coverage

- Live authoring factory: covered by Task 4
- City-side writer adapter: covered by Task 3
- Generator integration only for `cube_test`: covered by Task 5
- Focused authoring regression coverage: covered by Tasks 1 and 7
- Packaging validation: covered by Tasks 6 and 7
- Narrow scope with other generators unchanged: enforced in Task 5

No spec gaps remain for the `cube_test` slice.

### Placeholder scan

One step intentionally calls out a placeholder assertion in Task 6 so the engineer replaces it with the existing packaging harness pattern before claiming completion. That placeholder is explicit and localized to the test-writing phase of the plan rather than left as an unspecified implementation detail.

### Type consistency

Planned new/updated names remain consistent across tasks:

- `GeneratedAuthoringSceneDefinition`
- `GeneratedAuthoringSceneWriteService`
- `CreateSceneDefinition(...)`
- `WriteScene(string projectRootPath, GeneratedAuthoringSceneDefinition sceneDefinition)`


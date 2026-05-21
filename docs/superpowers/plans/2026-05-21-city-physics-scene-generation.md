# City Physics Scene Generation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move physics demo scene generation ownership into the city project while preserving the existing generated scene ids and layouts.

**Architecture:** Add a city-owned physics scene catalog and factory under `C:\dev\helprojs\city\assets\codebase\physics.tools`. Update the city generator to call those types directly, and add source/asset tests in helengine editor tests that prevent the command from delegating to `helengine.editor.PhysicsValidationSceneFactory`.

**Tech Stack:** C# 12, xUnit, helengine editor asset serialization, city project editor modules, `rtk` command wrapper.

---

## File Structure

- Create: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs`
  - Owns the stable ordered list of city generated physics scene ids.
- Create: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs`
  - Owns city physics scene creation, support shader/material creation, and scene writing.
- Modify: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneGenerator.cs`
  - Replaces the editor factory dependency with the city factory.
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
  - Adds ownership/source tests and generated physics scene asset tests for the city project.

The existing editor `PhysicsValidationSceneFactory` and `PhysicsValidationSceneCatalog` should remain in place for now so editor physics tests continue to validate generic editor packaging/runtime behavior. This plan changes city production ownership first; removing or shrinking editor fixtures can be a later cleanup once equivalent city integration coverage exists.

---

### Task 1: Add Failing City Ownership Tests

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Add source-level tests for city physics ownership**

Insert these tests near the existing rendering ownership tests:

```csharp
        /// <summary>
        /// Ensures the city physics generation command uses the city-owned physics scene generator.
        /// </summary>
        [Fact]
        public void ReadCityGeneratePhysicsScenesCommandSource_UsesPhysicsSceneGenerator() {
            string source = ReadCitySource("menu.tools", "GeneratePhysicsScenesCommand.cs");

            Assert.Contains("new PhysicsSceneGenerator()", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics scene generator no longer delegates generated scene ownership back to the editor validation factory.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneGeneratorSource_UsesCityPhysicsSceneFactory() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneGenerator.cs");

            Assert.Contains("new PhysicsSceneFactory()", source, StringComparison.Ordinal);
            Assert.Contains("factory.WriteScenes(projectRootPath);", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics tools module owns the generated physics scene catalog source.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneCatalogSource_DeclaresStablePhysicsSceneIds() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneCatalog.cs");

            Assert.Contains("public static class PhysicsSceneCatalog", source, StringComparison.Ordinal);
            Assert.Contains("CharacterSlopeSceneId", source, StringComparison.Ordinal);
            Assert.Contains("DynamicStackBoxesSceneId", source, StringComparison.Ordinal);
            Assert.Contains("TriggerVolumeSceneId", source, StringComparison.Ordinal);
            Assert.Contains("public static string[] GetSceneIds()", source, StringComparison.Ordinal);
        }

        /// <summary>
        /// Ensures the city physics tools module owns the generated physics scene factory source.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSceneFactorySource_DeclaresCitySceneFactory() {
            string source = ReadCitySource("physics.tools", "PhysicsSceneFactory.cs");

            Assert.Contains("public sealed class PhysicsSceneFactory", source, StringComparison.Ordinal);
            Assert.Contains("public SceneAsset CreateSceneAsset(string sceneId)", source, StringComparison.Ordinal);
            Assert.Contains("public void WriteScenes(string projectRootPath)", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneFactory", source, StringComparison.Ordinal);
            Assert.DoesNotContain("PhysicsValidationSceneCatalog", source, StringComparison.Ordinal);
        }
```

- [ ] **Step 2: Add generated asset tests for the city physics folder**

Add these tests after the source-level ownership tests:

```csharp
        /// <summary>
        /// Ensures the generated city physics scenes exist as normal authored project scene assets.
        /// </summary>
        [Fact]
        public void DeserializeCityPhysicsScenes_AllGeneratedPhysicsScenesExist() {
            string[] sceneFileNames = new[] {
                "test_scene_character_slope.helen",
                "test_scene_character_steps.helen",
                "test_scene_character_moving_platform.helen",
                "test_scene_dynamic_stack_boxes.helen",
                "test_scene_dynamic_sphere_ramp.helen",
                "test_scene_kinematic_push.helen",
                "test_scene_mesh_ground_stability.helen",
                "test_scene_trigger_volume.helen"
            };

            for (int index = 0; index < sceneFileNames.Length; index++) {
                SceneAsset sceneAsset = ReadPhysicsSceneAsset(sceneFileNames[index]);

                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Camera", StringComparison.Ordinal));
                Assert.Contains(sceneAsset.RootEntities, entity => string.Equals(entity.Name, "Scenario", StringComparison.Ordinal));
            }
        }

        /// <summary>
        /// Ensures a representative city physics scene contains serialized 3D physics component records.
        /// </summary>
        [Fact]
        public void DeserializeCityDynamicStackBoxesPhysicsScene_ContainsRigidBodyAndColliderRecords() {
            SceneAsset sceneAsset = ReadPhysicsSceneAsset("test_scene_dynamic_stack_boxes.helen");

            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.RigidBody3DComponent") >= 2);
            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.BoxCollider3DComponent") >= 2);
        }

        /// <summary>
        /// Ensures a representative city character physics scene contains serialized character-controller records.
        /// </summary>
        [Fact]
        public void DeserializeCityCharacterSlopePhysicsScene_ContainsCharacterControllerRecord() {
            SceneAsset sceneAsset = ReadPhysicsSceneAsset("test_scene_character_slope.helen");

            Assert.True(CountComponents(sceneAsset.RootEntities, "helengine.CharacterController3DComponent") >= 1);
        }

        /// <summary>
        /// Ensures the city physics generation flow emits the shared support shader and materials beside the project assets.
        /// </summary>
        [Fact]
        public void ReadCityPhysicsSupportAssets_EmitsShaderAndMaterials() {
            string shaderPath = Path.Combine(CityProjectRootPath, "assets", "Shaders", "physics", "PhysicsDemoMesh.hlsl");
            string neutralMaterialPath = Path.Combine(CityProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoNeutral.hasset");
            string blueMaterialPath = Path.Combine(CityProjectRootPath, "assets", "Materials", "physics", "PhysicsDemoBlue.hasset");

            Assert.True(File.Exists(shaderPath));
            Assert.True(File.Exists(neutralMaterialPath));
            Assert.True(File.Exists(blueMaterialPath));
            Assert.Contains("cbuffer MaterialColorBuffer", File.ReadAllText(shaderPath), StringComparison.Ordinal);
        }
```

- [ ] **Step 3: Add a city physics scene asset reader helper**

Add this helper beside `ReadSceneAsset`:

```csharp
        /// <summary>
        /// Reads one generated city physics scene asset from the authored project scene folder.
        /// </summary>
        /// <param name="sceneFileName">File name of the authored physics scene.</param>
        /// <returns>Deserialized scene asset.</returns>
        SceneAsset ReadPhysicsSceneAsset(string sceneFileName) {
            string scenePath = Path.Combine(CityProjectRootPath, "assets", "scenes", "physics", sceneFileName);
            Assert.True(File.Exists(scenePath));

            using FileStream stream = File.OpenRead(scenePath);
            return Assert.IsType<SceneAsset>(EditorAssetBinarySerializer.Deserialize(stream));
        }
```

- [ ] **Step 4: Run tests to verify the new tests fail**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests" --no-restore
```

Expected: FAIL because `PhysicsSceneCatalog.cs`, `PhysicsSceneFactory.cs`, generated physics scenes, and support assets do not exist in the city project yet, and `PhysicsSceneGenerator.cs` still contains `PhysicsValidationSceneFactory`.

- [ ] **Step 5: Commit the failing tests**

```powershell
rtk git add engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs
rtk git commit -m "test: cover city physics scene generation ownership"
```

---

### Task 2: Move Physics Catalog Into City

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneCatalog.cs`

- [ ] **Step 1: Create the city-owned catalog**

Create `PhysicsSceneCatalog.cs` with this content:

```csharp
namespace city.physics.tools {
    /// <summary>
    /// Enumerates the generated city physics showcase scenes authored for demo-disc playback.
    /// </summary>
    public static class PhysicsSceneCatalog {
        /// <summary>
        /// Relative scene id for the character slope showcase scene.
        /// </summary>
        public const string CharacterSlopeSceneId = "scenes/physics/test_scene_character_slope.helen";

        /// <summary>
        /// Relative scene id for the character steps showcase scene.
        /// </summary>
        public const string CharacterStepsSceneId = "scenes/physics/test_scene_character_steps.helen";

        /// <summary>
        /// Relative scene id for the character moving-platform showcase scene.
        /// </summary>
        public const string CharacterMovingPlatformSceneId = "scenes/physics/test_scene_character_moving_platform.helen";

        /// <summary>
        /// Relative scene id for the stacked dynamic-body showcase scene.
        /// </summary>
        public const string DynamicStackBoxesSceneId = "scenes/physics/test_scene_dynamic_stack_boxes.helen";

        /// <summary>
        /// Relative scene id for the sphere-ramp showcase scene.
        /// </summary>
        public const string DynamicSphereRampSceneId = "scenes/physics/test_scene_dynamic_sphere_ramp.helen";

        /// <summary>
        /// Relative scene id for the kinematic push showcase scene.
        /// </summary>
        public const string KinematicPushSceneId = "scenes/physics/test_scene_kinematic_push.helen";

        /// <summary>
        /// Relative scene id for the static-mesh ground stability showcase scene.
        /// </summary>
        public const string MeshGroundStabilitySceneId = "scenes/physics/test_scene_mesh_ground_stability.helen";

        /// <summary>
        /// Relative scene id for the trigger-volume showcase scene.
        /// </summary>
        public const string TriggerVolumeSceneId = "scenes/physics/test_scene_trigger_volume.helen";

        /// <summary>
        /// Stable ordered list of generated city physics showcase scene ids.
        /// </summary>
        static readonly string[] SceneIds = new[] {
            CharacterSlopeSceneId,
            CharacterStepsSceneId,
            CharacterMovingPlatformSceneId,
            DynamicStackBoxesSceneId,
            DynamicSphereRampSceneId,
            KinematicPushSceneId,
            MeshGroundStabilitySceneId,
            TriggerVolumeSceneId
        };

        /// <summary>
        /// Gets the stable ordered list of generated city physics showcase scene ids.
        /// </summary>
        /// <returns>Ordered scene ids used by city demo tooling.</returns>
        public static string[] GetSceneIds() {
            string[] copy = new string[SceneIds.Length];
            Array.Copy(SceneIds, copy, SceneIds.Length);
            return copy;
        }
    }
}
```

- [ ] **Step 2: Run the source ownership tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ReadCityPhysicsSceneCatalogSource_DeclaresStablePhysicsSceneIds" --no-restore
```

Expected: PASS.

- [ ] **Step 3: Commit the catalog**

```powershell
rtk git -C C:\dev\helprojs\city add assets\codebase\physics.tools\PhysicsSceneCatalog.cs
rtk git -C C:\dev\helprojs\city commit -m "feat: add city physics scene catalog"
```

---

### Task 3: Move Physics Scene Factory Into City

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs`

- [ ] **Step 1: Copy the editor factory as the starting point**

Run:

```powershell
rtk proxy powershell.exe -NoProfile -Command "Copy-Item 'C:\dev\helworks\helengine\engine\helengine.editor\managers\physics\PhysicsValidationSceneFactory.cs' 'C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs'"
```

Expected: `PhysicsSceneFactory.cs` exists in the city physics tools folder.

- [ ] **Step 2: Rename the namespace, class, and catalog references**

In `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs`, make these exact replacements:

```text
namespace helengine.editor {
```

to:

```text
namespace city.physics.tools {
```

```text
public sealed class PhysicsValidationSceneFactory
```

to:

```text
public sealed class PhysicsSceneFactory
```

```text
public PhysicsValidationSceneFactory()
```

to:

```text
public PhysicsSceneFactory()
```

```text
PhysicsValidationSceneCatalog
```

to:

```text
PhysicsSceneCatalog
```

Also update XML comments that say `validation scene` or `validation scenes` where they describe city production content:

```text
physics showcase scene
physics showcase scenes
```

Keep comments that describe validation-style scenarios if they refer to the behavior being exercised inside a generated scene.

- [ ] **Step 3: Verify no old ownership names remain in the city factory**

Run:

```powershell
rtk rg -n "PhysicsValidationSceneFactory|PhysicsValidationSceneCatalog|namespace helengine.editor" C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneFactory.cs
```

Expected: no matches.

- [ ] **Step 4: Run the source ownership test for the factory**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ReadCityPhysicsSceneFactorySource_DeclaresCitySceneFactory" --no-restore
```

Expected: PASS.

- [ ] **Step 5: Commit the factory**

```powershell
rtk git -C C:\dev\helprojs\city add assets\codebase\physics.tools\PhysicsSceneFactory.cs
rtk git -C C:\dev\helprojs\city commit -m "feat: move physics scene factory into city"
```

---

### Task 4: Update City Generator To Use The City Factory

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\physics.tools\PhysicsSceneGenerator.cs`

- [ ] **Step 1: Replace the editor factory usage**

Change `Generate` to:

```csharp
        /// <summary>
        /// Writes the current city physics showcase scene set into the supplied project.
        /// </summary>
        /// <param name="projectRootPath">Absolute or relative city project root path.</param>
        public void Generate(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            PhysicsSceneFactory factory = new PhysicsSceneFactory();
            factory.WriteScenes(projectRootPath);
        }
```

- [ ] **Step 2: Run the generator source ownership tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ReadCityGeneratePhysicsScenesCommandSource_UsesPhysicsSceneGenerator|FullyQualifiedName~ReadCityPhysicsSceneGeneratorSource_UsesCityPhysicsSceneFactory" --no-restore
```

Expected: PASS.

- [ ] **Step 3: Commit the generator update**

```powershell
rtk git -C C:\dev\helprojs\city add assets\codebase\physics.tools\PhysicsSceneGenerator.cs
rtk git -C C:\dev\helprojs\city commit -m "feat: use city physics scene factory"
```

---

### Task 5: Generate City Physics Assets

**Files:**
- Create generated files under `C:\dev\helprojs\city\assets\scenes\physics`
- Create or update generated files under `C:\dev\helprojs\city\assets\Shaders\physics`
- Create or update generated files under `C:\dev\helprojs\city\assets\Materials\physics`

- [ ] **Step 1: Run the city physics scene generation command path**

Run:

```powershell
rtk dotnet run --project helengine.ui\helengine.editor.app\helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --editor-command menu.generate-physics-scenes
```

Expected: the editor command host exits successfully after invoking the city `menu.generate-physics-scenes` command.

Expected generated scene files:

```text
C:\dev\helprojs\city\assets\scenes\physics\test_scene_character_slope.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_character_steps.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_character_moving_platform.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_dynamic_stack_boxes.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_dynamic_sphere_ramp.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_kinematic_push.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_mesh_ground_stability.helen
C:\dev\helprojs\city\assets\scenes\physics\test_scene_trigger_volume.helen
```

Expected support files:

```text
C:\dev\helprojs\city\assets\Shaders\physics\PhysicsDemoMesh.hlsl
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoNeutral.hasset
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoBlue.hasset
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoGreen.hasset
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoMagenta.hasset
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoYellow.hasset
C:\dev\helprojs\city\assets\Materials\physics\PhysicsDemoCyan.hasset
```

- [ ] **Step 2: Verify the generated file list**

Run:

```powershell
rtk proxy powershell.exe -NoProfile -Command "Get-ChildItem 'C:\dev\helprojs\city\assets\scenes\physics','C:\dev\helprojs\city\assets\Shaders\physics','C:\dev\helprojs\city\assets\Materials\physics' -File | Select-Object FullName"
```

Expected: the eight `.helen` scene files, one shader file, and six material files are listed.

- [ ] **Step 3: Run generated asset tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~DeserializeCityPhysicsScenes_AllGeneratedPhysicsScenesExist|FullyQualifiedName~DeserializeCityDynamicStackBoxesPhysicsScene_ContainsRigidBodyAndColliderRecords|FullyQualifiedName~DeserializeCityCharacterSlopePhysicsScene_ContainsCharacterControllerRecord|FullyQualifiedName~ReadCityPhysicsSupportAssets_EmitsShaderAndMaterials" --no-restore
```

Expected: PASS.

- [ ] **Step 4: Commit generated city physics assets**

```powershell
rtk git -C C:\dev\helprojs\city add assets\scenes\physics assets\Shaders\physics assets\Materials\physics
rtk git -C C:\dev\helprojs\city commit -m "chore: generate city physics scene assets"
```

---

### Task 6: Run Focused Regression Tests

**Files:**
- No file edits expected.

- [ ] **Step 1: Run all city authoring tests**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests" --no-restore
```

Expected: PASS.

- [ ] **Step 2: Run existing physics factory tests to confirm editor coverage remains intact**

Run:

```powershell
rtk dotnet test engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~PhysicsValidationSceneFactoryTests" --no-restore
```

Expected: PASS.

- [ ] **Step 3: Inspect final git status in both repos**

Run:

```powershell
rtk git status --short --branch
rtk git -C C:\dev\helprojs\city status --short --branch
```

Expected: only pre-existing unrelated changes remain unstaged, plus the commits created by this plan. Do not revert unrelated work.

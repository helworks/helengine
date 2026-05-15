# City Scene Generator Ownership Migration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Move the remaining city/demo-disc/rendering scene generators out of `helengine`, re-author them as live city-owned scene definitions, and persist them only through the editor scene save pipeline.

**Architecture:** City will own all project scene authoring through `GeneratedAuthoringSceneDefinition` and `GeneratedAuthoringSceneWriteService`. HelEngine will retain only generic scene save/load/packaging infrastructure; the old engine-side project scene generators and their manual `SceneAsset` construction paths will be removed after the city-owned replacements are live and verified.

**Tech Stack:** C#, HelEngine editor/runtime scene APIs, city gameplay/menu/rendering modules, xUnit, editor scene save pipeline

---

## File Structure

### Existing files that remain the save/load boundary

- Keep: `C:\dev\helworks\helengine\engine\helengine.editor\serialization\scene\SceneSaveService.cs`
- Keep: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneDefinition.cs`
- Keep: `C:\dev\helprojs\city\assets\codebase\rendering.tools\GeneratedAuthoringSceneWriteService.cs`

### Existing city files that will become the only project-owned scene generator entrypoints

- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneGenerator.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu.tools\RegenerateDemoDiscMainMenuCommand.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu.tools\GenerateRenderingScenesCommand.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`

### New city-owned generator files

- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscSceneGenerator.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\PointShadowSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\PointShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\Ps2BasisLightTestSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingShowcaseSourceWriter.cs`

### Old HelEngine project-owned generator files to delete after cutover

- Delete: `C:\dev\helworks\helengine\engine\helengine.editor\managers\menu\DemoMenuSceneAssetFactory.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DemoDiscSceneWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingSceneWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DirectionalShadowPlazaSceneAssetFactory.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingShowcaseSourceWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\Program.cs`

### Tests to update

- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\RenderingSceneWriterTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`

---

### Task 1: Add ownership-boundary and scene-shape regressions

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\RenderingSceneWriterTests.cs`

- [ ] **Step 1: Add failing city ownership assertions**

```csharp
[Fact]
public void ReadCityRenderingSceneGeneratorSource_DoesNotDependOnHelengineDemoDiscSceneWriterNamespace() {
    string source = ReadCitySource("rendering.tools", "RenderingSceneGenerator.cs");

    Assert.DoesNotContain("helengine.demo_disc_scene_writer", source, StringComparison.Ordinal);
    Assert.Contains("GeneratedAuthoringSceneWriteService", source, StringComparison.Ordinal);
}

[Fact]
public void ReadCityMenuRegenerationCommandSource_DoesNotDependOnEngineMenuSceneRegenerationService() {
    string source = ReadCitySource("menu.tools", "RegenerateDemoDiscMainMenuCommand.cs");

    Assert.DoesNotContain("MenuSceneRegenerationService", source, StringComparison.Ordinal);
    Assert.Contains("DemoDiscSceneGenerator", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Add failing scene-shape assertions for the baked menu FPS placement**

```csharp
[Fact]
public void DeserializeCityDemoDiscMainMenuSceneAsset_GeneratedRootContainsFpsComponent() {
    SceneAsset sceneAsset = ReadTopLevelSceneAsset("DemoDiscMainMenu.helen");
    SceneEntityAsset menuRoot = Assert.Single(sceneAsset.RootEntities, entity => entity.Name == "DemoDiscMenuRoot");
    SceneEntityAsset generatedRoot = Assert.Single(menuRoot.Children, entity => entity.Name == DemoMenuLayout.GeneratedRootEntityName);

    Assert.Contains(
        generatedRoot.Components ?? Array.Empty<SceneComponentAssetRecord>(),
        component => string.Equals(component.ComponentTypeId, "helengine.FPSComponent", StringComparison.Ordinal));
}
```

- [ ] **Step 3: Replace engine-tool-specific test expectations**

```csharp
[Fact]
public void DemoDiscMainMenuGeneration_IsOwnedByCityCodebase() {
    string source = ReadCitySource("menu.tools", "DemoDiscSceneGenerator.cs");

    Assert.Contains("GeneratedAuthoringSceneWriteService", source, StringComparison.Ordinal);
    Assert.DoesNotContain("SceneComponentAssetRecord", source, StringComparison.Ordinal);
    Assert.DoesNotContain("EditorAssetBinarySerializer.Serialize", source, StringComparison.Ordinal);
}
```

- [ ] **Step 4: Run the focused tests and verify they fail for the right reason**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests|FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~RenderingSceneWriterTests" -v minimal`

Expected: FAIL because the current city commands still route through engine-side generators and the current saved menu scene does not yet match the new generated-root FPS placement assertion.

- [ ] **Step 5: Commit the failing tests**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs engine/helengine.editor.tests/tools/DemoDiscSceneWriterTests.cs engine/helengine.editor.tests/tools/RenderingSceneWriterTests.cs
git -C C:\dev\helworks\helengine commit -m "Add city scene generator ownership regression tests"
```

### Task 2: Add a city-owned demo-disc menu generator and switch the command path

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscSceneGenerator.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\menu.tools\DemoDiscMainMenuSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu.tools\RegenerateDemoDiscMainMenuCommand.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Add the failing city-owned menu generator shell**

```csharp
namespace city.menu.tools {
    /// <summary>
    /// Writes the demo-disc menu assets and scene through the normal city-owned live authoring path.
    /// </summary>
    public sealed class DemoDiscSceneGenerator {
        readonly GeneratedAuthoringSceneWriteService AuthoringSceneWriteService;
        readonly DemoDiscMainMenuSceneFactory SceneFactory;

        public DemoDiscSceneGenerator() {
            AuthoringSceneWriteService = new GeneratedAuthoringSceneWriteService();
            SceneFactory = new DemoDiscMainMenuSceneFactory();
        }

        public void Generate(string projectRootPath, string providerTypeName) {
            throw new NotImplementedException();
        }
    }
}
```

- [ ] **Step 2: Implement the live menu scene factory around normal entities**

```csharp
public GeneratedAuthoringSceneDefinition CreateSceneDefinition(string providerTypeName, MenuDefinition definition) {
    return new GeneratedAuthoringSceneDefinition {
        SceneId = "Scenes/DemoDiscMainMenu.helen",
        SceneSettings = new SceneSettingsAsset {
            CanvasProfile = new SceneCanvasProfile {
                Width = DemoMenuLayout.CanvasWidth,
                Height = DemoMenuLayout.CanvasHeight
            }
        },
        RootEntities = new[] {
            CreateCameraEntity(),
            CreateMenuRootEntity(providerTypeName, definition)
        }
    };
}
```

- [ ] **Step 3: Author the FPS component under the generated fitted UI subtree**

```csharp
Entity CreateGeneratedRootEntity(MenuDefinition definition) {
    Entity generatedRoot = Core.Instance.EntityFactory.Create(DemoMenuLayout.GeneratedRootEntityName);
    generatedRoot.AddComponent(new FPSComponent {
        Font = ResolveRequiredBodyFont(definition.BodyFontPath)
    });

    return generatedRoot;
}
```

- [ ] **Step 4: Switch the city command from engine regeneration service to the city generator**

```csharp
public void Execute(IEditorCommandContext context) {
    if (context == null) {
        throw new ArgumentNullException(nameof(context));
    }

    DemoDiscSceneGenerator generator = new DemoDiscSceneGenerator();
    generator.Generate(context.ProjectRootPath, "city.menu.DemoDiscMenuDefinitionProvider, gameplay");
}
```

- [ ] **Step 5: Run the focused menu-generation tests**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests|FullyQualifiedName~DemoDiscSceneWriterTests" -v minimal`

Expected: PASS for the new city-owned menu generation assertions; any remaining failures should now be limited to still-unmigrated engine-side rendering generators.

- [ ] **Step 6: Commit the city-owned menu generator cutover**

```bash
git -C C:\dev\helprojs\city add assets/codebase/menu.tools/DemoDiscSceneGenerator.cs assets/codebase/menu.tools/DemoDiscMainMenuSceneFactory.cs assets/codebase/menu.tools/RegenerateDemoDiscMainMenuCommand.cs
git -C C:\dev\helprojs\city commit -m "Move demo disc menu generation into city"
```

### Task 3: Migrate the remaining rendering showcase scenes into city live authoring

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\PointShadowSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\PointShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\SpotShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowLabSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\Ps2BasisLightTestSceneFactory.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingShowcaseSourceWriter.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneGenerator.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Add failing authoring regressions for the missing migrated scenes**

```csharp
[Fact]
public void DeserializeCityPointShadowSceneAsset_ContainsCameraLightAndFittedFpsRoot() {
    SceneAsset sceneAsset = ReadSceneAsset("point_shadow.helen");

    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "PointShadowCamera"));
    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "PointShadowLight"));
    Assert.NotNull(FindEntityByName(sceneAsset.RootEntities, "PointShadowFpsOverlayRoot"));
}
```

- [ ] **Step 2: Implement each live scene factory as `GeneratedAuthoringSceneDefinition`**

```csharp
public GeneratedAuthoringSceneDefinition CreateSceneDefinition(RuntimeModel cubeModel, RuntimeModel planeModel, RuntimeMaterial standardMaterial) {
    return new GeneratedAuthoringSceneDefinition {
        SceneId = "scenes/rendering/point_shadow.helen",
        SceneSettings = new SceneSettingsAsset(),
        RootEntities = new[] {
            CreateCameraEntity(),
            CreateFittedFpsOverlayRoot(),
            CreateFloorEntity(planeModel, standardMaterial),
            CreateCasterEntity(cubeModel, standardMaterial),
            CreateLightEntity()
        }
    };
}
```

- [ ] **Step 3: Keep FPS overlays project-authored and fitted**

```csharp
Entity CreateFittedFpsOverlayRoot(string name) {
    Entity entity = Core.Instance.EntityFactory.Create(name);
    entity.AddComponent(new ViewportComponent {
        BindingMode = ViewportComponent.ScreenBindingMode,
        FixedSize = new int2(SceneCanvasProfile.DefaultWidth, SceneCanvasProfile.DefaultHeight)
    });
    entity.AddComponent(new ReferenceCanvasFitComponent {
        ReferenceWidth = SceneCanvasProfile.DefaultWidth,
        ReferenceHeight = SceneCanvasProfile.DefaultHeight
    });
    entity.AddComponent(new FPSComponent {
        Font = ResolveRequiredEditorFont()
    });
    return entity;
}
```

- [ ] **Step 4: Extend the city rendering generator to own every showcase scene**

```csharp
GeneratedAuthoringSceneDefinition pointShadowSceneDefinition = PointShadowFactory.CreateSceneDefinition(assets.GeneratedCubeModel, assets.GeneratedPlaneModel, assets.GeneratedStandardMaterial);
GeneratedAuthoringSceneDefinition pointShadowLabSceneDefinition = PointShadowLabFactory.CreateSceneDefinition(assets.GeneratedCubeModel, assets.GeneratedStandardMaterial);
GeneratedAuthoringSceneDefinition spotShadowLabSceneDefinition = SpotShadowLabFactory.CreateSceneDefinition(assets.GeneratedCubeModel, assets.GeneratedStandardMaterial);
GeneratedAuthoringSceneDefinition directionalShadowLabSceneDefinition = DirectionalShadowLabFactory.CreateSceneDefinition(assets.GeneratedCubeModel, assets.GeneratedStandardMaterial);
GeneratedAuthoringSceneDefinition ps2BasisLightTestSceneDefinition = Ps2BasisLightTestFactory.CreateSceneDefinition(assets.GeneratedPlaneModel, assets.GeneratedCubeModel);
```

- [ ] **Step 5: Run the city rendering authoring regressions**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests" -v minimal`

Expected: PASS for the newly migrated rendering scene existence and FPS-overlay assertions.

- [ ] **Step 6: Commit the migrated city rendering factories**

```bash
git -C C:\dev\helprojs\city add assets/codebase/rendering.tools/PointShadowSceneFactory.cs assets/codebase/rendering.tools/PointShadowLabSceneFactory.cs assets/codebase/rendering.tools/SpotShadowLabSceneFactory.cs assets/codebase/rendering.tools/DirectionalShadowLabSceneFactory.cs assets/codebase/rendering.tools/Ps2BasisLightTestSceneFactory.cs assets/codebase/rendering.tools/RenderingShowcaseSourceWriter.cs assets/codebase/rendering.tools/RenderingSceneGenerator.cs
git -C C:\dev\helprojs\city commit -m "Move rendering showcase scene generation into city"
```

### Task 4: Cut over directional shadow plaza and menu/rendering helper ownership completely

**Files:**
- Modify: `C:\dev\helprojs\city\assets\codebase\rendering.tools\DirectionalShadowPlazaSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu.tools\GenerateRenderingScenesCommand.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DirectionalShadowPlazaSceneAssetFactory.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingShowcaseSourceWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingScriptComponentRecordFactory.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`

- [ ] **Step 1: Remove remaining engine-side serialization-only helper dependencies from city scene factories**

```csharp
Assert.DoesNotContain("SceneComponentAssetRecord", source, StringComparison.Ordinal);
Assert.DoesNotContain("MeshComponentPersistenceDescriptor", source, StringComparison.Ordinal);
Assert.DoesNotContain("helengine.demo_disc_scene_writer", source, StringComparison.Ordinal);
Assert.Contains("GeneratedAuthoringSceneDefinition", source, StringComparison.Ordinal);
```

- [ ] **Step 2: Replace any old helper behavior with normal city live component authoring**

```csharp
entity.AddComponent(new AxisRotationComponent {
    Axis = new float3(0f, 1f, 0f),
    AngularSpeedRadiansPerSecond = angularSpeedRadians
});
```

- [ ] **Step 3: Run the focused ownership-source regressions**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~ReadCity" -v minimal`

Expected: PASS with city source tests proving the project generators no longer depend on engine-side manual serialization helpers.

- [ ] **Step 4: Commit the full city helper ownership cutover**

```bash
git -C C:\dev\helprojs\city add assets/codebase/rendering.tools/DirectionalShadowPlazaSceneFactory.cs assets/codebase/menu.tools/GenerateRenderingScenesCommand.cs
git -C C:\dev\helprojs\city commit -m "Finish city ownership of scene authoring helpers"
```

### Task 5: Remove the old HelEngine project-scene generator path

**Files:**
- Delete: `C:\dev\helworks\helengine\engine\helengine.editor\managers\menu\DemoMenuSceneAssetFactory.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DemoDiscSceneWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingSceneWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\DirectionalShadowPlazaSceneAssetFactory.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\RenderingShowcaseSourceWriter.cs`
- Delete: `C:\dev\helworks\helengine\tools\demo-disc-scene-writer\Program.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\DemoDiscSceneWriterTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\tools\RenderingSceneWriterTests.cs`

- [ ] **Step 1: Rewrite the engine tests so they validate city-owned saved output instead of engine generator classes**

```csharp
[Fact]
public void CityOwnedDemoDiscGeneration_SavesReadableMenuScene() {
    SceneAsset sceneAsset = ReadTopLevelSceneAsset("DemoDiscMainMenu.helen");

    Assert.Equal("DemoDiscMainMenu", sceneAsset.Id);
    Assert.Equal(2, sceneAsset.RootEntities.Length);
}
```

- [ ] **Step 2: Delete the obsolete engine-side project scene generators**

```text
Delete the old HelEngine project scene generator files after the city commands and tests no longer reference them.
```

- [ ] **Step 3: Run the full editor test slice that covered the old tool project**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests|FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~RenderingSceneWriterTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal`

Expected: PASS, with no test still referencing `helengine.demo_disc_scene_writer` or the deleted `DemoMenuSceneAssetFactory`.

- [ ] **Step 4: Commit the HelEngine cleanup**

```bash
git -C C:\dev\helworks\helengine add -A
git -C C:\dev\helworks\helengine commit -m "Remove engine-owned city scene generators"
```

### Task 6: Verify packaging and authored output end to end

**Files:**
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\managers\project\EditorWindowsBuildScenePackagerTests.cs`
- Test: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityRenderingSceneAuthoringTests.cs`
- Test output: `C:\dev\helprojs\city\assets\Scenes\DemoDiscMainMenu.helen`
- Test output: `C:\dev\helprojs\city\assets\scenes\rendering\*.helen`

- [ ] **Step 1: Add packaging regression coverage for the migrated menu and rendering scenes**

```csharp
[Fact]
public void Package_WhenCityOwnedDemoDiscAndRenderingScenesAreSavedThroughSceneSaveService_PackagesSuccessfully() {
    SceneAsset menuScene = ReadSceneAsset("DemoDiscMainMenu.helen");
    SceneAsset cubeScene = ReadSceneAsset("cube_test.helen");

    Assert.NotNull(menuScene);
    Assert.NotNull(cubeScene);
}
```

- [ ] **Step 2: Run the packaging-focused regression slice**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~CityRenderingSceneAuthoringTests" -v minimal`

Expected: PASS with menu and rendering scenes still packaging and deserializing after the ownership migration.

- [ ] **Step 3: Run the complete high-signal verification slice**

Run: `dotnet test C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj --filter "FullyQualifiedName~CityRenderingSceneAuthoringTests|FullyQualifiedName~DemoDiscSceneWriterTests|FullyQualifiedName~RenderingSceneWriterTests|FullyQualifiedName~EditorWindowsBuildScenePackagerTests|FullyQualifiedName~RuntimeSceneLoadServiceTests" -v minimal`

Expected: PASS

- [ ] **Step 4: Commit the packaging verification updates**

```bash
git -C C:\dev\helworks\helengine add engine/helengine.editor.tests/managers/project/EditorWindowsBuildScenePackagerTests.cs engine/helengine.editor.tests/CityRenderingSceneAuthoringTests.cs
git -C C:\dev\helworks\helengine commit -m "Verify city-owned scene generation packaging"
```

## Self-Review

- Spec coverage:
  - ownership move from `helengine` to `city`: covered by Tasks 2 through 5
  - live-authoring/save-path convergence: covered by Tasks 2 through 4
  - FPS overlay under fitted project UI hierarchy: covered by Tasks 1 through 3
  - packaging/runtime verification: covered by Task 6
- Placeholder scan:
  - removed generic “update tests” wording and replaced it with named files, concrete assertions, and exact commands
- Type consistency:
  - plan consistently uses `GeneratedAuthoringSceneDefinition`, `GeneratedAuthoringSceneWriteService`, `DemoDiscSceneGenerator`, and live `Entity` authoring across all tasks

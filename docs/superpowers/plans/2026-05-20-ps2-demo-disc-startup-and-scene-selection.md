# PS2 Demo Disc Startup And Scene Selection Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make PS2 builds use the Windows-style demo-disc startup flow for `city`, stage `GeneratedBootScene` and `DemoDiscMainMenu`, exclude DS scene redirects, and export the playable rendering-scene lineup.

**Architecture:** Reuse the editor’s existing startup-scene and generated boot-scene pipeline instead of adding a PS2-specific path. The code change is narrow: wire `ps2` into the same startup-scene behavior as `windows`, keep DS-only companion/remap logic isolated, then verify the resulting scene set through one environment-backed `city` build regression and one local deployment config sync.

**Tech Stack:** C#, xUnit, `helengine.editor` project build services, `EditorBuildQueueItemFactory`, `EditorGeneratedBootScenePreparationService`, local `city` project build configuration, editor CLI build entry point.

---

## File Map

### Editor startup-scene ordering
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemFactory.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`

### Generated boot-scene preparation
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs`

### Environment-backed city verification
- Create: `engine/helengine.editor.tests/managers/project/CityPs2DemoDiscBuildVerificationTests.cs`

### Local deployment setup
- Modify: `C:/dev/helprojs/city/user_settings/build_config.json`

## Task 1: Add PS2 startup-scene ordering to the queue-item factory

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorBuildQueueItemFactory.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs`

- [ ] **Step 1: Write the failing queue-item tests**

Extend `engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs` with two PS2-focused facts:

```csharp
[Fact]
public void Create_WhenPs2BuildOmitsGeneratedBootScene_InsertsItAsStartupScene() {
    WriteScene("Scenes/DemoDiscMainMenu.helen");
    WriteScene("Scenes/rendering/cube_test.helen");

    EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
    EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
    EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
        PlatformId = "ps2",
        SelectedSceneIds = [
            "DemoDiscMainMenu",
            "cube_test"
        ]
    };

    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreatePs2SelectionModel());
    EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

    Assert.Equal(new[] { "GeneratedBootScene", "DemoDiscMainMenu", "cube_test" }, queueItem.SelectedSceneIds);
}

[Fact]
public void Create_WhenPs2BuildSelectsSceneWithGeneratedCompanion_DoesNotIncludeNintendoDsCompanionScene() {
    WriteScene("Scenes/rendering/cube_test.helen");
    WriteScene("Scenes/rendering/ds/cube_test_ds.helen");

    EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(TempProjectRootPath);
    EditorBuildQueueItemFactory factory = new EditorBuildQueueItemFactory(sceneCatalogService);
    EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
        PlatformId = "ps2",
        SelectedSceneIds = [
            "DemoDiscMainMenu",
            "cube_test"
        ]
    };

    EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(CreatePs2SelectionModel());
    EditorBuildQueueItemDocument queueItem = factory.Create(platformConfig, selectionModel, Path.Combine(TempProjectRootPath, "Build"));

    Assert.Equal(new[] { "GeneratedBootScene", "DemoDiscMainMenu", "cube_test" }, queueItem.SelectedSceneIds);
    Assert.DoesNotContain("cube_test_ds", queueItem.SelectedSceneIds);
}
```

- [ ] **Step 2: Run the focused queue-item tests and confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected:
- the new PS2 startup-scene assertion fails because `ps2` currently does not insert `GeneratedBootScene`
- the DS companion exclusion test may pass already; keep it because it locks the branch boundary in place

- [ ] **Step 3: Implement PS2 startup-scene ordering**

Update `engine/helengine.editor/managers/project/EditorBuildQueueItemFactory.cs` so `ps2` shares the Windows startup-scene insertion behavior while leaving DS companion expansion untouched:

```csharp
const string Playstation2PlatformId = "ps2";
const string Playstation2StartupSceneId = PlatformMenuSceneResolver.GeneratedBootSceneId;

void ApplyPlatformStartupSceneOverrides(string platformId, List<string> orderedSceneIds) {
    if (orderedSceneIds == null) {
        throw new ArgumentNullException(nameof(orderedSceneIds));
    }

    if (string.Equals(platformId, WindowsPlatformId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(platformId, Playstation2PlatformId, StringComparison.OrdinalIgnoreCase)) {
        EnsureStartupSceneFirst(orderedSceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId);
        return;
    }

    if (!string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)) {
        return;
    }

    EnsureStartupSceneFirst(orderedSceneIds, NintendoDsStartupSceneId);
}
```

Keep `ApplyPlatformSceneExpansions` DS-only. Do not add any PS2 companion-scene discovery.

- [ ] **Step 4: Re-run the focused queue-item tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueItemFactoryTests" -v minimal
```

Expected:
- all `EditorBuildQueueItemFactoryTests` pass, including the new PS2 startup-scene case

- [ ] **Step 5: Commit the queue-item slice**

```bash
git add engine/helengine.editor/managers/project/EditorBuildQueueItemFactory.cs engine/helengine.editor.tests/managers/project/EditorBuildQueueItemFactoryTests.cs
git commit -m "Wire PS2 into generated startup scene ordering"
```

## Task 2: Make generated boot-scene preparation treat PS2 like Windows

**Files:**
- Modify: `engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs`
- Modify: `engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs`

- [ ] **Step 1: Write the failing boot-scene preparation tests**

Replace the current unsupported-platform PS2 assertion with explicit PS2 behavior coverage in `engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs`:

```csharp
[Fact]
public void EnsurePrepared_WhenPlatformIsPs2_WritesBootSceneWithoutMappings() {
    EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

    service.EnsurePrepared("ps2", [PlatformMenuSceneResolver.DesktopMainMenuSceneId]);

    string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
    Assert.True(File.Exists(scenePath));

    using FileStream stream = File.OpenRead(scenePath);
    SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
    SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
    SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));

    Assert.Equal(PlatformMenuSceneResolver.DesktopMainMenuSceneId, sceneMapComponent.InitialSceneId);
    Assert.Empty(sceneMapComponent.Mappings);
}

[Fact]
public void EnsurePrepared_WhenPlatformIsPs2_DoesNotWriteNintendoDsCompanionMappings() {
    EditorGeneratedBootScenePreparationService service = new EditorGeneratedBootScenePreparationService(ProjectRootPath);

    service.EnsurePrepared(
        "ps2",
        [
            PlatformMenuSceneResolver.DesktopMainMenuSceneId,
            "cube_test",
            "cube_test_ds"
        ]);

    string scenePath = Path.Combine(ProjectRootPath, "assets", "Scenes", PlatformMenuSceneResolver.GeneratedBootSceneId + ".helen");
    using FileStream stream = File.OpenRead(scenePath);
    SceneAsset sceneAsset = Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    SceneEntityAsset rootEntity = Assert.Single(sceneAsset.RootEntities);
    SceneMapComponentPersistenceDescriptor descriptor = new SceneMapComponentPersistenceDescriptor();
    SceneMapComponent sceneMapComponent = Assert.IsType<SceneMapComponent>(descriptor.DeserializeComponent(rootEntity.Components[0], null, null));

    Assert.Empty(sceneMapComponent.Mappings);
}
```

Keep one unsupported-platform test, but move it to a genuinely unsupported platform such as `"linux"` so the opt-in behavior still stays covered.

- [ ] **Step 2: Run the focused boot-scene tests and confirm they fail**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests" -v minimal
```

Expected:
- the new PS2 tests fail because `BuildMappings("ps2", ...)` currently returns `null`

- [ ] **Step 3: Implement PS2 boot-scene preparation**

Update `engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs` so `ps2` shares the Windows empty-mapping path:

```csharp
const string Playstation2PlatformId = "ps2";

static Dictionary<string, string> BuildMappings(string platformId, IReadOnlyList<string> sceneIds) {
    if (string.IsNullOrWhiteSpace(platformId)) {
        throw new ArgumentException("Platform id must be provided.", nameof(platformId));
    } else if (sceneIds == null) {
        throw new ArgumentNullException(nameof(sceneIds));
    }

    if (string.Equals(platformId, WindowsPlatformId, StringComparison.OrdinalIgnoreCase)
        || string.Equals(platformId, Playstation2PlatformId, StringComparison.OrdinalIgnoreCase)) {
        if (!ContainsSceneId(sceneIds, PlatformMenuSceneResolver.DesktopMainMenuSceneId)
            && !ContainsSceneId(sceneIds, PlatformMenuSceneResolver.GeneratedBootSceneId)) {
            return null;
        }

        return new Dictionary<string, string>(StringComparer.Ordinal);
    } else if (string.Equals(platformId, NintendoDsPlatformId, StringComparison.OrdinalIgnoreCase)) {
        // existing DS logic stays unchanged
    }

    return null;
}
```

Do not add PS2-specific remapping entries. Do not change `GeneratedBootSceneAssetFactory`.

- [ ] **Step 4: Re-run the focused boot-scene tests**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests" -v minimal
```

Expected:
- all generated boot-scene preparation tests pass

- [ ] **Step 5: Commit the boot-scene slice**

```bash
git add engine/helengine.editor/managers/project/EditorGeneratedBootScenePreparationService.cs engine/helengine.editor.tests/managers/project/EditorGeneratedBootScenePreparationServiceTests.cs
git commit -m "Treat PS2 boot scene generation like Windows"
```

## Task 3: Add an environment-backed city PS2 build verification

**Files:**
- Create: `engine/helengine.editor.tests/managers/project/CityPs2DemoDiscBuildVerificationTests.cs`

- [ ] **Step 1: Write the failing city verification test**

Create `engine/helengine.editor.tests/managers/project/CityPs2DemoDiscBuildVerificationTests.cs` with one environment-backed fact that copies the local `city` project into a temporary workspace, mirrors the Windows playable demo-disc lineup into the copied PS2 build config, runs the editor-owned scene-selection and cook pipeline, and asserts the resulting manifest is Windows-style instead of DS-style:

```csharp
namespace helengine.editor.tests.managers.project;

public sealed class CityPs2DemoDiscBuildVerificationTests {
    const string CityProjectRootPath = @"C:\dev\helprojs\city";

    [Fact]
    public void Cook_WhenCityPs2BuildUsesPlayableDemoDiscSceneSet_UsesGeneratedBootSceneAndExcludesDsSceneIds() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-city-ps2-build-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(workspaceRootPath, "project");
        string buildRootPath = Path.Combine(workspaceRootPath, "build");

        try {
            CopyDirectory(CityProjectRootPath, projectRootPath);
            ConfigurePs2BuildFromWindowsDemoDiscSelection(projectRootPath, buildRootPath);

            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(projectRootPath, "project.heproj"));
            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(bootstrap.BuildConfigService.TryLoadExisting(), "ps2");
            EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel("ps2");
            EditorBuildQueueItemDocument queueItem = new EditorBuildQueueItemFactory(bootstrap.SceneCatalogService).Create(platformConfig, selectionModel, buildRootPath);

            Assert.Equal("GeneratedBootScene", queueItem.SelectedSceneIds[0]);
            Assert.Contains("DemoDiscMainMenu", queueItem.SelectedSceneIds);
            Assert.Contains("spotlight_street_slice", queueItem.SelectedSceneIds);
            Assert.DoesNotContain(queueItem.SelectedSceneIds, sceneId => sceneId.EndsWith("_ds", StringComparison.Ordinal));

            EditorGeneratedBootScenePreparationService bootScenePreparationService = new EditorGeneratedBootScenePreparationService(projectRootPath);
            bootScenePreparationService.EnsurePrepared(queueItem.PlatformId, queueItem.SelectedSceneIds);

            EditorPlatformAssetCookService cookService = new(
                projectRootPath,
                bootstrap.RequiredEngineVersion,
                bootstrap.ProjectName,
                bootstrap.ProjectVersion,
                CreateDefaultImporters(),
                PackagedFontAssetFactory.Create());
            FakePs2PlatformBuilder builder = new FakePs2PlatformBuilder();

            PlatformBuildManifest manifest = cookService.Cook(
                builder.Definition,
                queueItem.SelectedSceneIds,
                buildRootPath,
                ["ps2"],
                builder,
                queueItem.SelectedBuildProfileId,
                queueItem.SelectedGraphicsProfileId);

            Assert.Equal("GeneratedBootScene", manifest.StartupSceneId);
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "GeneratedBootScene");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "DemoDiscMainMenu");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "cube_test");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "scaled_cube");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "colored_cube_grid");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "textured_cube_grid");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "axis_test");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "axis_test2");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "directional_shadow_plaza");
            Assert.Contains(manifest.Scenes, scene => scene.SceneId == "spotlight_street_slice");
            Assert.DoesNotContain(manifest.Scenes, scene => scene.SceneId.EndsWith("_ds", StringComparison.Ordinal));
        } finally {
            if (Directory.Exists(workspaceRootPath)) {
                Directory.Delete(workspaceRootPath, true);
            }
        }
    }
}
```

Add private helpers inside the same test class for:
- `ConfigurePs2BuildFromWindowsDemoDiscSelection(...)`
- `FindPlatformConfig(...)`
- `CopyDirectory(...)`
- `CreateDefaultImporters()`
- a nested `FakePs2PlatformBuilder : IPlatformAssetBuilder` copied from the existing build-graph runner test shape, with `PlatformId = "ps2"` and a minimal `ps2-standard-forward` graphics profile

- [ ] **Step 2: Run the new city verification test and confirm it fails**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~CityPs2DemoDiscBuildVerificationTests" -v minimal
```

Expected:
- the assertion on `GeneratedBootScene` fails before Task 1 and Task 2 are implemented

- [ ] **Step 3: Implement the copied-city PS2 build configuration helper**

Inside `ConfigurePs2BuildFromWindowsDemoDiscSelection(...)`, load `projectRootPath\user_settings\build_config.json`, copy the Windows scene list and scene orders into the PS2 platform entry, and replace the output path with the temporary build root:

```csharp
static void ConfigurePs2BuildFromWindowsDemoDiscSelection(string projectRootPath, string buildRootPath) {
    EditorBuildConfigService buildConfigService = new EditorBuildConfigService(projectRootPath);
    EditorBuildConfigDocument buildConfig = buildConfigService.TryLoadExisting()
        ?? throw new InvalidOperationException("City build configuration was not found.");

    EditorBuildPlatformConfigDocument windowsPlatform = FindPlatformConfig(buildConfig, "windows");
    EditorBuildPlatformConfigDocument ps2Platform = FindPlatformConfig(buildConfig, "ps2");

    ps2Platform.SelectedSceneIds = windowsPlatform.SelectedSceneIds.ToList();
    ps2Platform.SceneOrders = windowsPlatform.SceneOrders
        .Select(sceneOrder => new EditorBuildSceneOrderDocument {
            SceneId = sceneOrder.SceneId,
            OrderNumber = sceneOrder.OrderNumber
        })
        .ToList();
    ps2Platform.OutputDirectoryPath = buildRootPath.Replace('\\', '/');
    ps2Platform.SelectedBuildProfileId = "ps2-default";
    ps2Platform.SelectedGraphicsProfileId = "ps2-standard-forward";
    ps2Platform.SelectedStorageProfileId = "disc-layout";
    ps2Platform.SelectedMediaProfileId = "ps2-install-tree";

    buildConfigService.Save(buildConfig);
}
```

The helper is part of the test file, not production code.

- [ ] **Step 3a: Implement the real importer helper used by the copied-city cook**

Add a private importer helper in the same test file that mirrors the host defaults closely enough for the copied `city` project:

```csharp
static IReadOnlyList<IAssetImporterRegistration> CreateDefaultImporters() {
    string[] modelExtensions = [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x"];
    string[] fontExtensions = [".ttf", ".otf"];
    List<IAssetImporterRegistration> registrations = new List<IAssetImporterRegistration>(EditorHostTextureImporterFactory.CreateDefault()) {
        new TextImporterRegistration("text", new TextImporter(), [".txt"]),
        new FontImporterRegistration("gdi-font", new GdiFontImporter(), fontExtensions),
        new ModelImporterRegistration(
            "assimp",
            new LazyModelImporter(new AssemblyModelImporterFactory("helengine.editor.assimp", "helengine.editor.assimp.HelengineAssimpImporter")),
            modelExtensions)
    };

    return registrations;
}
```

This keeps the verification aligned with the real editor cook path instead of using toy importers that may reject `city` assets for unrelated reasons.

- [ ] **Step 4: Re-run the city verification plus the focused editor slices**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueItemFactoryTests|FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests|FullyQualifiedName~CityPs2DemoDiscBuildVerificationTests" -v minimal
```

Expected:
- all three focused suites pass

- [ ] **Step 5: Commit the verification slice**

```bash
git add engine/helengine.editor.tests/managers/project/CityPs2DemoDiscBuildVerificationTests.cs
git commit -m "Add city PS2 demo disc build verification"
```

## Task 4: Sync the local city PS2 build config and export the deployment

**Files:**
- Modify: `C:/dev/helprojs/city/user_settings/build_config.json`

- [ ] **Step 1: Mirror the Windows playable scene lineup into the local PS2 build config**

Update the `ps2` entry in `C:/dev/helprojs/city/user_settings/build_config.json` so it matches the Windows demo-disc scene list and ordering:

```json
"selectedSceneIds": [
  "DemoDiscMainMenu",
  "cube_test",
  "scaled_cube",
  "colored_cube_grid",
  "textured_cube_grid",
  "axis_test",
  "axis_test2",
  "directional_shadow_plaza",
  "spotlight_street_slice"
]
```

Use the Windows `sceneOrders` values as the source of truth, then keep the PS2 output directory pointed at a dedicated export root such as:

```json
"outputDirectoryPath": "C:/dev/helprojs/output/ps2-city-demo-disc"
```

Do not add any `_ds` scene ids to the local PS2 selection.

- [ ] **Step 2: Run the focused test slice before the export**

Run:

```bash
rtk dotnet test engine/helengine.editor.tests/helengine.editor.tests.csproj --filter "FullyQualifiedName~EditorBuildQueueItemFactoryTests|FullyQualifiedName~EditorGeneratedBootScenePreparationServiceTests|FullyQualifiedName~CityPs2DemoDiscBuildVerificationTests" -v minimal
```

Expected:
- all focused PS2 startup/boot-scene/build-verification tests pass

- [ ] **Step 3: Export the PS2 city demo disc build through the editor CLI**

Run:

```bash
rtk dotnet run --project helengine.ui/helengine.editor.app/helengine.editor.app.csproj -- --project C:\dev\helprojs\city\project.heproj --build ps2 --output C:\dev\helprojs\output\ps2-city-demo-disc
```

Expected:
- the editor CLI exits successfully
- `C:\dev\helprojs\output\ps2-city-demo-disc` contains the fresh PS2 export

- [ ] **Step 4: Verify the generated startup flow in the local export inputs**

Run:

```bash
rtk powershell -Command "Test-Path 'C:\dev\helprojs\city\assets\Scenes\GeneratedBootScene.helen'; Test-Path 'C:\dev\helprojs\output\ps2-city-demo-disc' | Out-String -Width 4000"
```

Expected:
- the generated boot scene file exists after the export
- the output directory exists after the export
- the focused test slice from Step 2 remains the source of truth for the no-`_ds` assertion

- [ ] **Step 5: No commit for the local deployment settings**

Do not commit `C:/dev/helprojs/city/user_settings/build_config.json`. It is a local deployment configuration file, not part of the engine source change set.

# Zombislayer Demo Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Zombislayer as a Windows-playable second game on the city demo disc, with direct gameplay launch, FPS movement, a camera-attached M4 viewmodel, and an FSM-driven pause menu.

**Architecture:** Keep the slice aligned with the existing city generated-scene workflow. Add one narrow Zombislayer runtime layer under `assets/codebase/game`, one narrow asset/staging + scene-generation layer under `assets/codebase/game.tools`, and wire it into the existing demo-disc game catalog plus Windows build config. Use city-local xUnit projects for gameplay/source tests and engine-side source-audit tests for integration seams.

**Tech Stack:** C#/.NET 9, xUnit, helengine editor CLI, city generated game-scene pipeline, Windows loose-file build, legacy `.X` imported models.

---

## File Map

### Runtime gameplay files

- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSceneIds.cs`
  Purpose: stable scene ids for the Zombislayer slice.
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionState.cs`
  Purpose: FSM state enum for `Playing` and `Paused`.
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionComponent.cs`
  Purpose: owns pause-state transitions, overlay visibility, cursor state, and return-to-menu flow.
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerFpsControllerComponent.cs`
  Purpose: Windows `WASD` movement, mouse look, pitch clamp, and paused-input suppression.

### Generated scene + asset files

- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetCatalog.cs`
  Purpose: centralizes project-relative imported asset paths.
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerGenerationAssets.cs`
  Purpose: bundles prepared runtime imported models for scene generation.
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetPreparationService.cs`
  Purpose: loads imported runtime models from the city project assets.
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerSceneFactory.cs`
  Purpose: emits the generated authored gameplay scene.
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs`
  Purpose: exports the Zombislayer generated scene id.
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs`
  Purpose: stages assets and writes the Zombislayer scene.

### Menu + build integration files

- Modify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs`
  Purpose: adds the Zombislayer `Games` entry.
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json`
  Purpose: includes the Zombislayer scene in Windows selected scenes and ordering so the item is visible in Windows builds only.

### Source asset files

- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\level.X`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\level.nm`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\Barricade_LPDiffuseMap.png`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\H2.png`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\road.png`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\st-metal.png`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\TS7.png`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\m4a1.X`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\mag.X`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\vs-bg-digitalcamo.png`
- Modify/generated: `C:\dev\helprojs\city\assets\scenes\games\zombislayer.helen`

### Test files

- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityGameSceneSourceTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerAssetPreparationSourceTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerSceneGenerationSourceTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerBuildConfigTests.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\GameSceneCatalogSourceTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerSessionComponentTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerFpsControllerComponentTests.cs`

---

### Task 1: Add Zombislayer scene ids and demo-disc catalog wiring

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSceneIds.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityGameSceneSourceTests.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\GameSceneCatalogSourceTests.cs`

- [ ] **Step 1: Write the failing integration/source tests**

Add these tests first.

In `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityGameSceneSourceTests.cs`:

```csharp
[Fact]
public void City_demo_disc_menu_source_exposes_zombislayer_games_entry() {
    string sceneCatalogPath = @"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs";
    string source = File.ReadAllText(sceneCatalogPath);

    Assert.Contains("\"games-zombislayer\"", source, StringComparison.Ordinal);
    Assert.Contains("\"Zombislayer\"", source, StringComparison.Ordinal);
    Assert.Contains("global::city.game.ZombislayerSceneIds.GameplaySceneId", source, StringComparison.Ordinal);
}

[Fact]
public void City_game_scene_catalog_source_exports_zombislayer_scene() {
    string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs";
    string source = File.ReadAllText(sourcePath);

    Assert.Contains("public const string ZombislayerSceneId = global::city.game.ZombislayerSceneIds.GameplaySceneId;", source, StringComparison.Ordinal);
    Assert.Contains("ZombislayerSceneId,", source, StringComparison.Ordinal);
}
```

In `C:\dev\helprojs\city\assets\codebase\game.tools.tests\GameSceneCatalogSourceTests.cs`:

```csharp
[Fact]
public void Scene_catalog_reuses_runtime_zombislayer_scene_id() {
    string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs");

    Assert.Contains("global::city.game.ZombislayerSceneIds.GameplaySceneId", source, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~City_demo_disc_menu_source_exposes_zombislayer_games_entry|FullyQualifiedName~City_game_scene_catalog_source_exports_zombislayer_scene'"
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~Scene_catalog_reuses_runtime_zombislayer_scene_id'"
```

Expected: failures because the Zombislayer scene id type and catalog/menu entry do not exist yet.

- [ ] **Step 3: Implement the ids and catalog wiring**

Create `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSceneIds.cs`:

```csharp
namespace city.game {
    /// <summary>
    /// Stores the shared runtime scene ids used by the Zombislayer gameplay slice.
    /// </summary>
    public static class ZombislayerSceneIds {
        /// <summary>
        /// Stable scene id used by the generated Zombislayer gameplay scene.
        /// </summary>
        public const string GameplaySceneId = "scenes/games/zombislayer.helen";
    }
}
```

Modify `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs`:

```csharp
public const string ZombislayerSceneId = global::city.game.ZombislayerSceneIds.GameplaySceneId;

public static IReadOnlyList<string> GetSceneIds() {
    return [
        TiltTrialSceneId,
        TiltTrialLevel01SceneId,
        TiltTrialLevel02SceneId,
        TiltTrialLevel03SceneId,
        TiltTrialLevel04SceneId,
        TiltTrialLevel05SceneId,
        ZombislayerSceneId,
    ];
}
```

Modify `C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs`:

```csharp
public IReadOnlyList<DemoDiscGameSceneEntry> CreateGameSceneEntries() {
    return [
        new DemoDiscGameSceneEntry(
            "games-tilt-trial",
            "Tilt Trial",
            "tilt_trial"),
        new DemoDiscGameSceneEntry(
            "games-zombislayer",
            "Zombislayer",
            global::city.game.ZombislayerSceneIds.GameplaySceneId)
    ];
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~City_demo_disc_menu_source_exposes_zombislayer_games_entry|FullyQualifiedName~City_game_scene_catalog_source_exports_zombislayer_scene'"
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~Scene_catalog_reuses_runtime_zombislayer_scene_id'"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helprojs\city add assets\codebase\game\ZombislayerSceneIds.cs assets\codebase\game.tools\GameSceneCatalog.cs assets\codebase\menu\DemoDiscSceneCatalog.cs assets\codebase\game.tools.tests\GameSceneCatalogSourceTests.cs
git -C C:\dev\helworks\helengine add engine\helengine.editor.tests\CityGameSceneSourceTests.cs
git -C C:\dev\helprojs\city commit -m "feat: add zombislayer scene ids and menu entry"
git -C C:\dev\helworks\helengine commit -m "test: cover zombislayer city source wiring"
```

### Task 2: Stage the legacy source assets and add imported-model preparation

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetCatalog.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerGenerationAssets.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetPreparationService.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerAssetPreparationSourceTests.cs`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\level\*`
- Create: `C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\*`

- [ ] **Step 1: Write the failing source tests for the asset-preparation seam**

Create `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerAssetPreparationSourceTests.cs`:

```csharp
namespace city.tests {
    /// <summary>
    /// Verifies the Zombislayer asset preparation path uses imported level and weapon source assets from the city project tree.
    /// </summary>
    public sealed class ZombislayerAssetPreparationSourceTests {
        [Fact]
        public void Zombislayer_asset_catalog_uses_expected_project_relative_model_paths() {
            string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetCatalog.cs");

            Assert.Contains("public const string EnvironmentModelRelativePath = \"models/games/zombislayer/level/level.X\";", source, StringComparison.Ordinal);
            Assert.Contains("public const string WeaponModelRelativePath = \"models/games/zombislayer/weapons/m4a1.X\";", source, StringComparison.Ordinal);
        }

        [Fact]
        public void Zombislayer_asset_preparation_service_loads_both_imported_runtime_models() {
            string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetPreparationService.cs");

            Assert.Contains("LoadImportedModelRuntime(projectRootPath, ZombislayerAssetCatalog.EnvironmentModelRelativePath)", source, StringComparison.Ordinal);
            Assert.Contains("LoadImportedModelRuntime(projectRootPath, ZombislayerAssetCatalog.WeaponModelRelativePath)", source, StringComparison.Ordinal);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerAssetPreparationSourceTests'"
```

Expected: FAIL because the new asset catalog and preparation service do not exist.

- [ ] **Step 3: Copy the legacy source assets and implement the preparation classes**

Copy the source files into the city project:

```powershell
New-Item -ItemType Directory -Force -Path 'C:\dev\helprojs\city\assets\models\games\zombislayer\level' | Out-Null
New-Item -ItemType Directory -Force -Path 'C:\dev\helprojs\city\assets\models\games\zombislayer\weapons' | Out-Null

Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\level.X' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\level.X' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\level.nm' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\level.nm' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\Barricade_LPDiffuseMap.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\Barricade_LPDiffuseMap.png' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\H2.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\H2.png' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\road.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\road.png' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\st-metal.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\st-metal.png' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\level\TS7.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\level\TS7.png' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\weapons\m4a1.X' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\m4a1.X' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\weapons\mag.X' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\mag.X' -Force
Copy-Item -LiteralPath 'C:\Users\Helena\Downloads\Cinetica Games\Zombislayer\Zombislayer\ZombislayerContent\models\weapons\vs-bg-digitalcamo.png' -Destination 'C:\dev\helprojs\city\assets\models\games\zombislayer\weapons\vs-bg-digitalcamo.png' -Force
```

Create `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetCatalog.cs`:

```csharp
namespace city.game.tools {
    /// <summary>
    /// Centralizes Zombislayer source asset ids and project-relative imported-model paths.
    /// </summary>
    public static class ZombislayerAssetCatalog {
        public const string EnvironmentModelRelativePath = "models/games/zombislayer/level/level.X";
        public const string WeaponModelRelativePath = "models/games/zombislayer/weapons/m4a1.X";
    }
}
```

Create `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerGenerationAssets.cs`:

```csharp
namespace city.game.tools {
    /// <summary>
    /// Bundles the imported runtime models required to compose the first Zombislayer gameplay slice.
    /// </summary>
    public sealed class ZombislayerGenerationAssets {
        public RuntimeModel EnvironmentModel { get; set; }
        public RuntimeModel WeaponModel { get; set; }
    }
}
```

Create `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerAssetPreparationService.cs`:

```csharp
using helengine.editor;
using System.Reflection;

namespace city.game.tools {
    /// <summary>
    /// Loads the imported runtime assets required by the generated Zombislayer gameplay slice.
    /// </summary>
    public sealed class ZombislayerAssetPreparationService {
        public ZombislayerGenerationAssets Prepare(string projectRootPath) {
            if (string.IsNullOrWhiteSpace(projectRootPath)) {
                throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
            }

            return new ZombislayerGenerationAssets {
                EnvironmentModel = LoadImportedModelRuntime(projectRootPath, ZombislayerAssetCatalog.EnvironmentModelRelativePath),
                WeaponModel = LoadImportedModelRuntime(projectRootPath, ZombislayerAssetCatalog.WeaponModelRelativePath)
            };
        }

        RuntimeModel LoadImportedModelRuntime(string projectRootPath, string relativeSourcePath) {
            string fullProjectRootPath = Path.GetFullPath(projectRootPath);
            string assetsRootPath = Path.Combine(fullProjectRootPath, "assets");
            AssetImportManager importManager = CreateAssetImportManager(fullProjectRootPath, assetsRootPath);
            EditorFileSystemModelResolver modelResolver = new EditorFileSystemModelResolver(importManager);
            string fullSourcePath = Path.GetFullPath(Path.Combine(assetsRootPath, relativeSourcePath.Replace('/', Path.DirectorySeparatorChar)));
            return modelResolver.ResolveRuntimeModel(fullSourcePath);
        }

        AssetImportManager CreateAssetImportManager(string projectRootPath, string assetsRootPath) {
            ContentManager contentManager = new ContentManager(new HostFileSystemContentStreamSource(assetsRootPath));
            AssetImportManager importManager = new AssetImportManager(projectRootPath, contentManager);
            IReadOnlyList<IAssetImporterRegistration> importers = CreateDefaultImporters();
            for (int index = 0; index < importers.Count; index++) {
                importers[index].Register(importManager);
            }

            importManager.GenerateMissingImportSettings();
            return importManager;
        }

        IReadOnlyList<IAssetImporterRegistration> CreateDefaultImporters() {
            Assembly appAssembly = Assembly.Load("helengine.editor.app");
            Type importerFactoryType = appAssembly.GetType("helengine.editor.app.EditorHostImporterFactory", throwOnError: true);
            MethodInfo createDefaultMethod = importerFactoryType.GetMethod("CreateDefault", BindingFlags.Public | BindingFlags.Static)
                ?? throw new InvalidOperationException("EditorHostImporterFactory.CreateDefault was not found.");
            return (IReadOnlyList<IAssetImporterRegistration>)createDefaultMethod.Invoke(null, Array.Empty<object>());
        }
    }
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerAssetPreparationSourceTests'"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helprojs\city add assets\codebase\game.tools\ZombislayerAssetCatalog.cs assets\codebase\game.tools\ZombislayerGenerationAssets.cs assets\codebase\game.tools\ZombislayerAssetPreparationService.cs assets\codebase\game.tools.tests\ZombislayerAssetPreparationSourceTests.cs assets\models\games\zombislayer
git -C C:\dev\helprojs\city commit -m "feat: stage zombislayer source assets"
```

### Task 3: Add the FSM-backed session component and the FPS controller

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionState.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionComponent.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game\ZombislayerFpsControllerComponent.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerSessionComponentTests.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerFpsControllerComponentTests.cs`

- [ ] **Step 1: Write the failing gameplay tests**

Create `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerSessionComponentTests.cs`:

```csharp
namespace city.tests {
    /// <summary>
    /// Verifies the Zombislayer session controller exposes stable pause-state behavior.
    /// </summary>
    public sealed class ZombislayerSessionComponentTests {
        [Fact]
        public void Create_state_machine_registers_playing_and_paused_states() {
            helengine.FiniteStateMachine<city.game.ZombislayerSessionState> machine = city.game.ZombislayerSessionComponent.CreateStateMachine();

            machine.Initialize(city.game.ZombislayerSessionState.Playing);
            bool changed = machine.TryChangeState(city.game.ZombislayerSessionState.Paused);

            Assert.True(changed);
            Assert.Equal(city.game.ZombislayerSessionState.Paused, machine.CurrentState);
        }

        [Fact]
        public void Toggle_pause_from_playing_returns_paused() {
            city.game.ZombislayerSessionState nextState = city.game.ZombislayerSessionComponent.ResolveStateAfterPauseToggle(city.game.ZombislayerSessionState.Playing);

            Assert.Equal(city.game.ZombislayerSessionState.Paused, nextState);
        }

        [Fact]
        public void Should_show_pause_overlay_returns_true_only_for_paused() {
            Assert.False(city.game.ZombislayerSessionComponent.ShouldShowPauseOverlay(city.game.ZombislayerSessionState.Playing));
            Assert.True(city.game.ZombislayerSessionComponent.ShouldShowPauseOverlay(city.game.ZombislayerSessionState.Paused));
        }
    }
}
```

Create `C:\dev\helprojs\city\assets\codebase\gameplay.tests\ZombislayerFpsControllerComponentTests.cs`:

```csharp
namespace city.tests {
    /// <summary>
    /// Verifies the Zombislayer FPS controller preserves grounded planar movement and clamped pitch.
    /// </summary>
    public sealed class ZombislayerFpsControllerComponentTests {
        [Fact]
        public void Clamp_pitch_degrees_limits_to_minus_eighty_and_plus_eighty() {
            Assert.Equal(-80f, city.game.ZombislayerFpsControllerComponent.ClampPitchDegrees(-120f));
            Assert.Equal(80f, city.game.ZombislayerFpsControllerComponent.ClampPitchDegrees(120f));
        }

        [Fact]
        public void Build_planar_move_direction_uses_yaw_relative_axes() {
            helengine.float3 direction = city.game.ZombislayerFpsControllerComponent.BuildPlanarMoveDirection(0f, 1f, 0f);

            Assert.True(direction.Z > 0.9f);
            Assert.Equal(0f, direction.Y);
        }
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerSessionComponentTests|FullyQualifiedName~ZombislayerFpsControllerComponentTests'"
```

Expected: FAIL because the runtime types do not exist.

- [ ] **Step 3: Implement the runtime types**

Create `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionState.cs`:

```csharp
namespace city.game {
    /// <summary>
    /// Stores the high-level session states used by the first Zombislayer gameplay slice.
    /// </summary>
    public enum ZombislayerSessionState {
        Playing,
        Paused
    }
}
```

Create `C:\dev\helprojs\city\assets\codebase\game\ZombislayerSessionComponent.cs`:

```csharp
using city.menu;
using helengine;

namespace city.game {
    /// <summary>
    /// Owns pause-state transitions, overlay visibility, and return-to-demo-disc flow for the first Zombislayer gameplay slice.
    /// </summary>
    public sealed class ZombislayerSessionComponent : UpdateComponent {
        readonly FiniteStateMachine<ZombislayerSessionState> SessionStateMachine;
        Entity PauseOverlayEntity;
        bool IsInitialized;

        public ZombislayerSessionComponent() {
            SessionStateMachine = CreateStateMachine();
        }

        public static FiniteStateMachine<ZombislayerSessionState> CreateStateMachine() {
            FiniteStateMachine<ZombislayerSessionState> machine = new FiniteStateMachine<ZombislayerSessionState>();
            machine.RegisterState(ZombislayerSessionState.Playing, new FiniteStateDefinition<ZombislayerSessionState>());
            machine.RegisterState(ZombislayerSessionState.Paused, new FiniteStateDefinition<ZombislayerSessionState>());
            return machine;
        }

        public static ZombislayerSessionState ResolveStateAfterPauseToggle(ZombislayerSessionState currentState) {
            return currentState == ZombislayerSessionState.Playing
                ? ZombislayerSessionState.Paused
                : ZombislayerSessionState.Playing;
        }

        public static bool ShouldShowPauseOverlay(ZombislayerSessionState state) {
            return state == ZombislayerSessionState.Paused;
        }

        public override void Update() {
            if (!IsInitialized) {
                SessionStateMachine.Initialize(ZombislayerSessionState.Playing);
                PauseOverlayEntity = FindRequiredChildEntity("ZombislayerPauseOverlay");
                IsInitialized = true;
            }

            if (Core.Instance.Input.WasKeyPressed(Keys.Escape)) {
                SessionStateMachine.TryChangeState(ResolveStateAfterPauseToggle(SessionStateMachine.CurrentState));
            }

            PauseOverlayEntity.Enabled = ShouldShowPauseOverlay(SessionStateMachine.CurrentState);
            if (SessionStateMachine.CurrentState == ZombislayerSessionState.Paused && Core.Instance.Input.WasKeyPressed(Keys.Enter)) {
                SessionStateMachine.TryChangeState(ZombislayerSessionState.Playing);
            } else if (SessionStateMachine.CurrentState == ZombislayerSessionState.Paused && Core.Instance.Input.WasKeyPressed(Keys.Back)) {
                string resolvedSceneId = DemoDiscMainMenuSceneResolver.ResolveRuntimeSceneId();
                Core.Instance.SceneManager.LoadScene(resolvedSceneId, SceneLoadMode.Single);
            }
        }

        Entity FindRequiredChildEntity(string name) {
            for (int index = 0; index < Parent.Children.Count; index++) {
                if (Parent.Children[index] is Entity child && string.Equals(child.Name, name, StringComparison.Ordinal)) {
                    return child;
                }
            }

            throw new InvalidOperationException($"Zombislayer session could not resolve required child '{name}'.");
        }
    }
}
```

Create `C:\dev\helprojs\city\assets\codebase\game\ZombislayerFpsControllerComponent.cs`:

```csharp
using helengine;

namespace city.game {
    /// <summary>
    /// Drives one simple Windows-first FPS rig for the first Zombislayer gameplay slice.
    /// </summary>
    public sealed class ZombislayerFpsControllerComponent : UpdateComponent {
        const float MaximumPitchDegrees = 80f;
        const float MinimumPitchDegrees = -80f;

        public Entity CameraPivot { get; set; }
        public float MoveSpeedUnitsPerSecond { get; set; } = 5f;
        public float LookSensitivityDegrees { get; set; } = 0.12f;
        float PitchDegrees;

        public static float ClampPitchDegrees(float pitchDegrees) {
            return Math.Clamp(pitchDegrees, MinimumPitchDegrees, MaximumPitchDegrees);
        }

        public static float3 BuildPlanarMoveDirection(float yawRadians, float forwardAmount, float rightAmount) {
            float3 forward = new float3((float)Math.Sin(yawRadians), 0f, (float)Math.Cos(yawRadians));
            float3 right = new float3(forward.Z, 0f, -forward.X);
            float3 direction = (forward * forwardAmount) + (right * rightAmount);
            if (direction.LengthSquared() > 0f) {
                direction.Normalize();
            }

            return direction;
        }

        public override void Update() {
            if (Parent == null || CameraPivot == null) {
                throw new InvalidOperationException("Zombislayer FPS controller requires a parent entity and camera pivot.");
            }

            InputSystem input = Core.Instance.Input;
            float forwardAmount = (input.IsKeyDown(Keys.W) ? 1f : 0f) - (input.IsKeyDown(Keys.S) ? 1f : 0f);
            float rightAmount = (input.IsKeyDown(Keys.D) ? 1f : 0f) - (input.IsKeyDown(Keys.A) ? 1f : 0f);
            float yawRadians = Parent.LocalOrientation.ToYaw();
            float3 direction = BuildPlanarMoveDirection(yawRadians, forwardAmount, rightAmount);
            Parent.LocalPosition += direction * (MoveSpeedUnitsPerSecond * (float)Core.Instance.FrameDeltaSeconds);

            PitchDegrees = ClampPitchDegrees(PitchDegrees - (input.MouseDelta.Y * LookSensitivityDegrees));
        }
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerSessionComponentTests|FullyQualifiedName~ZombislayerFpsControllerComponentTests'"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helprojs\city add assets\codebase\game\ZombislayerSessionState.cs assets\codebase\game\ZombislayerSessionComponent.cs assets\codebase\game\ZombislayerFpsControllerComponent.cs assets\codebase\gameplay.tests\ZombislayerSessionComponentTests.cs assets\codebase\gameplay.tests\ZombislayerFpsControllerComponentTests.cs
git -C C:\dev\helprojs\city commit -m "feat: add zombislayer session and fps controller"
```

### Task 4: Generate the Zombislayer gameplay scene

**Files:**
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerSceneFactory.cs`
- Modify: `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerSceneGenerationSourceTests.cs`
- Modify: `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityGameSceneSourceTests.cs`

- [ ] **Step 1: Write the failing scene-generation source tests**

Create `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerSceneGenerationSourceTests.cs`:

```csharp
namespace city.tests {
    /// <summary>
    /// Verifies the generated Zombislayer scene path is wired into the city game-scene generator.
    /// </summary>
    public sealed class ZombislayerSceneGenerationSourceTests {
        [Fact]
        public void Game_scene_generator_writes_zombislayer_scene() {
            string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs");

            Assert.Contains("ZombislayerAssetPreparationService", source, StringComparison.Ordinal);
            Assert.Contains("ZombislayerSceneFactory", source, StringComparison.Ordinal);
            Assert.Contains("sceneWriteService.WriteScene(projectRootPath, zombislayerScene);", source, StringComparison.Ordinal);
        }

        [Fact]
        public void Zombislayer_scene_factory_authors_environment_player_camera_viewmodel_and_pause_overlay() {
            string source = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerSceneFactory.cs");

            Assert.Contains("new city.game.ZombislayerSessionComponent()", source, StringComparison.Ordinal);
            Assert.Contains("new city.game.ZombislayerFpsControllerComponent()", source, StringComparison.Ordinal);
            Assert.Contains("\"ZombislayerWeapon\"", source, StringComparison.Ordinal);
            Assert.Contains("\"ZombislayerPauseOverlay\"", source, StringComparison.Ordinal);
            Assert.Contains("SceneAssetReferenceFactory.CreateFileSystemModel(ZombislayerAssetCatalog.EnvironmentModelRelativePath)", source, StringComparison.Ordinal);
            Assert.Contains("SceneAssetReferenceFactory.CreateFileSystemModel(ZombislayerAssetCatalog.WeaponModelRelativePath)", source, StringComparison.Ordinal);
        }
    }
}
```

Add this test to `C:\dev\helworks\helengine\engine\helengine.editor.tests\CityGameSceneSourceTests.cs`:

```csharp
[Fact]
public void City_zombislayer_source_uses_generated_scene_factory_and_runtime_components() {
    string generatorSource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs");
    string factorySource = File.ReadAllText(@"C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerSceneFactory.cs");

    Assert.Contains("ZombislayerAssetPreparationService", generatorSource, StringComparison.Ordinal);
    Assert.Contains("CreateGameplayScene()", factorySource, StringComparison.Ordinal);
    Assert.Contains("new city.game.ZombislayerSessionComponent()", factorySource, StringComparison.Ordinal);
    Assert.Contains("new city.game.ZombislayerFpsControllerComponent()", factorySource, StringComparison.Ordinal);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerSceneGenerationSourceTests'"
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~City_zombislayer_source_uses_generated_scene_factory_and_runtime_components'"
```

Expected: FAIL because the Zombislayer scene factory and generator wiring do not exist.

- [ ] **Step 3: Implement the generated-scene path**

Create `C:\dev\helprojs\city\assets\codebase\game.tools\ZombislayerSceneFactory.cs`:

```csharp
using helengine.editor;

namespace city.game.tools {
    /// <summary>
    /// Builds the generated authored Zombislayer gameplay scene contributed by the city demo-disc project.
    /// </summary>
    public sealed class ZombislayerSceneFactory {
        readonly RuntimeModel EnvironmentModel;
        readonly RuntimeModel WeaponModel;

        public ZombislayerSceneFactory(ZombislayerGenerationAssets assets) {
            if (assets == null || assets.EnvironmentModel == null || assets.WeaponModel == null) {
                throw new ArgumentException("Zombislayer scene generation requires prepared environment and weapon models.", nameof(assets));
            }

            EnvironmentModel = assets.EnvironmentModel;
            WeaponModel = assets.WeaponModel;
        }

        public GeneratedAuthoringSceneDefinition CreateGameplayScene() {
            EditorEntity playerRoot = CreatePlayerRootEntity();
            EditorEntity cameraPivot = CreateCameraPivotEntity();
            EditorEntity cameraEntity = CreateCameraEntity();
            EditorEntity weaponEntity = CreateWeaponEntity();

            cameraPivot.AddChild(cameraEntity);
            cameraPivot.AddChild(weaponEntity);
            playerRoot.AddChild(cameraPivot);

            return new GeneratedAuthoringSceneDefinition {
                SceneId = global::city.game.ZombislayerSceneIds.GameplaySceneId,
                SceneSettings = new SceneSettingsAsset(),
                RootEntities = [
                    CreateEnvironmentEntity(),
                    CreateSunEntity(),
                    playerRoot,
                    CreateUiRootEntity()
                ]
            };
        }

        EditorEntity CreateEnvironmentEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerEnvironment");
            MeshComponent meshComponent = new MeshComponent();
            SceneSaveComponent saveComponent = new SceneSaveComponent();
            saveComponent.SetAssetReference(meshComponent, "Model", global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel(ZombislayerAssetCatalog.EnvironmentModelRelativePath));
            entity.AddComponent(meshComponent);
            entity.AddComponent(saveComponent);
            return (EditorEntity)entity;
        }

        EditorEntity CreatePlayerRootEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerPlayer");
            entity.LocalPosition = new float3(0f, 1.7f, 0f);
            entity.AddComponent(new city.game.ZombislayerFpsControllerComponent());
            return (EditorEntity)entity;
        }

        EditorEntity CreateCameraPivotEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerCameraPivot");
            return (EditorEntity)entity;
        }

        EditorEntity CreateCameraEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerCamera");
            entity.AddComponent(new CameraComponent {
                CameraDrawOrder = 0,
                LayerMask = EditorLayerMasks.SceneObjects,
                Viewport = new float4(0f, 0f, 1f, 1f),
                NearPlaneDistance = 0.05f,
                FarPlaneDistance = 200f,
                ClearSettings = new CameraClearSettings(true, new float4(100f / 255f, 149f / 255f, 237f / 255f, 1f), true, 1f, false, 0)
            });
            return (EditorEntity)entity;
        }

        EditorEntity CreateWeaponEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerWeapon");
            entity.LocalPosition = new float3(0.18f, -0.18f, 0.42f);
            MeshComponent meshComponent = new MeshComponent();
            SceneSaveComponent saveComponent = new SceneSaveComponent();
            saveComponent.SetAssetReference(meshComponent, "Model", global::helengine.SceneAssetReferenceFactory.CreateFileSystemModel(ZombislayerAssetCatalog.WeaponModelRelativePath));
            entity.AddComponent(meshComponent);
            entity.AddComponent(saveComponent);
            return (EditorEntity)entity;
        }

        EditorEntity CreateUiRootEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerUi");
            entity.AddComponent(new city.game.ZombislayerSessionComponent());
            entity.AddComponent(new ViewportComponent { BindingMode = ViewportComponent.ScreenBindingMode, FixedSize = new int2(1280, 720) });
            entity.AddComponent(new ReferenceCanvasFitComponent { ReferenceWidth = 1280, ReferenceHeight = 720 });

            Entity pauseOverlay = Core.Instance.EntityFactory.Create("ZombislayerPauseOverlay");
            pauseOverlay.Enabled = false;
            entity.AddChild(pauseOverlay);
            return (EditorEntity)entity;
        }

        EditorEntity CreateSunEntity() {
            Entity entity = Core.Instance.EntityFactory.Create("ZombislayerSun");
            entity.AddComponent(new DirectionalLightComponent { Intensity = 1f, ShadowsEnabled = true, ShadowMapMode = ShadowMapMode.Forced, ShadowDistance = 72f });
            return (EditorEntity)entity;
        }
    }
}
```

Modify `C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs`:

```csharp
ZombislayerAssetPreparationService zombislayerAssetPreparationService = new ZombislayerAssetPreparationService();
ZombislayerGenerationAssets zombislayerAssets = zombislayerAssetPreparationService.Prepare(projectRootPath);
ZombislayerSceneFactory zombislayerSceneFactory = new ZombislayerSceneFactory(zombislayerAssets);
GeneratedAuthoringSceneDefinition zombislayerScene = zombislayerSceneFactory.CreateGameplayScene();
sceneWriteService.WriteScene(projectRootPath, zombislayerScene);
```

- [ ] **Step 4: Run the tests to verify they pass**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerSceneGenerationSourceTests'"
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~City_zombislayer_source_uses_generated_scene_factory_and_runtime_components'"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helprojs\city add assets\codebase\game.tools\ZombislayerSceneFactory.cs assets\codebase\game.tools\GameSceneGenerator.cs assets\codebase\game.tools.tests\ZombislayerSceneGenerationSourceTests.cs
git -C C:\dev\helworks\helengine add engine\helengine.editor.tests\CityGameSceneSourceTests.cs
git -C C:\dev\helprojs\city commit -m "feat: generate zombislayer gameplay scene"
git -C C:\dev\helworks\helengine commit -m "test: audit zombislayer generated scene source"
```

### Task 5: Make the scene appear in Windows builds only

**Files:**
- Modify: `C:\dev\helprojs\city\user_settings\build_config.json`
- Create: `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerBuildConfigTests.cs`

- [ ] **Step 1: Write the failing build-config test**

Create `C:\dev\helprojs\city\assets\codebase\game.tools.tests\ZombislayerBuildConfigTests.cs`:

```csharp
using System.Text.Json;

namespace city.tests {
    /// <summary>
    /// Verifies the Windows demo-disc build packages the generated Zombislayer gameplay scene so the menu item remains visible and launchable.
    /// </summary>
    public sealed class ZombislayerBuildConfigTests {
        [Fact]
        public void Windows_build_config_packages_zombislayer_scene() {
            string json = File.ReadAllText(@"C:\dev\helprojs\city\user_settings\build_config.json");
            using JsonDocument document = JsonDocument.Parse(json);

            JsonElement windowsPlatform = document.RootElement
                .GetProperty("platforms")
                .EnumerateArray()
                .Single(platform => string.Equals(platform.GetProperty("platformId").GetString(), "windows", StringComparison.Ordinal));

            string[] selectedSceneIds = windowsPlatform.GetProperty("selectedSceneIds").EnumerateArray().Select(scene => scene.GetString() ?? string.Empty).ToArray();

            Assert.Contains("scenes/games/zombislayer.helen", selectedSceneIds);
        }
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerBuildConfigTests'"
```

Expected: FAIL because the Windows build config does not package the new scene yet.

- [ ] **Step 3: Update the Windows build config**

In `C:\dev\helprojs\city\user_settings\build_config.json`, add the scene id to the Windows platform section only.

Update the Windows `selectedSceneIds` block:

```json
"selectedSceneIds": [
  "GeneratedBootScene",
  "DemoDiscMainMenu",
  "cube_test",
  "colored_cube_grid",
  "textured_cube_grid",
  "axis_test",
  "axis_test2",
  "test_scene_matrix_render",
  "directional_shadow_plaza",
  "test_scene_dynamic_stack_boxes",
  "test_scene_dynamic_sphere_stack",
  "test_scene_dynamic_mixed_stack",
  "test_scene_static_mesh_showcase",
  "test_scene_static_mesh_minimal",
  "tilt_trial",
  "scenes/games/tilt_trial_level_01.helen",
  "scenes/games/tilt_trial_level_02.helen",
  "scenes/games/tilt_trial_level_03.helen",
  "scenes/games/tilt_trial_level_04.helen",
  "scenes/games/tilt_trial_level_05.helen",
  "scenes/games/zombislayer.helen"
]
```

Update the Windows `sceneOrders` block:

```json
{
  "sceneId": "scenes/games/zombislayer.helen",
  "orderNumber": 21
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~ZombislayerBuildConfigTests'"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git -C C:\dev\helprojs\city add user_settings\build_config.json assets\codebase\game.tools.tests\ZombislayerBuildConfigTests.cs
git -C C:\dev\helprojs\city commit -m "build: add zombislayer to windows demo disc config"
```

### Task 6: Regenerate the scene, run the narrow suite, and verify the Windows build

**Files:**
- Modify/generated: `C:\dev\helprojs\city\assets\scenes\games\zombislayer.helen`
- Verify: `C:\dev\helprojs\city\windows-build\helengine_windows.exe`

- [ ] **Step 1: Regenerate the city game scenes**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet run --project 'C:\dev\helworks\helengine\helengine.ui\helengine.editor.app\helengine.editor.app.csproj' -- --project 'C:\dev\helprojs\city\project.heproj' --editor-command menu.generate-game-scenes"
```

Expected: command exits successfully and writes `C:\dev\helprojs\city\assets\scenes\games\zombislayer.helen`.

- [ ] **Step 2: Run the targeted automated suite**

Run:

```powershell
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helworks\helengine\engine\helengine.editor.tests\helengine.editor.tests.csproj' --filter 'FullyQualifiedName~City_demo_disc_menu_source_exposes_zombislayer_games_entry|FullyQualifiedName~City_game_scene_catalog_source_exports_zombislayer_scene|FullyQualifiedName~City_zombislayer_source_uses_generated_scene_factory_and_runtime_components'"
rtk powershell -NoProfile -Command "dotnet test 'C:\dev\helprojs\city\city.sln' --filter 'FullyQualifiedName~Scene_catalog_reuses_runtime_zombislayer_scene_id|FullyQualifiedName~ZombislayerAssetPreparationSourceTests|FullyQualifiedName~ZombislayerSessionComponentTests|FullyQualifiedName~ZombislayerFpsControllerComponentTests|FullyQualifiedName~ZombislayerSceneGenerationSourceTests|FullyQualifiedName~ZombislayerBuildConfigTests'"
```

Expected: PASS.

- [ ] **Step 3: Build the Windows target**

Run:

```powershell
rtk powershell -NoProfile -ExecutionPolicy Bypass -Command "& 'C:\dev\helworks\helengine\artifacts\build-platform.ps1' -Project 'C:\dev\helprojs\city\project.heproj' -Platform 'windows' -Output 'C:\dev\helprojs\city\windows-build'"
```

Expected: command exits successfully and refreshes `C:\dev\helprojs\city\windows-build\helengine_windows.exe`.

- [ ] **Step 4: Launch the Windows build and perform the smoke test**

Run:

```powershell
rtk powershell -NoProfile -Command "Get-Process helengine_windows -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue; Start-Process -FilePath 'C:\dev\helprojs\city\windows-build\helengine_windows.exe' -WorkingDirectory 'C:\dev\helprojs\city\windows-build'"
```

Expected manual smoke test:

1. open `Games`
2. select `Zombislayer`
3. verify direct gameplay load
4. verify `WASD` movement
5. verify mouse look
6. verify the M4 viewmodel is visible
7. press `Esc`
8. verify pause overlay appears
9. verify `Resume`
10. verify `Return to Demo Disc`

- [ ] **Step 5: Commit the regenerated scene and any generated import settings**

```powershell
git -C C:\dev\helprojs\city add assets\scenes\games\zombislayer.helen assets\models\games\zombislayer
git -C C:\dev\helprojs\city commit -m "chore: regenerate zombislayer gameplay scene"
```

---

## Self-Review

### Spec coverage

- Demo-disc `Games` entry: covered by Task 1.
- Windows-only first slice without breaking other platforms: covered by Task 5.
- Static environment + M4 viewmodel asset staging: covered by Task 2.
- FSM-backed session with `Playing` and `Paused`: covered by Task 3.
- FPS controls and pause behavior: covered by Task 3 and Task 4.
- Generated gameplay scene and direct launch: covered by Task 4 and Task 6.
- Windows runtime validation: covered by Task 6.

### Placeholder scan

- No `TODO`, `TBD`, or deferred code markers remain in the task steps.
- Each code-changing step includes concrete file paths and code blocks.
- Each test/run step includes an exact command.

### Type consistency

- Scene id type name is consistently `ZombislayerSceneIds`.
- Runtime state type is consistently `ZombislayerSessionState`.
- Session controller type is consistently `ZombislayerSessionComponent`.
- FPS controller type is consistently `ZombislayerFpsControllerComponent`.
- Generation asset types are consistently `ZombislayerAssetCatalog`, `ZombislayerGenerationAssets`, `ZombislayerAssetPreparationService`, and `ZombislayerSceneFactory`.

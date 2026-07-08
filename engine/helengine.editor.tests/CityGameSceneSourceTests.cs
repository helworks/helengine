namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city game-scene source keeps the intended Tilt Trial wiring.
/// </summary>
public sealed class CityGameSceneSourceTests {
    /// <summary>
    /// Ensures the new game catalog exports the Tilt Trial scene id.
    /// </summary>
    [Fact]
    public void City_game_scene_catalog_source_exports_tilt_trial_scene() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneCatalog.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("public const string TiltTrialSceneId = \"scenes/games/tilt_trial.helen\";", source, StringComparison.Ordinal);
        Assert.Contains("TiltTrialSceneId,", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the demo-disc menu exposes a top-level Games panel and one Tilt Trial item.
    /// </summary>
    [Fact]
    public void City_demo_disc_menu_source_exposes_games_panel_and_tilt_trial_item() {
        string menuProviderPath = @"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscMenuDefinitionProvider.cs";
        string menuProviderSource = File.ReadAllText(menuProviderPath);
        string sceneCatalogPath = @"C:\dev\helprojs\city\assets\codebase\menu\DemoDiscSceneCatalog.cs";
        string sceneCatalogSource = File.ReadAllText(sceneCatalogPath);

        Assert.Contains("\"main-games\", \"Games\"", menuProviderSource, StringComparison.Ordinal);
        Assert.Contains("\"games-select\",", menuProviderSource, StringComparison.Ordinal);
        Assert.Contains("sceneCatalog.CreateGameSceneItems()", menuProviderSource, StringComparison.Ordinal);
        Assert.Contains("\"games-tilt-trial\"", sceneCatalogSource, StringComparison.Ordinal);
        Assert.Contains("\"Tilt Trial\"", sceneCatalogSource, StringComparison.Ordinal);
        Assert.Contains("\"tilt_trial\"", sceneCatalogSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the game-scene factory builds Tilt Trial from box gameplay geometry and game-owned runtime components.
    /// </summary>
    [Fact]
    public void City_tilt_trial_source_uses_box_course_stage_tilt_reset_and_game_camera() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("CreateTiltTrialScene()", source, StringComparison.Ordinal);
        Assert.Contains("entity.LocalPosition = new float3(0f, 3.675f, 1f);", source, StringComparison.Ordinal);
        Assert.Contains("\"StageRoot\"", source, StringComparison.Ordinal);
        Assert.Contains("\"StartPad\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Ramp\"", source, StringComparison.Ordinal);
        Assert.Contains("\"GoalPad\"", source, StringComparison.Ordinal);
        Assert.Contains("\"CatchFloor\"", source, StringComparison.Ordinal);
        Assert.Contains("\"PlayerSphere\"", source, StringComparison.Ordinal);
        Assert.Contains("new city.game.DemoTiltStageComponent", source, StringComparison.Ordinal);
        Assert.Contains("new city.game.DemoTiltBallResetComponent", source, StringComparison.Ordinal);
        Assert.Contains("new city.game.DemoTiltFollowCameraComponent", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Tilt Trial scene factory writes the tuned controller values directly into the generated authored scene instead of relying on stale serialized defaults.
    /// </summary>
    [Fact]
    public void City_tilt_trial_source_writes_explicit_stage_drive_tuning() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("MaximumPlanarSpeed = 11.25f", source, StringComparison.Ordinal);
        Assert.Contains("PlanarAccelerationUnitsPerSecond = 4.25f", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Tilt Trial player sphere source uses the dedicated marble material asset and generator path.
    /// </summary>
    [Fact]
    public void City_tilt_trial_player_sphere_source_uses_marble_material() {
        string gameSceneFactoryPath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string gameSceneFactorySource = File.ReadAllText(gameSceneFactoryPath);
        string gameSceneGeneratorPath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs";
        string gameSceneGeneratorSource = File.ReadAllText(gameSceneGeneratorPath);
        string preparationSourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneAssetPreparationService.cs";
        string preparationSource = File.ReadAllText(preparationSourcePath);

        Assert.Contains("Materials.rendering.tilt_trial.PlayerSphereMarble", gameSceneFactorySource, StringComparison.Ordinal);
        Assert.Contains("PlayerSphereMaterialReferenceName = \"Materials[0]\"", gameSceneFactorySource, StringComparison.Ordinal);
        Assert.Contains("CreateFileSystemMaterial(TiltTrialPlayerSphereMarbleMaterialRelativePath)", gameSceneFactorySource, StringComparison.Ordinal);
        Assert.Contains("TiltTrialPlayerSphereMarbleMaterialFactory", gameSceneGeneratorSource, StringComparison.Ordinal);
        Assert.Contains("materials/rendering/tilt_trial/PlayerSphereMarble.hasset", preparationSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ensures the Tilt Trial generated UI includes a packaged speed label that follows the player sphere target.
    /// </summary>
    [Fact]
    public void City_tilt_trial_scene_source_includes_ball_speed_hud() {
        string sourcePath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string source = File.ReadAllText(sourcePath);

        Assert.Contains("\"TiltTrialSpeedText\"", source, StringComparison.Ordinal);
        Assert.Contains("\"0 km/h\"", source, StringComparison.Ordinal);
        Assert.Contains("new city.game.DemoTiltSpeedTextComponent()", source, StringComparison.Ordinal);
        Assert.Contains("ConfigureTiltTrialSpeedTextTarget(uiEntity, playerSphereEntity);", source, StringComparison.Ordinal);
        Assert.Contains("CreateEditorUiFontReference()", source, StringComparison.Ordinal);
    }
}

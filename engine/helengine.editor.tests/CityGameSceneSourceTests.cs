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
    /// Ensures the Tilt Trial player sphere source uses the dedicated walnut material asset and generator path.
    /// </summary>
    [Fact]
    public void City_tilt_trial_player_sphere_source_uses_walnut_material() {
        string gameSceneFactoryPath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneFactory.cs";
        string gameSceneFactorySource = File.ReadAllText(gameSceneFactoryPath);
        string gameSceneGeneratorPath = @"C:\dev\helprojs\city\assets\codebase\game.tools\GameSceneGenerator.cs";
        string gameSceneGeneratorSource = File.ReadAllText(gameSceneGeneratorPath);
        string preparationSourcePath = @"C:\dev\helprojs\city\assets\codebase\rendering.tools\RenderingSceneAssetPreparationService.cs";
        string preparationSource = File.ReadAllText(preparationSourcePath);

        Assert.Contains("Materials.rendering.tilt_trial.PlayerSphereWalnut", gameSceneFactorySource, StringComparison.Ordinal);
        Assert.Contains("TiltTrialPlayerSphereWalnutMaterialFactory", gameSceneGeneratorSource, StringComparison.Ordinal);
        Assert.Contains("materials/rendering/tilt_trial/PlayerSphereWalnut.hasset", preparationSource, StringComparison.Ordinal);
    }
}

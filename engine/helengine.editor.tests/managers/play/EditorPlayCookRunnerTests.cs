using helengine.baseplatform.Definitions;
using helengine.editor.tests.testing;
using Xunit;

namespace helengine.editor.tests.managers.play;

/// <summary>
/// Verifies that the Play cook snapshots the platform's build settings but cooks only the scene open in the editor.
/// </summary>
public sealed class EditorPlayCookRunnerTests : IDisposable {
    /// <summary>
    /// Platform identifier of the test builder definition.
    /// </summary>
    const string PlatformId = "windows";

    /// <summary>
    /// Scratch project root, deleted on dispose.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Saved path of the scene the test treats as open in the editor.
    /// </summary>
    string CurrentScenePath;

    /// <summary>
    /// Creates a scratch project with two authored scenes.
    /// </summary>
    public EditorPlayCookRunnerTests() {
        ProjectRootPath = Path.Combine(@"C:\temp\viewport-layout\play-tests", Guid.NewGuid().ToString("N"));
        WriteScene("Scenes/A.helen");
        CurrentScenePath = WriteScene("Scenes/B.helen");
    }

    /// <summary>
    /// Deletes the scratch project.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(ProjectRootPath)) {
            Directory.Delete(ProjectRootPath, true);
        }
    }

    /// <summary>
    /// Ensures the queue item keeps the platform's settings but selects only the open scene and the Play output folder.
    /// </summary>
    [Fact]
    public void CreateQueueItem_SelectsOnlyTheOpenScene() {
        EditorPlayCookRunner runner = CreateRunner();

        EditorBuildQueueItemDocument queueItem = runner.CreateQueueItem(PlatformId);

        Assert.Equal(new[] { "B" }, queueItem.SelectedSceneIds);
        Assert.Single(queueItem.SelectedSceneReferences);
        Assert.Equal(PlatformId, queueItem.PlatformId);
        Assert.Equal("debug", queueItem.SelectedBuildProfileId);
        Assert.Equal(Path.Combine(ProjectRootPath, "cache", "play", PlatformId, "output"), queueItem.OutputDirectoryPath);
    }

    /// <summary>The explicit Play mode reaches cooking without rewriting the project's graphics selection.</summary>
    [Fact]
    public void Cook_CapturesPlayVideoModeBeforeExecutorRuns() {
        PlatformVideoMode small = new PlatformVideoMode("small", 320, 240, true, 60, 60, 4, 3);
        PlatformVideoMode large = new PlatformVideoMode("large", 640, 480, true, 60, 60, 4, 3);
        PlatformVideoModeCapability modes = new PlatformVideoModeCapability("video-mode", [small, large]);
        PlatformPlayerCapability capability = new PlatformPlayerCapability(true, "a.B", "staging", 60,
            PlayerInputDevices.Gamepad, PlatformId, "1.0.0",
            [new PlatformSettingDefinition("video-mode", "Video Mode", PlatformSettingKind.Choice, "project", true, ["project", "small", "large"])]);
        EditorPlaySettingsDocument settings = new EditorPlaySettingsDocument();
        settings.PlatformValues[PlatformId] = new Dictionary<string, string> { ["video-mode"] = "large" };
        EditorPlaySettingsSnapshot snapshot = new EditorPlaySettingsSnapshot(
            EditorPlaySettingsEvaluator.Evaluate(settings, Path.Combine(ProjectRootPath, "play.json"), PlatformId, capability),
            false, capability, modes, 55);
        RecordingPlayAssetCookExecutor executor = new RecordingPlayAssetCookExecutor();
        EditorBuildPlatformConfigDocument projectSettings = new EditorBuildPlatformConfigDocument {
            PlatformId = PlatformId, SelectedSceneIds = ["A", "B"],
            SelectedGraphicsOptionValues = new Dictionary<string, string> { ["video-mode"] = "small" }
        };
        EditorPlayCookRunner runner = CreateRunner(platform => projectSettings, () => CurrentScenePath, platform => executor);
        Assert.True(runner.Cook(PlatformId, snapshot).Succeeded);
        Assert.Same(large, Assert.Single(executor.QueueItems).PlayVideoMode);
        Assert.Equal("small", projectSettings.SelectedGraphicsOptionValues["video-mode"]);
    }

    /// <summary>
    /// Ensures the Play script build receives the same environment as the cooked queue item.
    /// </summary>
    [Fact]
    public void ResolveEnvironmentId_ReturnsTheQueueItemEnvironment() {
        EditorPlayCookRunner runner = CreateRunner();

        Assert.Equal("debug", runner.ResolveEnvironmentId(PlatformId));
    }

    /// <summary>
    /// Ensures the Play script environment equals the one a Play queue item of the same build settings captures.
    /// </summary>
    /// <param name="selectedEnvironmentId">Persisted environment selection of the platform.</param>
    /// <param name="debugBuild">Persisted debug-build flag of the platform.</param>
    /// <param name="selectedBuildProfileId">Persisted build profile selection, possibly stale or non-default.</param>
    [Theory]
    [InlineData("", false, "")]
    [InlineData("", true, "")]
    [InlineData(" staging ", false, "")]
    [InlineData("release", true, "")]
    [InlineData("", false, "retired-profile")]
    [InlineData("", false, "release")]
    [InlineData("", true, "Debug")]
    public void ResolveEnvironmentId_EqualsTheEnvironmentTheQueueItemCaptures(string selectedEnvironmentId, bool debugBuild, string selectedBuildProfileId) {
        EditorPlayCookRunner runner = CreateRunner(platformId => new EditorBuildPlatformConfigDocument {
            PlatformId = platformId,
            SelectedSceneIds = ["A", "B"],
            SelectedEnvironmentId = selectedEnvironmentId,
            DebugBuild = debugBuild,
            SelectedBuildProfileId = selectedBuildProfileId
        });

        string environmentId = runner.ResolveEnvironmentId(PlatformId);

        Assert.Equal(runner.CreateQueueItem(PlatformId).SelectedEnvironmentId, environmentId);
    }

    /// <summary>
    /// Ensures resolving the Play environment neither builds a queue item (it never asks for the open scene) nor
    /// rewrites the platform's build settings the way queue-item creation does.
    /// </summary>
    [Fact]
    public void ResolveEnvironmentId_DoesNotCreateAQueueItemOrChangeTheSettings() {
        EditorBuildPlatformConfigDocument platformConfig = new EditorBuildPlatformConfigDocument {
            PlatformId = PlatformId,
            SelectedSceneIds = ["A", "B"]
        };
        int scenePathRequests = 0;
        EditorPlayCookRunner runner = CreateRunner(
            platformId => platformConfig,
            () => {
                scenePathRequests++;
                return CurrentScenePath;
            });

        string environmentId = runner.ResolveEnvironmentId(PlatformId);

        Assert.Equal("debug", environmentId);
        Assert.Equal(0, scenePathRequests);
        Assert.Equal(string.Empty, platformConfig.SelectedBuildProfileId);
        Assert.Equal(string.Empty, platformConfig.SelectedGraphicsProfileId);
        Assert.Equal(new[] { "A", "B" }, platformConfig.SelectedSceneIds);
    }

    /// <summary>
    /// Ensures one Play (cook, then the script stage's environment lookup) loads the platform's build settings once,
    /// and the script stage receives the environment of the queue item that was cooked.
    /// </summary>
    [Fact]
    public void Cook_ThenResolveEnvironmentId_LoadsTheBuildSettingsOnce() {
        int settingsLoads = 0;
        RecordingPlayAssetCookExecutor executor = new RecordingPlayAssetCookExecutor();
        EditorPlayCookRunner runner = CreateRunner(
            platformId => {
                settingsLoads++;
                return new EditorBuildPlatformConfigDocument {
                    PlatformId = platformId,
                    SelectedSceneIds = ["A", "B"],
                    SelectedEnvironmentId = " staging "
                };
            },
            () => CurrentScenePath,
            platformId => executor);

        EditorPlayCookResult cookResult = runner.Cook(PlatformId);
        string environmentId = runner.ResolveEnvironmentId(PlatformId);

        Assert.True(cookResult.Succeeded);
        Assert.Equal(1, settingsLoads);
        Assert.Equal(Assert.Single(executor.QueueItems).SelectedEnvironmentId, environmentId);
        Assert.Equal("staging", environmentId);
    }

    /// <summary>
    /// Ensures a failed cook remembers no environment, so the lookup reads the platform's current settings.
    /// </summary>
    [Fact]
    public void ResolveEnvironmentId_AfterAFailedCook_ReadsTheCurrentSettings() {
        string selectedEnvironmentId = "staging";
        EditorPlayCookRunner runner = CreateRunner(
            platformId => new EditorBuildPlatformConfigDocument {
                PlatformId = platformId,
                SelectedSceneIds = ["A", "B"],
                SelectedEnvironmentId = selectedEnvironmentId
            },
            () => CurrentScenePath,
            platformId => new FailingPlayAssetCookExecutor());

        EditorPlayCookResult cookResult = runner.Cook(PlatformId);
        selectedEnvironmentId = "qa";

        Assert.False(cookResult.Succeeded);
        Assert.Equal("qa", runner.ResolveEnvironmentId(PlatformId));
    }

    /// <summary>
    /// Ensures the environment remembered from a cook is only used for the platform that was cooked.
    /// </summary>
    [Fact]
    public void ResolveEnvironmentId_ForAnotherPlatformThanTheCook_ReadsThatPlatformsSettings() {
        RecordingPlayAssetCookExecutor executor = new RecordingPlayAssetCookExecutor();
        EditorPlayCookRunner runner = CreateRunner(
            platformId => new EditorBuildPlatformConfigDocument {
                PlatformId = platformId,
                SelectedSceneIds = ["A", "B"],
                SelectedEnvironmentId = platformId == PlatformId ? "staging" : "qa"
            },
            () => CurrentScenePath,
            platformId => executor);

        runner.Cook(PlatformId);

        Assert.Equal("qa", runner.ResolveEnvironmentId("linux"));
        Assert.Equal("staging", runner.ResolveEnvironmentId(PlatformId));
    }

    /// <summary>
    /// Ensures a scene saved outside the project's assets cannot be played.
    /// </summary>
    [Fact]
    public void CreateQueueItem_WhenOpenSceneIsOutsideAssets_Throws() {
        EditorPlayCookRunner runner = CreateRunner();
        CurrentScenePath = Path.Combine(ProjectRootPath, "outside.helen");

        Assert.Throws<InvalidOperationException>(() => runner.CreateQueueItem(PlatformId));
    }

    /// <summary>
    /// Creates the runner with both scenes selected in the platform's build settings.
    /// </summary>
    /// <returns>Runner under test; its executor factory is never reached by these tests.</returns>
    EditorPlayCookRunner CreateRunner() {
        return CreateRunner(platformId => new EditorBuildPlatformConfigDocument {
            PlatformId = platformId,
            SelectedSceneIds = ["A", "B"]
        });
    }

    /// <summary>
    /// Creates the runner with the supplied platform build settings and the test's open scene.
    /// </summary>
    /// <param name="platformConfigResolver">Supplies the platform's build settings.</param>
    /// <returns>Runner under test; its executor factory is never reached by these tests.</returns>
    EditorPlayCookRunner CreateRunner(Func<string, EditorBuildPlatformConfigDocument> platformConfigResolver) {
        return CreateRunner(platformConfigResolver, () => CurrentScenePath);
    }

    /// <summary>
    /// Creates the runner with the supplied platform build settings and open-scene provider.
    /// </summary>
    /// <param name="platformConfigResolver">Supplies the platform's build settings.</param>
    /// <param name="currentScenePathProvider">Supplies the saved path of the open scene.</param>
    /// <returns>Runner under test; its executor factory is never reached by these tests.</returns>
    EditorPlayCookRunner CreateRunner(Func<string, EditorBuildPlatformConfigDocument> platformConfigResolver, Func<string> currentScenePathProvider) {
        return CreateRunner(
            platformConfigResolver,
            currentScenePathProvider,
            platformId => throw new InvalidOperationException("The executor is not used by queue-item tests."));
    }

    /// <summary>
    /// Creates the runner with the supplied platform build settings, open-scene provider and cook executor.
    /// </summary>
    /// <param name="platformConfigResolver">Supplies the platform's build settings.</param>
    /// <param name="currentScenePathProvider">Supplies the saved path of the open scene.</param>
    /// <param name="executorFactory">Creates the asset-cook executor of one platform.</param>
    /// <returns>Runner under test.</returns>
    EditorPlayCookRunner CreateRunner(
        Func<string, EditorBuildPlatformConfigDocument> platformConfigResolver,
        Func<string> currentScenePathProvider,
        Func<string, IEditorPlayAssetCookExecutor> executorFactory) {
        EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(
            new RecordingPlayPlatformAssetBuilder(PlatformPlayerCapability.CreateUnsupported()).Definition);
        return new EditorPlayCookRunner(
            ProjectRootPath,
            sceneCatalogService,
            platformConfigResolver,
            platformId => selectionModel,
            executorFactory,
            currentScenePathProvider);
    }

    /// <summary>
    /// Writes one serialized scene asset under the project's assets folder.
    /// </summary>
    /// <param name="sceneId">Assets-relative scene path.</param>
    /// <returns>Absolute path of the written scene.</returns>
    string WriteScene(string sceneId) {
        string scenePath = Path.Combine(ProjectRootPath, "assets", sceneId.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        using FileStream stream = File.Create(scenePath);
        AssetSerializer.Serialize(stream, new SceneAsset {
            Id = sceneId,
            AuthoringAssetId = Guid.NewGuid().ToString("N")
        });
        return scenePath;
    }
}

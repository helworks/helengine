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
        ProjectRootPath = Path.Combine(@"C:\dev\helworks\builds\helengine\play-tests", Guid.NewGuid().ToString("N"));
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
    [Theory]
    [InlineData("", false)]
    [InlineData("", true)]
    [InlineData(" staging ", false)]
    [InlineData("release", true)]
    public void ResolveEnvironmentId_EqualsTheEnvironmentTheQueueItemCaptures(string selectedEnvironmentId, bool debugBuild) {
        EditorPlayCookRunner runner = CreateRunner(platformId => new EditorBuildPlatformConfigDocument {
            PlatformId = platformId,
            SelectedSceneIds = ["A", "B"],
            SelectedEnvironmentId = selectedEnvironmentId,
            DebugBuild = debugBuild
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
        EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(
            new RecordingPlayPlatformAssetBuilder(PlatformPlayerCapability.CreateUnsupported()).Definition);
        return new EditorPlayCookRunner(
            ProjectRootPath,
            sceneCatalogService,
            platformConfigResolver,
            platformId => selectionModel,
            platformId => throw new InvalidOperationException("The executor is not used by queue-item tests."),
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

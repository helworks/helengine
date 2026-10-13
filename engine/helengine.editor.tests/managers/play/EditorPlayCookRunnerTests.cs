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
        EditorProjectSceneCatalogService sceneCatalogService = new EditorProjectSceneCatalogService(ProjectRootPath);
        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(
            new RecordingPlayPlatformAssetBuilder(PlatformPlayerCapability.CreateUnsupported()).Definition);
        return new EditorPlayCookRunner(
            ProjectRootPath,
            sceneCatalogService,
            platformId => new EditorBuildPlatformConfigDocument {
                PlatformId = platformId,
                SelectedSceneIds = ["A", "B"]
            },
            platformId => selectionModel,
            platformId => throw new InvalidOperationException("The executor is not used by queue-item tests."),
            () => CurrentScenePath);
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

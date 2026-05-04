using helengine.baseplatform.Builders;
using helengine.platforms;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the platform build executor delegates execution to the shared build-graph runner.
/// </summary>
public class EditorPlatformBuildGraphRunnerTests {
    [Fact]
    public void Execute_DelegatesToInjectedBuildGraphRunner() {
        FakeEditorPlatformBuildGraphRunner runner = new();
        EditorPlatformBuildExecutor executor = new(
            projectRootPath: Path.GetTempPath(),
            requiredEngineVersion: "1.0.0",
            projectId: "project",
            projectVersion: "1.0.0",
            importers: Array.Empty<IAssetImporterRegistration>(),
            platformDescriptor: new AvailablePlatformDescriptor(
                "windows",
                "Windows",
                "builder.dll",
                string.Empty,
                true,
                "generated-core",
                "codegen.exe"),
            defaultFontAsset: null,
            buildGraphRunner: runner);

        EditorBuildExecutionResult result = executor.Execute(new EditorBuildQueueItemDocument {
            QueueItemId = "queue-item",
            PlatformId = "windows",
            OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N")),
            SelectedSceneIds = ["Scenes/Main.helen"]
        });

        Assert.True(result.Succeeded);
        Assert.Equal("queue-item", runner.ExecutedQueueItemId);
    }

    sealed class FakeEditorPlatformBuildGraphRunner : EditorPlatformBuildGraphRunner {
        public FakeEditorPlatformBuildGraphRunner()
            : base(
                Path.GetTempPath(),
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("windows", "Windows", "builder.dll", string.Empty, true, "generated-core", "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService()) {
        }

        public string ExecutedQueueItemId { get; private set; }

        public override EditorBuildExecutionResult Execute(EditorBuildQueueItemDocument queueItem) {
            ExecutedQueueItemId = queueItem.QueueItemId;
            return EditorBuildExecutionResult.Success("Executed.");
        }
    }
}

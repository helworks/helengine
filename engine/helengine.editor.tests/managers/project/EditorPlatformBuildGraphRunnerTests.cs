using helengine.baseplatform.Builders;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Requests;
using helengine.platforms;
using System.Reflection;

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

    /// <summary>
    /// Verifies the build request uses the per-execution generated core root and resolved storage profile instead of descriptor defaults.
    /// </summary>
    [Fact]
    public void BuildRequest_uses_workspace_generated_core_root_and_resolved_storage_profile() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string stagingRootPath = Path.Combine(rootPath, "cooked");
        string builderWorkingRootPath = Path.Combine(rootPath, "builder");
        string outputRootPath = Path.Combine(rootPath, "output");
        string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
        Directory.CreateDirectory(stagingRootPath);
        Directory.CreateDirectory(builderWorkingRootPath);
        Directory.CreateDirectory(outputRootPath);
        Directory.CreateDirectory(generatedCoreRootPath);
        File.WriteAllText(Path.Combine(stagingRootPath, "dummy.txt"), "payload");

        try {
            EditorPlatformBuildGraphRunner runner = new(
                rootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "ps2",
                    "PlayStation 2",
                    typeof(FakePlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    Path.Combine(rootPath, "descriptor-generated-core"),
                    "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());

            EditorBuildQueueItemDocument queueItem = new() {
                QueueItemId = "queue-item",
                PlatformId = "ps2",
                OutputDirectoryPath = outputRootPath,
                SelectedSceneIds = ["Scenes/Main.helen"],
                SelectedBuildOptionValues = new Dictionary<string, string>(),
                SelectedGraphicsOptionValues = new Dictionary<string, string>(),
                SelectedCodegenOptionValues = new Dictionary<string, string>()
            };

            PlatformBuildManifest cookedManifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0",
                "Scenes/Main.helen",
                [
                    new PlatformBuildScene(
                        "Scenes/Main.helen",
                        "Main",
                        "cooked/scenes/main.hasset",
                        [],
                        [])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

            MethodInfo buildRequestMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "BuildRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(buildRequestMethod);

            PlatformBuildRequest request = (PlatformBuildRequest)buildRequestMethod.Invoke(
                runner,
                [
                    queueItem,
                    cookedManifest,
                    stagingRootPath,
                    builderWorkingRootPath,
                    "ps2-default",
                    "gs-kit",
                    "default",
                    "ps2-install-tree",
                    generatedCoreRootPath,
                    "disc-layout"
                ]);

            Assert.Equal(generatedCoreRootPath, request.GeneratedCoreCppRootPath);
            Assert.Equal("disc-layout", request.SelectedStorageProfileId);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
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

    /// <summary>
    /// Provides one minimal PS2 builder definition for request-construction tests.
    /// </summary>
    sealed class FakePlatformBuilder : IPlatformAssetBuilder {
        /// <summary>
        /// Initializes the fake PS2 builder metadata.
        /// </summary>
        public FakePlatformBuilder() {
            Descriptor = new(
                "test.ps2.builder",
                "1.0.0",
                "ps2",
                new("1.0.0", "999.0.0"),
                new(1, 3),
                ["ps2"],
                ["ps2"]);
            Definition = new(
                "ps2",
                "PlayStation 2",
                Array.Empty<helengine.baseplatform.Definitions.PlatformBuildProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformGraphicsProfileDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformAssetRequirementDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformMaterialSchemaDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentCompatibilityDefinition>(),
                Array.Empty<helengine.baseplatform.Definitions.PlatformCodegenProfileDefinition>(),
                [
                    new(
                        "disc-layout",
                        "Disc Layout",
                        helengine.baseplatform.Definitions.PlatformStorageProfileKind.DiscLayout,
                        "ps2-disc-layout",
                        true)
                ],
                [
                    new(
                        "ps2-install-tree",
                        "PS2 Install Tree",
                        helengine.baseplatform.Definitions.PlatformMediaLayoutKind.InstallTree,
                        true,
                        true)
                ]);
        }

        /// <summary>
        /// Gets the fake builder descriptor returned to the loader.
        /// </summary>
        public helengine.baseplatform.Descriptors.PlatformBuilderDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the fake PS2 platform definition used by the test.
        /// </summary>
        public helengine.baseplatform.Definitions.PlatformDefinition Definition { get; }

        /// <summary>
        /// Material cooking is not used by this test-only builder.
        /// </summary>
        /// <param name="request">Material translation request that is unsupported in this test.</param>
        /// <returns>This method always throws because the request-construction tests never cook materials.</returns>
        public helengine.baseplatform.Results.PlatformMaterialCookResult CookMaterial(helengine.baseplatform.Requests.PlatformMaterialCookRequest request) {
            throw new NotSupportedException("Material cooking is not used by this test builder.");
        }

        /// <summary>
        /// Returns a successful build report without mutating the request.
        /// </summary>
        public Task<helengine.baseplatform.Reporting.PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            helengine.baseplatform.Builders.IPlatformBuildProgressReporter progressReporter,
            helengine.baseplatform.Builders.IPlatformBuildDiagnosticReporter diagnosticReporter,
            CancellationToken cancellationToken) {
            return Task.FromResult(new helengine.baseplatform.Reporting.PlatformBuildReport(true, [], [], []));
        }
    }
}

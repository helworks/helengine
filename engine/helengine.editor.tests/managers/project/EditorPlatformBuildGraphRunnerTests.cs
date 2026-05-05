using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
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

    /// <summary>
    /// Verifies the build-graph runner writes renderer defaults from the persisted platform profile into generated native source.
    /// </summary>
    [Fact]
    public void WriteRuntimeGraphicsRendererManifestSource_uses_platform_profile_defaults() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try {
            EditorProfileSettingsService profileSettingsService = new EditorProfileSettingsService(rootPath);
            profileSettingsService.Save(new EditorProfileSettingsDocument {
                Platforms = new List<EditorPlatformProfileSettingsDocument> {
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "windows",
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            RendererDepthPrepassMode = DepthPrepassMode.Always,
                            RendererShadowQualityTier = "ultra",
                            RendererHdrEnabled = true,
                            RendererPostProcessTier = PostProcessTier.High
                        }
                    }
                }
            });

            EditorPlatformBuildGraphRunner runner = new(
                rootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "windows",
                    "Windows",
                    "builder.dll",
                    string.Empty,
                    true,
                    Path.Combine(rootPath, "descriptor-generated-core"),
                    "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());

            string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
            Directory.CreateDirectory(generatedCoreRootPath);

            MethodInfo writeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "WriteRuntimeGraphicsRendererManifestSource",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(writeMethod);

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(new FakePlatformBuilder().Definition);
            writeMethod.Invoke(runner, [generatedCoreRootPath, selectionModel]);

            string sourcePath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_graphics_renderer_manifest.cpp");
            Assert.True(File.Exists(sourcePath));

            string source = File.ReadAllText(sourcePath);
            Assert.Contains("HERuntimeDepthPrepassMode::Always", source);
            Assert.Contains("\"ultra\"", source);
            Assert.Contains("true", source);
            Assert.Contains("HERuntimePostProcessTier::High", source);
            Assert.Contains("HERuntimePs2DepthHandlerMode::Hardware", source);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies the build-graph runner forwards the PS2 depth-handler choice from the persisted graphics profile into generated native source.
    /// </summary>
    [Fact]
    public void WriteRuntimeGraphicsRendererManifestSource_uses_ps2_depth_handler_mode() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try {
            EditorProfileSettingsService profileSettingsService = new EditorProfileSettingsService(rootPath);
            profileSettingsService.Save(new EditorProfileSettingsDocument {
                Platforms = new List<EditorPlatformProfileSettingsDocument> {
                    new EditorPlatformProfileSettingsDocument {
                        PlatformId = "ps2",
                        Graphics = new EditorGraphicsProfileSettingsDocument {
                            SelectedGraphicsProfileId = "ps2-standard-forward",
                            SelectedOptionValues = new Dictionary<string, string> {
                                ["depth-handler-mode"] = "software"
                            }
                        }
                    }
                }
            });

            EditorPlatformBuildGraphRunner runner = new(
                rootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "ps2",
                    "PlayStation 2",
                    typeof(FakePs2PlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    Path.Combine(rootPath, "descriptor-generated-core"),
                    "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());

            string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
            Directory.CreateDirectory(generatedCoreRootPath);

            MethodInfo writeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "WriteRuntimeGraphicsRendererManifestSource",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(writeMethod);

            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(new FakePs2PlatformBuilder().Definition);
            writeMethod.Invoke(runner, [generatedCoreRootPath, selectionModel]);

            string sourcePath = Path.Combine(generatedCoreRootPath, "runtime", "runtime_graphics_renderer_manifest.cpp");
            Assert.True(File.Exists(sourcePath));

            string source = File.ReadAllText(sourcePath);
            Assert.Contains("HERuntimePs2DepthHandlerMode::Software", source);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies the shared Windows build graph can export the committed point-shadow smoke scene from a copied project workspace.
    /// </summary>
    [Fact]
    public void Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds() {
        string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(workspaceRootPath, "project");
        string outputRootPath = Path.Combine(workspaceRootPath, "output");

        try {
            CopyDirectory(sourceProjectRootPath, projectRootPath);
            ConfigureWindowsBuildForCommittedPointShadowScene(projectRootPath, outputRootPath);

            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(projectRootPath, "project.heproj"));
            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(
                bootstrap.BuildConfigService.TryLoadExisting(),
                "windows");
            EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel("windows");
            EditorBuildQueueItemFactory queueItemFactory = new EditorBuildQueueItemFactory(bootstrap.SceneCatalogService);
            EditorBuildQueueItemDocument queueItem = queueItemFactory.Create(platformConfig, selectionModel, outputRootPath);
            AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
            EditorPlatformBuildGraphRunner runner = new(
                bootstrap.ProjectRootPath,
                bootstrap.RequiredEngineVersion,
                bootstrap.ProjectName,
                bootstrap.ProjectVersion,
                Array.Empty<IAssetImporterRegistration>(),
                platformDescriptor,
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());

            EditorBuildExecutionResult result = runner.Execute(queueItem);

            Assert.True(result.Succeeded, result.Message);
        } finally {
            if (Directory.Exists(workspaceRootPath)) {
                Directory.Delete(workspaceRootPath, true);
            }
        }
    }

    /// <summary>
    /// Finds the persisted build configuration entry for one platform id.
    /// </summary>
    /// <param name="buildConfig">Persisted build configuration document.</param>
    /// <param name="platformId">Target platform identifier.</param>
    /// <returns>Matching platform configuration when present; otherwise null.</returns>
    static EditorBuildPlatformConfigDocument FindPlatformConfig(EditorBuildConfigDocument buildConfig, string platformId) {
        if (buildConfig == null) {
            throw new ArgumentNullException(nameof(buildConfig));
        }
        if (string.IsNullOrWhiteSpace(platformId)) {
            throw new ArgumentException("Platform id must be provided.", nameof(platformId));
        }

        for (int index = 0; index < buildConfig.Platforms.Count; index++) {
            EditorBuildPlatformConfigDocument platform = buildConfig.Platforms[index];
            if (platform != null && string.Equals(platform.PlatformId, platformId, StringComparison.OrdinalIgnoreCase)) {
                return platform;
            }
        }

        throw new InvalidOperationException($"Build configuration did not define platform '{platformId}'.");
    }

    /// <summary>
    /// Rewrites the copied test-project build configuration so Windows exports only the committed point-shadow smoke scene.
    /// </summary>
    /// <param name="projectRootPath">Copied project workspace root.</param>
    /// <param name="outputRootPath">Requested build output root.</param>
    static void ConfigureWindowsBuildForCommittedPointShadowScene(string projectRootPath, string outputRootPath) {
        EditorBuildConfigService buildConfigService = new EditorBuildConfigService(projectRootPath);
        EditorBuildConfigDocument buildConfig = buildConfigService.TryLoadExisting()
            ?? throw new InvalidOperationException($"Copied project at '{projectRootPath}' did not provide a build configuration.");

        for (int index = 0; index < buildConfig.Platforms.Count; index++) {
            EditorBuildPlatformConfigDocument platform = buildConfig.Platforms[index];
            if (platform == null || !string.Equals(platform.PlatformId, "windows", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            platform.SelectedSceneIds = ["Scenes/rendering/point-shadow.helen"];
            platform.OutputDirectoryPath = outputRootPath.Replace('\\', '/');
        }

        buildConfigService.Save(buildConfig);
    }

    /// <summary>
    /// Copies one directory tree into a target workspace while preserving the relative layout.
    /// </summary>
    /// <param name="sourceRootPath">Source directory tree to copy.</param>
    /// <param name="destinationRootPath">Destination directory tree that will receive the copy.</param>
    static void CopyDirectory(string sourceRootPath, string destinationRootPath) {
        Directory.CreateDirectory(destinationRootPath);
        string[] sourceFilePaths = Directory.GetFiles(sourceRootPath, "*", SearchOption.AllDirectories);
        Array.Sort(sourceFilePaths, StringComparer.OrdinalIgnoreCase);

        for (int index = 0; index < sourceFilePaths.Length; index++) {
            string sourceFilePath = sourceFilePaths[index];
            string relativePath = Path.GetRelativePath(sourceRootPath, sourceFilePath);
            string destinationPath = Path.Combine(destinationRootPath, relativePath);
            string destinationDirectoryPath = Path.GetDirectoryName(destinationPath)
                ?? throw new InvalidOperationException($"Unable to resolve destination directory for '{destinationPath}'.");
            Directory.CreateDirectory(destinationDirectoryPath);
            File.Copy(sourceFilePath, destinationPath, true);
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

    /// <summary>
    /// Provides one minimal PS2 builder definition for manifest-resolution tests.
    /// </summary>
    sealed class FakePs2PlatformBuilder : IPlatformAssetBuilder {
        /// <summary>
        /// Initializes the fake PS2 builder metadata.
        /// </summary>
        public FakePs2PlatformBuilder() {
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
                [
                    new PlatformBuildProfileDefinition(
                        "ps2-default",
                        "PS2 Default",
                        "PS2 player build",
                        "ps2-standard-forward",
                        "default",
                        [
                            new PlatformSettingDefinition(
                                "texture-scale-percent",
                                "Texture scale %",
                                PlatformSettingKind.Text,
                                "100",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "ps2-standard-forward",
                        "PS2 Standard Forward",
                        "Default PS2 forward renderer",
                        [
                            new PlatformSettingDefinition(
                                "default-width",
                                "Default width",
                                PlatformSettingKind.Text,
                                "640",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "default-height",
                                "Default height",
                                PlatformSettingKind.Text,
                                "448",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "vsync-enabled",
                                "VSync",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "fullscreen-enabled",
                                "Fullscreen",
                                PlatformSettingKind.Boolean,
                                "false",
                                true,
                                []),
                            new PlatformSettingDefinition(
                                "depth-handler-mode",
                                "Depth Handler Mode",
                                PlatformSettingKind.Choice,
                                "hardware",
                                true,
                                ["hardware", "software"])
                        ])
                ],
                [],
                [],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [
                            new PlatformSettingDefinition(
                                "write-conversion-report",
                                "Write Conversion Report",
                                PlatformSettingKind.Boolean,
                                "true",
                                true,
                                [])
                        ])
                ],
                [
                    new PlatformStorageProfileDefinition(
                        "disc-layout",
                        "Disc Layout",
                        PlatformStorageProfileKind.DiscLayout,
                        "ps2-disc-layout",
                        true)
                ],
                [
                    new PlatformMediaProfileDefinition(
                        "ps2-install-tree",
                        "PS2 Install Tree",
                        PlatformMediaLayoutKind.InstallTree,
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
        public PlatformDefinition Definition { get; }

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

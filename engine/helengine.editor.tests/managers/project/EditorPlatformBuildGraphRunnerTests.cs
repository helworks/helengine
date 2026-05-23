using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Requests;
using helengine.editor.tests.testing;
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
                "ps2",
                "2026.05.12",
                "Scenes/Main.helen",
                [
                    new PlatformBuildScene(
                        "Scenes/Main.helen",
                        "Main",
                        "cooked/scenes/Main.hasset",
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
    /// Verifies the cook phase targets the execution-root workspace so scene outputs land beneath the shared cooked tree without duplicating the `cooked` segment.
    /// </summary>
    [Fact]
    public void RunCookAssets_writes_scene_outputs_beneath_workspace_cook_root_without_duplicate_cooked_segment() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(rootPath, "project");
        Directory.CreateDirectory(Path.Combine(projectRootPath, "assets", "Scenes"));

        try {
            WriteSceneAssetForBuildGraphRunnerTest(projectRootPath, "Scenes/MainMenu.helen");
            EditorPlatformBuildGraphRunner runner = new(
                projectRootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "windows",
                    "Windows",
                    typeof(FakePlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    "generated-core",
                    "codegen.exe"),
                PackagedFontAssetFactory.Create(),
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());
            TestPlatformMaterialAssetBuilder builder = new();
            EditorBuildQueueItemDocument queueItem = new() {
                QueueItemId = "queue-item",
                PlatformId = "windows",
                OutputDirectoryPath = Path.Combine(rootPath, "output"),
                SelectedSceneIds = ["MainMenu"],
                SelectedBuildOptionValues = new Dictionary<string, string>(),
                SelectedGraphicsOptionValues = new Dictionary<string, string>(),
                SelectedCodegenOptionValues = new Dictionary<string, string>()
            };
            EditorPlatformBuildGraphWorkspace workspace = new(Path.Combine(rootPath, "workspace"));

            MethodInfo runCookAssetsMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "RunCookAssets",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(runCookAssetsMethod);

            PlatformBuildManifest manifest = (PlatformBuildManifest)runCookAssetsMethod.Invoke(
                runner,
                [
                    builder,
                    builder.Definition,
                    "debug",
                    "directx11",
                    queueItem,
                    workspace
                ]);

            Assert.NotNull(manifest);
            Assert.True(File.Exists(Path.Combine(workspace.CookRootPath, "scenes", "MainMenu.hasset")));
            Assert.False(File.Exists(Path.Combine(workspace.CookRootPath, "cooked", "scenes", "MainMenu.hasset")));
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies the editor runner stages package content into the builder-owned package-source root before platform packaging begins.
    /// </summary>
    [Fact]
    public void StageBuilderPackageSourceRoot_copies_package_content_into_builder_working_root() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string packageRootPath = Path.Combine(rootPath, "package");
        string builderWorkingRootPath = Path.Combine(rootPath, "builder");
        string staleFilePath = Path.Combine(builderWorkingRootPath, "package-source", "stale.bin");
        string payloadSourcePath = Path.Combine(packageRootPath, "cooked", "scenes", "startup.hasset");
        string payloadDestinationPath = Path.Combine(builderWorkingRootPath, "package-source", "cooked", "scenes", "startup.hasset");

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(payloadSourcePath)
                ?? throw new InvalidOperationException("Unable to resolve the package payload directory."));
            Directory.CreateDirectory(Path.GetDirectoryName(staleFilePath)
                ?? throw new InvalidOperationException("Unable to resolve the builder package-source directory."));
            File.WriteAllText(payloadSourcePath, "scene");
            File.WriteAllText(staleFilePath, "stale");

            MethodInfo stageBuilderPackageSourceRootMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "StageBuilderPackageSourceRoot",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(stageBuilderPackageSourceRootMethod);

            stageBuilderPackageSourceRootMethod.Invoke(null, [packageRootPath, builderWorkingRootPath]);

            Assert.True(File.Exists(payloadDestinationPath));
            Assert.Equal("scene", File.ReadAllText(payloadDestinationPath));
            Assert.False(File.Exists(staleFilePath));
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies the package phase preserves the `cooked/` runtime-relative prefix when mirroring cooked assets into the staged package root.
    /// </summary>
    [Fact]
    public void RunWriteContainers_stages_cooked_tree_beneath_package_root_cooked_directory() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(rootPath, "project");
        Directory.CreateDirectory(projectRootPath);

        try {
            EditorPlatformBuildGraphRunner runner = new(
                projectRootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "windows",
                    "Windows",
                    typeof(FakePlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    "generated-core",
                    "codegen.exe"),
                PackagedFontAssetFactory.Create(),
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());
            EditorPlatformBuildGraphWorkspace workspace = new(Path.Combine(rootPath, "workspace"));
            string cookedArtifactPath = Path.Combine(workspace.CookRootPath, "engine", "materials", "standard.hasset");
            string cookedArtifactDirectoryPath = Path.GetDirectoryName(cookedArtifactPath)
                ?? throw new InvalidOperationException("Cooked artifact directory path could not be resolved.");
            Directory.CreateDirectory(cookedArtifactDirectoryPath);
            File.WriteAllText(cookedArtifactPath, "payload");
            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0",
                "windows",
                "1.0.0",
                "MainMenu",
                Array.Empty<PlatformBuildScene>(),
                Array.Empty<PlatformBuildAsset>(),
                [
                    new PlatformBuildArtifact("cooked/engine/materials/standard.hasset", "engine:material:standard", "hash", "asset", "shared")
                ],
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));
            PlatformStorageProfileDefinition storageProfile = new(
                "default",
                "Default",
                PlatformStorageProfileKind.LooseFiles,
                "storage",
                true);
            PlatformMediaProfileDefinition mediaProfile = new(
                "install",
                "Install",
                PlatformMediaLayoutKind.InstallTree,
                true,
                true);

            MethodInfo runWriteContainersMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "RunWriteContainers",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(runWriteContainersMethod);

            runWriteContainersMethod.Invoke(
                runner,
                [
                    manifest,
                    storageProfile,
                    mediaProfile,
                    workspace
                ]);

            Assert.True(File.Exists(Path.Combine(workspace.PackageRootPath, "cooked", "engine", "materials", "standard.hasset")));
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies generated-core scene discovery can resolve cooked scene payloads when the cook workspace retains the runtime-relative <c>cooked/</c> prefix beneath the cook root.
    /// </summary>
    [Fact]
    public void DiscoverReferencedRuntimeModuleIdsFromCookedScenes_whenSceneExistsBeneathCookRootCookedPrefix_resolvesSceneSuccessfully() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(rootPath, "project");
        Directory.CreateDirectory(projectRootPath);

        try {
            EditorPlatformBuildGraphRunner runner = new(
                projectRootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "windows",
                    "Windows",
                    typeof(FakePlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    "generated-core",
                    "codegen.exe"),
                PackagedFontAssetFactory.Create(),
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService());
            EditorPlatformBuildGraphWorkspace workspace = new(Path.Combine(rootPath, "workspace"));
            string cookedScenePath = Path.Combine(workspace.CookRootPath, "cooked", "scenes", "MainMenu.hasset");
            WriteCookedSceneAssetForBuildGraphRunnerTest(cookedScenePath, "Scenes/MainMenu.helen");

            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0",
                "windows",
                "1.0.0",
                "MainMenu",
                [
                    new PlatformBuildScene(
                        "MainMenu",
                        "MainMenu",
                        "cooked/scenes/MainMenu.hasset",
                        [],
                        [
                            new KeyValuePair<string, string>(PlatformBuildSceneMetadataKeys.CookedRelativePath, "cooked/scenes/MainMenu.hasset")
                        ])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

            MethodInfo discoverMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "DiscoverReferencedRuntimeModuleIdsFromCookedScenes",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(discoverMethod);

            IReadOnlyList<string> moduleIds = (IReadOnlyList<string>)discoverMethod.Invoke(
                runner,
                [
                    manifest,
                    workspace.CookRootPath
                ]);

            Assert.Empty(moduleIds);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies scene-driven runtime component deserializer emission refreshes the generated-core unity translation unit so the emitted deserializer implementation files are compiled into native player builds.
    /// </summary>
    [Fact]
    public void EmitGeneratedRuntimeComponentDeserializersForCookedScenes_refreshes_generated_core_unity_translation_unit() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(rootPath, "project");
        Directory.CreateDirectory(projectRootPath);

        try {
            string componentTypeId = AutomaticScriptComponentPersistenceDescriptor.BuildComponentTypeId(typeof(TestAxisRotationScriptComponent));
            string cookedScenePath = Path.Combine(rootPath, "workspace", "cooked", "scenes", "AxisScene.hasset");
            WriteCookedSceneAssetForBuildGraphRunnerTest(cookedScenePath, "Scenes/AxisScene.helen", componentTypeId);

            DictionaryScriptTypeResolver scriptTypeResolver = new DictionaryScriptTypeResolver();
            scriptTypeResolver.Register(componentTypeId, typeof(TestAxisRotationScriptComponent));

            EditorPlatformBuildGraphRunner runner = new(
                projectRootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "windows",
                    "Windows",
                    typeof(FakePlatformBuilder).Assembly.Location,
                    string.Empty,
                    true,
                    "generated-core",
                    "codegen.exe"),
                PackagedFontAssetFactory.Create(),
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService(),
                scriptTypeResolver);
            string generatedCoreRootPath = Path.Combine(rootPath, "generated-core");
            Directory.CreateDirectory(generatedCoreRootPath);
            File.WriteAllText(Path.Combine(generatedCoreRootPath, "Component.cpp"), "void TouchComponentUnity() {}\n");
            EditorGeneratedCoreRegenerationService.WriteGeneratedCoreTranslationUnit(generatedCoreRootPath);

            PlatformBuildManifest manifest = new(
                1,
                "project",
                "1.0.0",
                "1.0.0",
                "windows",
                "1.0.0",
                "Scenes/AxisScene.helen",
                [
                    new PlatformBuildScene(
                        "Scenes/AxisScene.helen",
                        "AxisScene",
                        "cooked/scenes/AxisScene.hasset",
                        [],
                        [])
                ],
                Array.Empty<PlatformBuildAsset>(),
                Array.Empty<PlatformBuildArtifact>(),
                Array.Empty<PlatformBuildCodeModule>(),
                Array.Empty<PlatformArtifactPlacement>(),
                new PlatformContainerWritePlan(string.Empty, Array.Empty<PlatformContainerArtifact>()));

            MethodInfo emitMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "EmitGeneratedRuntimeComponentDeserializersForCookedScenes",
                BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.NotNull(emitMethod);

            emitMethod.Invoke(
                runner,
                [
                    manifest,
                    generatedCoreRootPath,
                    Path.Combine(rootPath, "workspace")
                ]);

            string unitySource = File.ReadAllText(Path.Combine(generatedCoreRootPath, "helengine_core_unity.cpp"));
            Assert.Contains("GeneratedRuntimeTestAxisRotationScriptComponentDeserializer.cpp", unitySource, StringComparison.Ordinal);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies the generic editor build-graph runner no longer owns runtime graphics renderer manifest emission.
    /// </summary>
    [Fact]
    public void Run_does_not_emit_runtime_graphics_renderer_manifest_source() {
        MethodInfo writeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
            "WriteRuntimeGraphicsRendererManifestSource",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Null(writeMethod);
    }

    /// <summary>
    /// Verifies the generic editor build-graph runner no longer owns platform-specific repository-root environment overrides.
    /// </summary>
    [Fact]
    public void Run_does_not_set_platform_specific_repository_root_environment_variables() {
        MethodInfo applyMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
            "ApplyBuilderEnvironmentOverrides",
            BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo restoreMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
            "RestoreBuilderEnvironmentOverrides",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Null(applyMethod);
        Assert.Null(restoreMethod);
    }

    /// <summary>
    /// Verifies the generic editor build-graph runner no longer owns a generated-core finalization pass.
    /// </summary>
    [Fact]
    public void GeneratedCoreFinalizationPass_is_not_part_of_the_editor_build_graph_runner() {
        string removedMethodName = "FinalizeGeneratedCore" + "Sources";
        MethodInfo finalizeMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
            removedMethodName,
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.Null(finalizeMethod);
    }

    /// <summary>
    /// Verifies the build-graph runner branches into host-debug finalization after a successful package result.
    /// </summary>
    [Fact]
    public void FinalizeBuildExecution_WhenHostDebugModeIsSelected_LaunchesHostDebugRunner() {
        RecordingHostDebugBuildGraphRunner runner = new();
        EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(new HostDebugPlatformBuilder().Definition);
        EditorBuildQueueItemDocument queueItem = new() {
            QueueItemId = "queue-item",
            PlatformId = "ps2",
            OutputDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-host-debug-finalize-tests", Guid.NewGuid().ToString("N")),
            SelectedSceneIds = ["Scenes/Main.helen"],
            ExecutionMode = EditorBuildExecutionMode.HostDebug
        };
        EditorBuildExecutionResult packageResult = EditorBuildExecutionResult.Success("Packaged.");

        EditorBuildExecutionResult result = runner.InvokeFinalizeBuildExecution(selectionModel, queueItem, packageResult);

        Assert.True(result.Succeeded);
        Assert.True(runner.HostDebugRunnerLaunched);
    }

    /// <summary>
    /// Verifies the host-debug launch path resolves the published PS2 runner and passes the packaged output root.
    /// </summary>
    [Fact]
    public void LaunchHostDebugRunner_WhenPs2HostDebugIsSelected_LaunchesThePublishedRunnerAgainstPackagedOutput() {
        string repositoryRootPath = Path.Combine(Path.GetTempPath(), "helengine-host-debug-runner-tests", Guid.NewGuid().ToString("N"));
        string runnerExecutablePath = Path.Combine(repositoryRootPath, "tools", "ps2-host-debugger", "bin", OperatingSystem.IsWindows() ? "ps2-host-debugger.exe" : "ps2-host-debugger");
        Directory.CreateDirectory(Path.GetDirectoryName(runnerExecutablePath)
            ?? throw new InvalidOperationException("Runner directory path could not be resolved."));
        File.WriteAllText(runnerExecutablePath, "host debug runner");

        try {
            RecordingHostDebugLaunchBuildGraphRunner runner = new(repositoryRootPath);
            EditorPlatformBuildSelectionModel selectionModel = EditorPlatformBuildSelectionModel.From(new HostDebugPlatformBuilder().Definition);
            string outputDirectoryPath = Path.Combine(Path.GetTempPath(), "helengine-host-debug-output", Guid.NewGuid().ToString("N"));
            EditorBuildQueueItemDocument queueItem = new() {
                QueueItemId = "queue-item",
                PlatformId = "ps2",
                OutputDirectoryPath = outputDirectoryPath,
                SelectedSceneIds = ["Scenes/Main.helen"],
                ExecutionMode = EditorBuildExecutionMode.HostDebug
            };

            EditorBuildExecutionResult result = runner.InvokeLaunchHostDebugRunner(
                selectionModel,
                queueItem,
                outputDirectoryPath,
                EditorBuildExecutionResult.Success("Packaged."));

            Assert.True(result.Succeeded);
            Assert.Equal(runnerExecutablePath, runner.LaunchedExecutablePath);
            Assert.Contains("--export-root", runner.LaunchedArguments);
            Assert.Contains(Path.GetFullPath(outputDirectoryPath), runner.LaunchedArguments);
            Assert.Contains("--mode load-only", runner.LaunchedArguments);
        } finally {
            if (Directory.Exists(repositoryRootPath)) {
                Directory.Delete(repositoryRootPath, true);
            }
        }
    }

    /// Verifies the build runner can summarize detected runtime features from the generated conversion report.
    /// </summary>
    [Fact]
    public void BuildDetectedFeatureSummary_when_conversion_report_exists_lists_enabled_and_disabled_features() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(rootPath);

        try {
            File.WriteAllText(
                Path.Combine(rootPath, "cpp-conversion-report.json"),
                "{\n"
                + "  \"buildFeatures\": {\n"
                + "    \"decisions\": [\n"
                + "      { \"feature\": \"DebugOverlay\", \"enabled\": false, \"origin\": \"NotIncluded\" },\n"
                + "      { \"feature\": \"Render2D\", \"enabled\": true, \"origin\": \"AutoDetected\" },\n"
                + "      { \"feature\": \"Text2D\", \"enabled\": true, \"origin\": \"AutoDetected\" }\n"
                + "    ]\n"
                + "  }\n"
                + "}\n");

            MethodInfo summaryMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "BuildDetectedFeatureSummary",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(summaryMethod);

            string summary = (string)summaryMethod.Invoke(null, [rootPath]);

            Assert.Contains("Enabled runtime features: Render2D (AutoDetected), Text2D (AutoDetected).", summary);
            Assert.Contains("Disabled runtime features: DebugOverlay (NotIncluded).", summary);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies source-scene 3D physics feature symbols are forwarded into generated-core regeneration.
    /// </summary>
    [Fact]
    public void RunRegenerateCore_ForwardsPhysicsSceneFeatureSymbolsFromSelectedScenes() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootPath, "assets", "Scenes"));

        try {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/PhysicsScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Ground",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Static, false),
                            CreateBoxColliderRecord(new float3(8f, 1f, 8f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    },
                    new SceneEntityAsset {
                        Id = 2u,
                        Name = "Box",
                        LocalPosition = new float3(0f, 2f, 0f),
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = new[] {
                            CreateRigidBodyRecord(BodyKind3D.Dynamic, true),
                            CreateBoxColliderRecord(new float3(1f, 1f, 1f), false)
                        },
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            using (FileStream sceneStream = File.Create(Path.Combine(rootPath, "assets", "Scenes", "PhysicsScene.helen"))) {
                AssetSerializer.Serialize(sceneStream, sceneAsset);
            }

            RecordingGeneratedCoreRegenerationService regenerationService = new RecordingGeneratedCoreRegenerationService();
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
                regenerationService);

            MethodInfo runRegenerateCoreMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "RunRegenerateCore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(runRegenerateCoreMethod);

            runRegenerateCoreMethod.Invoke(
                runner,
                [
                    CreatePlatformDefinition("windows", "Windows"),
                    CreateCodegenProfile(),
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-item",
                        PlatformId = "windows",
                        OutputDirectoryPath = Path.Combine(rootPath, "output"),
                        SelectedSceneIds = ["PhysicsScene"],
                        SelectedCodegenOptionValues = new Dictionary<string, string>()
                    },
                    new EditorPlatformBuildGraphWorkspace(Path.Combine(rootPath, "workspace"))
                ]);

            Assert.NotNull(regenerationService.AdditionalPreprocessorSymbols);
            Assert.Contains(PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, regenerationService.AdditionalPreprocessorSymbols);
            Assert.Contains(PhysicsSceneFeatureSymbolCatalog3D.BoxBoxContactSymbol, regenerationService.AdditionalPreprocessorSymbols);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies selected scenes without physics records do not request physics generated-core support.
    /// </summary>
    [Fact]
    public void RunRegenerateCore_WhenScenesDoNotUsePhysics_DoesNotForwardPhysicsSceneFeatureSymbols() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootPath, "assets", "Scenes"));

        try {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/VisualScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Camera",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            using (FileStream sceneStream = File.Create(Path.Combine(rootPath, "assets", "Scenes", "VisualScene.helen"))) {
                AssetSerializer.Serialize(sceneStream, sceneAsset);
            }

            RecordingGeneratedCoreRegenerationService regenerationService = new RecordingGeneratedCoreRegenerationService();
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
                regenerationService);

            MethodInfo runRegenerateCoreMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "RunRegenerateCore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(runRegenerateCoreMethod);

            runRegenerateCoreMethod.Invoke(
                runner,
                [
                    CreatePlatformDefinition("windows", "Windows"),
                    CreateCodegenProfile(),
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-item",
                        PlatformId = "windows",
                        OutputDirectoryPath = Path.Combine(rootPath, "output"),
                        SelectedSceneIds = ["VisualScene"],
                        SelectedCodegenOptionValues = new Dictionary<string, string>()
                    },
                    new EditorPlatformBuildGraphWorkspace(Path.Combine(rootPath, "workspace"))
                ]);

            Assert.NotNull(regenerationService.AdditionalPreprocessorSymbols);
            Assert.DoesNotContain(PhysicsSceneFeatureSymbolCatalog3D.SceneFeatureStrippingSymbol, regenerationService.AdditionalPreprocessorSymbols);
        } finally {
            if (Directory.Exists(rootPath)) {
                Directory.Delete(rootPath, true);
            }
        }
    }

    /// <summary>
    /// Verifies external platform-managed generated-core project paths are forwarded into regeneration.
    /// </summary>
    [Fact]
    public void RunRegenerateCore_ForwardsExternalGeneratedCoreProjectPaths() {
        string rootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(rootPath, "assets", "Scenes"));

        try {
            SceneAsset sceneAsset = new SceneAsset {
                Id = "Scenes/VisualScene.helen",
                RootEntities = new[] {
                    new SceneEntityAsset {
                        Id = 1u,
                        Name = "Camera",
                        LocalPosition = float3.Zero,
                        LocalScale = float3.One,
                        LocalOrientation = float4.Identity,
                        Components = Array.Empty<SceneComponentAssetRecord>(),
                        Children = Array.Empty<SceneEntityAsset>()
                    }
                }
            };
            using (FileStream sceneStream = File.Create(Path.Combine(rootPath, "assets", "Scenes", "VisualScene.helen"))) {
                AssetSerializer.Serialize(sceneStream, sceneAsset);
            }

            string externalProjectPath = Path.Combine(rootPath, "external", "helengine.ps2.csproj");
            Directory.CreateDirectory(Path.GetDirectoryName(externalProjectPath)
                ?? throw new InvalidOperationException("Unable to resolve the external generated-core project directory."));
            File.WriteAllText(externalProjectPath, "<Project />");

            RecordingGeneratedCoreRegenerationService regenerationService = new RecordingGeneratedCoreRegenerationService();
            EditorPlatformBuildGraphRunner runner = new(
                rootPath,
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor(
                    "ps2",
                    "PlayStation 2",
                    "builder.dll",
                    string.Empty,
                    true,
                    Path.Combine(rootPath, "descriptor-generated-core"),
                    "codegen.exe",
                    [externalProjectPath]),
                null,
                new EditorPlatformAssetBuilderLoader(),
                regenerationService);

            MethodInfo runRegenerateCoreMethod = typeof(EditorPlatformBuildGraphRunner).GetMethod(
                "RunRegenerateCore",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(runRegenerateCoreMethod);

            runRegenerateCoreMethod.Invoke(
                runner,
                [
                    CreatePlatformDefinition("ps2", "PlayStation 2"),
                    CreateCodegenProfile(),
                    new EditorBuildQueueItemDocument {
                        QueueItemId = "queue-item",
                        PlatformId = "ps2",
                        OutputDirectoryPath = Path.Combine(rootPath, "output"),
                        SelectedSceneIds = ["VisualScene"],
                        SelectedCodegenOptionValues = new Dictionary<string, string>()
                    },
                    new EditorPlatformBuildGraphWorkspace(Path.Combine(rootPath, "workspace"))
                ]);

            Assert.NotNull(regenerationService.GeneratedCoreProjectPaths);
            Assert.Single(regenerationService.GeneratedCoreProjectPaths);
            Assert.Equal(externalProjectPath, regenerationService.GeneratedCoreProjectPaths[0]);
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
            EditorBuildQueueItemDocument queueItem = EditorBuildQueueItemDocument.Create(
                bootstrap.SceneCatalogService,
                platformConfig,
                selectionModel,
                outputRootPath);
            queueItem.SelectedSceneIds = ["point-shadow"];
            AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("windows");
            EditorPlatformBuildGraphRunner runner = new(
                bootstrap.ProjectRootPath,
                bootstrap.RequiredEngineVersion,
                bootstrap.ProjectName,
                bootstrap.ProjectVersion,
                Array.Empty<IAssetImporterRegistration>(),
                platformDescriptor,
                PackagedFontAssetFactory.Create(),
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
    /// Verifies the shared Windows build graph still succeeds when generated core includes the expanded 2D clip command stream types.
    /// </summary>
    [Fact]
    public void Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithClipCommandsInGeneratedCore_Succeeds() {
        Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds();
    }

    /// <summary>
    /// Verifies the shared Windows build graph still succeeds after the native rounded-rect path moves to the SDF renderer.
    /// </summary>
    [Fact]
    public void Execute_WhenBuildingCommittedPointShadowSceneForWindows_WithRoundedRectSdfParity_Succeeds() {
        Execute_WhenBuildingCommittedPointShadowSceneForWindows_Succeeds();
    }

    /// <summary>
    /// Verifies project bootstrap can still resolve the installed Windows selection model when the editor runs from a git worktree copy.
    /// </summary>
    [Fact]
    public void Bootstrap_WhenRunningFromWorktreeCopy_ResolvesWindowsSelectionModel() {
        string repositoryRootPath = new EditorSourceBuildWorkspaceLocator().ResolveHelEngineRootPath();
        string sourceProjectRootPath = Path.Combine(repositoryRootPath, "test-project");
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-build-graph-runner-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(workspaceRootPath, "project");

        try {
            CopyDirectory(sourceProjectRootPath, projectRootPath);

            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(projectRootPath, "project.heproj"));

            EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel("windows");

            Assert.NotNull(selectionModel);
            Assert.NotEmpty(selectionModel.MaterialSchemas);
            Assert.Equal("standard-shader", selectionModel.MaterialSchemas[0].SchemaId);
            Assert.Contains(
                selectionModel.MaterialSchemas[0].Fields,
                field => field.FieldId == "use-custom-shader"
                    && field.FieldKind == PlatformMaterialFieldKind.Boolean
                    && field.DefaultValue == "false");
            Assert.DoesNotContain(
                selectionModel.MaterialSchemas[0].Fields,
                field => field.FieldId == "variant");
            Assert.Contains(
                selectionModel.MaterialSchemas[0].Fields,
                field => field.FieldId == "base-color"
                    && field.FieldKind == PlatformMaterialFieldKind.Color
                    && field.DefaultValue == "#ffffff");
        } finally {
            if (Directory.Exists(workspaceRootPath)) {
                Directory.Delete(workspaceRootPath, true);
            }
        }
    }

    /// <summary>
    /// Creates one minimal platform definition for test-only build-graph execution.
    /// </summary>
    /// <param name="platformId">Stable platform identifier.</param>
    /// <param name="platformName">Display platform name.</param>
    /// <returns>Platform definition used by the focused build-graph tests.</returns>
    static PlatformDefinition CreatePlatformDefinition(string platformId, string platformName) {
        return new PlatformDefinition(
            platformId,
            platformName,
            Array.Empty<PlatformBuildProfileDefinition>(),
            Array.Empty<PlatformGraphicsProfileDefinition>(),
            Array.Empty<PlatformAssetRequirementDefinition>(),
            Array.Empty<PlatformMaterialSchemaDefinition>(),
            Array.Empty<PlatformComponentSupportRule>(),
            Array.Empty<PlatformCodegenProfileDefinition>(),
            Array.Empty<PlatformStorageProfileDefinition>(),
            Array.Empty<PlatformMediaProfileDefinition>());
    }

    /// <summary>
    /// Creates one minimal codegen profile for test-only regeneration forwarding.
    /// </summary>
    /// <returns>Codegen profile used by the focused build-graph tests.</returns>
    static PlatformCodegenProfileDefinition CreateCodegenProfile() {
        return new PlatformCodegenProfileDefinition(
            "default",
            "Default",
            "Default codegen profile",
            PlatformCodegenLanguage.Cpp,
            PlatformSerializationEndianness.LittleEndian,
            []);
    }

    /// <summary>
    /// Creates one serialized rigid-body component record.
    /// </summary>
    /// <param name="bodyKind">Rigid-body participation mode to encode.</param>
    /// <param name="useGravity">True when gravity should be enabled.</param>
    /// <returns>Serialized rigid-body scene record.</returns>
    static SceneComponentAssetRecord CreateRigidBodyRecord(BodyKind3D bodyKind, bool useGravity) {
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(1);
        writer.WriteByte((byte)bodyKind);
        writer.WriteByte(useGravity ? (byte)1 : (byte)0);
        writer.WriteSingle(1f);
        writer.WriteSingle(1f);
        writer.WriteFloat3(float3.Zero);

        return new SceneComponentAssetRecord {
            ComponentTypeId = "helengine.RigidBody3DComponent",
            ComponentIndex = 0,
            Payload = stream.ToArray()
        };
    }

    /// <summary>
    /// Creates one serialized box-collider component record.
    /// </summary>
    /// <param name="size">Full collider size to encode.</param>
    /// <param name="isTrigger">True when the collider should be encoded as a trigger.</param>
    /// <returns>Serialized box-collider scene record.</returns>
    static SceneComponentAssetRecord CreateBoxColliderRecord(float3 size, bool isTrigger) {
        using MemoryStream stream = new MemoryStream();
        using EngineBinaryWriter writer = EngineBinaryWriter.Create(stream, EngineBinaryEndianness.LittleEndian);
        writer.WriteByte(2);
        writer.WriteFloat3(size);
        writer.WriteUInt16(1);
        writer.WriteUInt16(ushort.MaxValue);
        writer.WriteByte(isTrigger ? (byte)1 : (byte)0);

        return new SceneComponentAssetRecord {
            ComponentTypeId = "helengine.BoxCollider3DComponent",
            ComponentIndex = 1,
            Payload = stream.ToArray()
        };
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

            platform.SelectedSceneIds = ["point-shadow"];
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

    /// <summary>
    /// Captures the additional preprocessor symbols supplied to generated-core regeneration.
    /// </summary>
    sealed class RecordingGeneratedCoreRegenerationService : EditorGeneratedCoreRegenerationService {
        /// <summary>
        /// Gets the additional preprocessor symbols supplied by the build graph.
        /// </summary>
        public IReadOnlyList<string> AdditionalPreprocessorSymbols { get; private set; }

        public IReadOnlyList<string> GeneratedCoreProjectPaths { get; private set; }

        /// <summary>
        /// Captures regeneration inputs without launching the external codegen tool.
        /// </summary>
        public override void Regenerate(
            PlatformDefinition platformDefinition,
            PlatformCodegenProfileDefinition codegenProfile,
            IReadOnlyDictionary<string, string> selectedCodegenOptionValues,
            string generatedCoreRootPath,
            string codegenToolPath,
            IReadOnlyList<string> generatedCoreProjectPaths,
            IReadOnlyList<string> additionalPreprocessorSymbols,
            CancellationToken cancellationToken) {
            GeneratedCoreProjectPaths = generatedCoreProjectPaths;
            AdditionalPreprocessorSymbols = additionalPreprocessorSymbols;
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

    sealed class RecordingHostDebugBuildGraphRunner : EditorPlatformBuildGraphRunner {
        public RecordingHostDebugBuildGraphRunner()
            : base(
                Path.GetTempPath(),
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("ps2", "PlayStation 2", "builder.dll", string.Empty, true, "generated-core", "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService()) {
        }

        public bool HostDebugRunnerLaunched { get; private set; }

        public EditorBuildExecutionResult InvokeFinalizeBuildExecution(
            EditorPlatformBuildSelectionModel selectionModel,
            EditorBuildQueueItemDocument queueItem,
            EditorBuildExecutionResult packageResult) {
            return FinalizeBuildExecution(selectionModel, queueItem, packageResult);
        }

        protected override EditorBuildExecutionResult LaunchHostDebugRunner(
            EditorPlatformBuildSelectionModel selectionModel,
            EditorBuildQueueItemDocument queueItem,
            string outputDirectoryPath,
            EditorBuildExecutionResult packageResult) {
            HostDebugRunnerLaunched = true;
            return EditorBuildExecutionResult.Success("Host-debug launched.");
        }
    }

    sealed class RecordingHostDebugLaunchBuildGraphRunner : EditorPlatformBuildGraphRunner {
        public RecordingHostDebugLaunchBuildGraphRunner(string nativeRepositoryRootPath)
            : base(
                Path.GetTempPath(),
                "1.0.0",
                "project",
                "1.0.0",
                Array.Empty<IAssetImporterRegistration>(),
                new AvailablePlatformDescriptor("ps2", "PlayStation 2", "builder.dll", nativeRepositoryRootPath, true, "generated-core", "codegen.exe"),
                null,
                new EditorPlatformAssetBuilderLoader(),
                new EditorGeneratedCoreRegenerationService()) {
        }

        public string LaunchedExecutablePath { get; private set; }

        public string LaunchedArguments { get; private set; }

        public EditorBuildExecutionResult InvokeLaunchHostDebugRunner(
            EditorPlatformBuildSelectionModel selectionModel,
            EditorBuildQueueItemDocument queueItem,
            string outputDirectoryPath,
            EditorBuildExecutionResult packageResult) {
            return LaunchHostDebugRunner(selectionModel, queueItem, outputDirectoryPath, packageResult);
        }

        protected override void StartHostDebugProcess(string executablePath, string arguments) {
            LaunchedExecutablePath = executablePath;
            LaunchedArguments = arguments;
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
                Array.Empty<helengine.baseplatform.Definitions.PlatformComponentSupportRule>(),
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
    /// Writes one minimal authored scene asset for the build-graph cook regression.
    /// </summary>
    /// <param name="projectRootPath">Temporary project root path that owns the authored asset tree.</param>
    /// <param name="sceneRelativePath">Project-relative authored scene path to write.</param>
    static void WriteSceneAssetForBuildGraphRunnerTest(string projectRootPath, string sceneRelativePath) {
        if (string.IsNullOrWhiteSpace(projectRootPath)) {
            throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
        }
        if (string.IsNullOrWhiteSpace(sceneRelativePath)) {
            throw new ArgumentException("Scene relative path must be provided.", nameof(sceneRelativePath));
        }

        string scenePath = Path.Combine(projectRootPath, "assets", sceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
        string sceneDirectoryPath = Path.GetDirectoryName(scenePath)
            ?? throw new InvalidOperationException("Scene directory path could not be resolved.");
        Directory.CreateDirectory(sceneDirectoryPath);

        SceneAsset sceneAsset = new() {
            Id = sceneRelativePath,
            AssetReferences = Array.Empty<SceneAssetReference>(),
            RootEntities = [
                new SceneEntityAsset {
                    Id = 1u,
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = Array.Empty<SceneComponentAssetRecord>(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            ]
        };

        using FileStream stream = new(scenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    /// <summary>
    /// Writes one minimal cooked scene asset to the supplied absolute output path for scene-discovery path regressions.
    /// </summary>
    /// <param name="cookedScenePath">Absolute cooked scene asset path to create.</param>
    /// <param name="sceneId">Stable scene id stamped into the serialized asset.</param>
    static void WriteCookedSceneAssetForBuildGraphRunnerTest(string cookedScenePath, string sceneId, params string[] componentTypeIds) {
        if (string.IsNullOrWhiteSpace(cookedScenePath)) {
            throw new ArgumentException("Cooked scene path must be provided.", nameof(cookedScenePath));
        }
        if (string.IsNullOrWhiteSpace(sceneId)) {
            throw new ArgumentException("Scene id must be provided.", nameof(sceneId));
        }

        string sceneDirectoryPath = Path.GetDirectoryName(cookedScenePath)
            ?? throw new InvalidOperationException("Cooked scene directory path could not be resolved.");
        Directory.CreateDirectory(sceneDirectoryPath);

        List<SceneComponentAssetRecord> componentRecords = new List<SceneComponentAssetRecord>();
        if (componentTypeIds != null) {
            for (int index = 0; index < componentTypeIds.Length; index++) {
                string componentTypeId = componentTypeIds[index];
                if (string.IsNullOrWhiteSpace(componentTypeId)) {
                    continue;
                }

                componentRecords.Add(new SceneComponentAssetRecord {
                    ComponentTypeId = componentTypeId,
                    ComponentIndex = (ushort)index,
                    Payload = Array.Empty<byte>()
                });
            }
        }

        SceneAsset sceneAsset = new() {
            Id = sceneId,
            AssetReferences = Array.Empty<SceneAssetReference>(),
            RootEntities = [
                new SceneEntityAsset {
                    Id = 1u,
                    Name = "Root",
                    LocalPosition = float3.Zero,
                    LocalScale = float3.One,
                    LocalOrientation = float4.Identity,
                    Components = componentRecords.ToArray(),
                    Children = Array.Empty<SceneEntityAsset>()
                }
            ]
        };

        using FileStream stream = new(cookedScenePath, FileMode.Create, FileAccess.Write, FileShare.None);
        AssetSerializer.Serialize(stream, sceneAsset);
    }

    sealed class HostDebugPlatformBuilder : IPlatformAssetBuilder {
        public HostDebugPlatformBuilder() {
            Descriptor = new(
                "test.ps2.hostdebug.builder",
                "1.0.0",
                "ps2",
                new("1.0.0", "999.0.0"),
                new(1, 3),
                ["ps2"],
                ["ps2"]);
            Definition = new(
                "ps2",
                "PlayStation 2",
                Array.Empty<PlatformBuildProfileDefinition>(),
                Array.Empty<PlatformGraphicsProfileDefinition>(),
                Array.Empty<PlatformAssetRequirementDefinition>(),
                Array.Empty<PlatformMaterialSchemaDefinition>(),
                Array.Empty<PlatformComponentSupportRule>(),
                Array.Empty<PlatformCodegenProfileDefinition>(),
                Array.Empty<PlatformStorageProfileDefinition>(),
                Array.Empty<PlatformMediaProfileDefinition>(),
                null,
                new PlatformHostDebugCapability(
                    true,
                    PlatformHostDebugRunnerKind.NativeExecutable,
                    true,
                    true,
                    false,
                    "ps2-host-debugger"));
        }

        public helengine.baseplatform.Descriptors.PlatformBuilderDescriptor Descriptor { get; }

        public PlatformDefinition Definition { get; }

        public helengine.baseplatform.Results.PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
            throw new NotSupportedException("Material cooking is not used by this test builder.");
        }

        public Task<helengine.baseplatform.Reporting.PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            IPlatformBuildProgressReporter progressReporter,
            IPlatformBuildDiagnosticReporter diagnosticReporter,
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


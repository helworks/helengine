using helengine.baseplatform.Builders;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using helengine.projectfile;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the editor-owned PS2 build flow can stage the city playable demo-disc scene set without Nintendo DS scene remaps.
/// </summary>
public sealed class CityPs2DemoDiscBuildVerificationTests {
    /// <summary>
    /// Absolute path to the local city project used by this environment-backed verification.
    /// </summary>
    const string CityProjectRootPath = @"C:\dev\helprojs\city";

    /// <summary>
    /// Ensures the copied city PS2 build uses the generated boot scene, includes the playable rendering lineup, and excludes Nintendo DS scene ids.
    /// </summary>
    [Fact]
    public void Cook_WhenCityPs2BuildUsesPlayableDemoDiscSceneSet_UsesGeneratedBootSceneAndExcludesDsSceneIds() {
        string workspaceRootPath = Path.Combine(Path.GetTempPath(), "helengine-city-ps2-build-tests", Guid.NewGuid().ToString("N"));
        string projectRootPath = Path.Combine(workspaceRootPath, "project");
        string buildRootPath = Path.Combine(workspaceRootPath, "build");

        try {
            CopyDirectory(CityProjectRootPath, projectRootPath);
            ConfigurePs2BuildFromWindowsDemoDiscSelection(projectRootPath, buildRootPath);

            EditorProjectBootstrapContext bootstrap = EditorProjectBootstrapper.Create(Path.Combine(projectRootPath, ProjectFilePathResolver.CanonicalProjectFileName));
            EditorBuildConfigDocument buildConfig = bootstrap.BuildConfigService.TryLoadExisting()
                ?? throw new InvalidOperationException("Copied city build configuration was not found.");
            EditorBuildPlatformConfigDocument platformConfig = FindPlatformConfig(buildConfig, "ps2");
            EditorPlatformBuildSelectionModel selectionModel = bootstrap.ResolveSelectionModel("ps2");
            EditorBuildQueueItemFactory queueItemFactory = new EditorBuildQueueItemFactory(bootstrap.SceneCatalogService);
            EditorBuildQueueItemDocument queueItem = queueItemFactory.Create(platformConfig, selectionModel, buildRootPath);

            Assert.Equal(PlatformMenuSceneResolver.GeneratedBootSceneId, queueItem.SelectedSceneIds[0]);
            Assert.Contains(PlatformMenuSceneResolver.DesktopMainMenuSceneId, queueItem.SelectedSceneIds);
            Assert.Contains("cube_test", queueItem.SelectedSceneIds);
            Assert.Contains("scaled_cube", queueItem.SelectedSceneIds);
            Assert.Contains("colored_cube_grid", queueItem.SelectedSceneIds);
            Assert.Contains("textured_cube_grid", queueItem.SelectedSceneIds);
            Assert.Contains("axis_test", queueItem.SelectedSceneIds);
            Assert.Contains("axis_test2", queueItem.SelectedSceneIds);
            Assert.Contains("directional_shadow_plaza", queueItem.SelectedSceneIds);
            Assert.Contains("spotlight_street_slice", queueItem.SelectedSceneIds);
            Assert.DoesNotContain(queueItem.SelectedSceneIds, sceneId => sceneId.EndsWith("_ds", StringComparison.Ordinal));

            EditorGeneratedBootScenePreparationService bootScenePreparationService = new EditorGeneratedBootScenePreparationService(projectRootPath);
            bootScenePreparationService.EnsurePrepared(queueItem.PlatformId, queueItem.SelectedSceneIds);

            AvailablePlatformDescriptor platformDescriptor = bootstrap.ResolvePlatformDescriptor("ps2");
            EditorPlatformAssetBuilderLoader builderLoader = new EditorPlatformAssetBuilderLoader();
            IPlatformAssetBuilder builder = builderLoader.Load(platformDescriptor.BuilderAssemblyPath);
            EditorPlatformAssetCookService cookService = new(
                projectRootPath,
                bootstrap.RequiredEngineVersion,
                bootstrap.ProjectName,
                bootstrap.ProjectVersion,
                CreateImporters(),
                PackagedFontAssetFactory.Create());

            PlatformBuildManifest manifest = cookService.Cook(
                builder.Definition,
                queueItem.SelectedSceneIds,
                buildRootPath,
                [queueItem.PlatformId],
                builder,
                queueItem.SelectedBuildProfileId,
                queueItem.SelectedGraphicsProfileId);

            Assert.Equal(PlatformMenuSceneResolver.GeneratedBootSceneId, manifest.StartupSceneId);
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, PlatformMenuSceneResolver.GeneratedBootSceneId, StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, PlatformMenuSceneResolver.DesktopMainMenuSceneId, StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "cube_test", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "scaled_cube", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "colored_cube_grid", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "textured_cube_grid", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "axis_test", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "axis_test2", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "directional_shadow_plaza", StringComparison.Ordinal));
            Assert.Contains(manifest.Scenes, scene => string.Equals(scene.SceneId, "spotlight_street_slice", StringComparison.Ordinal));
            Assert.DoesNotContain(manifest.Scenes, scene => scene.SceneId.EndsWith("_ds", StringComparison.Ordinal));
        } finally {
            if (Directory.Exists(workspaceRootPath)) {
                Directory.Delete(workspaceRootPath, true);
            }
        }
    }

    /// <summary>
    /// Copies the Windows playable demo-disc scene selection into the copied PS2 build configuration.
    /// </summary>
    /// <param name="projectRootPath">Copied city project root.</param>
    /// <param name="buildRootPath">Temporary build output root.</param>
    static void ConfigurePs2BuildFromWindowsDemoDiscSelection(string projectRootPath, string buildRootPath) {
        if (string.IsNullOrWhiteSpace(projectRootPath)) {
            throw new ArgumentException("Project root path must be provided.", nameof(projectRootPath));
        }
        if (string.IsNullOrWhiteSpace(buildRootPath)) {
            throw new ArgumentException("Build root path must be provided.", nameof(buildRootPath));
        }

        EditorBuildConfigService buildConfigService = new EditorBuildConfigService(projectRootPath);
        EditorBuildConfigDocument buildConfig = buildConfigService.TryLoadExisting()
            ?? throw new InvalidOperationException("Copied city build configuration was not found.");
        EditorBuildPlatformConfigDocument windowsPlatform = FindPlatformConfig(buildConfig, "windows");
        EditorBuildPlatformConfigDocument ps2Platform = FindPlatformConfig(buildConfig, "ps2");

        ps2Platform.SelectedSceneIds = [];
        for (int index = 0; index < windowsPlatform.SelectedSceneIds.Count; index++) {
            ps2Platform.SelectedSceneIds.Add(windowsPlatform.SelectedSceneIds[index]);
        }

        ps2Platform.SceneOrders = [];
        for (int index = 0; index < windowsPlatform.SceneOrders.Count; index++) {
            EditorBuildSceneOrderDocument sourceSceneOrder = windowsPlatform.SceneOrders[index];
            ps2Platform.SceneOrders.Add(new EditorBuildSceneOrderDocument {
                SceneId = sourceSceneOrder.SceneId,
                OrderNumber = sourceSceneOrder.OrderNumber
            });
        }

        ps2Platform.OutputDirectoryPath = buildRootPath.Replace('\\', '/');
        ps2Platform.SelectedBuildProfileId = "ps2-default";
        ps2Platform.SelectedGraphicsProfileId = "ps2-standard-forward";
        ps2Platform.SelectedCodegenProfileId = "default";
        ps2Platform.SelectedStorageProfileId = "disc-layout";
        ps2Platform.SelectedMediaProfileId = "ps2-install-tree";
        buildConfigService.Save(buildConfig);
    }

    /// <summary>
    /// Finds one platform build configuration entry by platform id.
    /// </summary>
    /// <param name="buildConfig">Build configuration document to inspect.</param>
    /// <param name="platformId">Platform identifier to find.</param>
    /// <returns>Matching platform build configuration.</returns>
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
    /// Creates the permissive importer set used by this environment-backed city verification.
    /// </summary>
    /// <returns>Importer registrations that accept the city source asset extensions used by the copied project.</returns>
    static IReadOnlyList<IAssetImporterRegistration> CreateImporters() {
        return [
            new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png", ".jpg", ".jpeg", ".tga", ".bmp", ".gif", ".dds"]),
            new TextImporterRegistration("text", new TextImporter(), [".txt", ".json", ".shader", ".material"]),
            new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf", ".otf"]),
            new ModelImporterRegistration("test-model", new TestModelImporter(), [".fbx", ".obj", ".gltf", ".glb", ".dae", ".3ds", ".x"])
        ];
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
}

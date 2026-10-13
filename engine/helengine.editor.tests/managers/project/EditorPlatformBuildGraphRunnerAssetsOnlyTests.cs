using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Requests;
using helengine.editor.tests.testing;
using helengine.platforms;

namespace helengine.editor.tests.managers.project;

/// <summary>
/// Verifies the assets-only build path that editor Play uses to cook a scene into a stable workspace without codegen or a native build.
/// </summary>
[Collection(EditorBuildCacheEnvironmentCollection.Name)]
public sealed class EditorPlatformBuildGraphRunnerAssetsOnlyTests : IDisposable {
    /// <summary>
    /// Assembly path under which the test builder loader serves the recording builder.
    /// </summary>
    const string BuilderAssemblyPath = "play-builder.dll";

    /// <summary>
    /// Original stable cache root inherited by the test process.
    /// </summary>
    readonly string OriginalCacheRoot = Environment.GetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT");

    /// <summary>
    /// Original stable build configuration inherited by the test process.
    /// </summary>
    readonly string OriginalConfiguration = Environment.GetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION");

    /// <summary>
    /// Original stable build profile inherited by the test process.
    /// </summary>
    readonly string OriginalProfile = Environment.GetEnvironmentVariable("HELENGINE_BUILD_PROFILE");

    /// <summary>
    /// Original workspace root inherited by the test process.
    /// </summary>
    readonly string OriginalWorkspaceRoot = Environment.GetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT");

    /// <summary>
    /// Visible test-owned scratch root holding the authored project and the build workspaces; deleted in <see cref="Dispose"/>.
    /// </summary>
    readonly string RootPath;

    /// <summary>
    /// Authored project root containing the single test scene.
    /// </summary>
    readonly string ProjectRootPath;

    /// <summary>
    /// Creates the scratch project with one material-free scene and routes build workspaces beneath the scratch root.
    /// </summary>
    public EditorPlatformBuildGraphRunnerAssetsOnlyTests() {
        RootPath = Path.Combine("C:/dev/helworks/builds/helengine/play-tests", Guid.NewGuid().ToString("N"));
        ProjectRootPath = Path.Combine(RootPath, "project");
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", null);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", null);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", null);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", Path.Combine(RootPath, "workspaces"));
        WriteSceneAsset(ProjectRootPath, "Scenes/MainMenu.helen");
        ShaderBackendRegistry shaderBackendRegistry = new ShaderBackendRegistry();
        shaderBackendRegistry.Register(new helengine.directx11.DirectX11ShaderBackend());
    }

    /// <summary>
    /// Restores the environment and deletes the scratch root.
    /// </summary>
    public void Dispose() {
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_CACHE_ROOT", OriginalCacheRoot);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_CONFIGURATION", OriginalConfiguration);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_PROFILE", OriginalProfile);
        Environment.SetEnvironmentVariable("HELENGINE_BUILD_WORKSPACE_ROOT", OriginalWorkspaceRoot);
        if (Directory.Exists(RootPath)) {
            Directory.Delete(RootPath, true);
        }
    }

    /// <summary>
    /// Ensures a platform that does not opt in to Play is rejected before any cooking or packaging happens.
    /// </summary>
    [Fact]
    public void ExecuteAssetsOnly_WhenPlatformDoesNotSupportPlay_ReturnsFailureWithoutBuilding() {
        RecordingPlayPlatformAssetBuilder builder = new(PlatformPlayerCapability.CreateUnsupported());
        EditorPlatformBuildGraphRunner runner = CreateRunner(builder);

        EditorPlayCookResult result = runner.ExecuteAssetsOnly(CreateQueueItem());

        Assert.False(result.Succeeded);
        Assert.Contains("windows", result.Message, StringComparison.Ordinal);
        Assert.Empty(builder.Requests);
        Assert.Equal(string.Empty, result.ContentRootPath);
    }

    /// <summary>
    /// Ensures the Play cook packages with the assets-only scope, hands the builder no generated-core or native-cache roots,
    /// and reports the builder's staging folder plus the startup scene's cooked path.
    /// </summary>
    [Fact]
    public void ExecuteAssetsOnly_PassesAssetsOnlyScopeAndReturnsStagingContentRoot() {
        RecordingPlayPlatformAssetBuilder builder = new(new PlatformPlayerCapability(true, "x.Adapter", "staging", 60d, PlayerInputDevices.Gamepad));
        EditorPlatformBuildGraphRunner runner = CreateRunner(builder);

        EditorPlayCookResult result = runner.ExecuteAssetsOnly(CreateQueueItem());

        Assert.True(result.Succeeded, result.Message);
        PlatformBuildRequest request = Assert.Single(builder.Requests);
        Assert.Equal(PlatformBuildExecutionScope.AssetsOnly, request.ExecutionScope);
        Assert.Equal(string.Empty, request.GeneratedCoreCppRootPath);
        Assert.Equal(string.Empty, request.NativeObjectCacheRoot);
        Assert.Equal(Path.Combine(request.WorkingRoot, "staging"), result.ContentRootPath);
        Assert.Equal("MainMenu", result.StartupSceneId);
        Assert.Equal("cooked/scenes/mainmenu.hasset", result.StartupSceneCookedRelativePath);
    }

    /// <summary>
    /// Ensures repeated Play cooks reuse one workspace and keep the builder working root, where the platform keeps
    /// its fingerprint store and staged content, while still refreshing the cooked tree.
    /// </summary>
    [Fact]
    public void ExecuteAssetsOnly_WhenRunTwice_ReusesWorkspaceAndPreservesBuilderWorkingRoot() {
        RecordingPlayPlatformAssetBuilder builder = new(new PlatformPlayerCapability(true, "x.Adapter", "staging", 60d, PlayerInputDevices.Gamepad));
        EditorPlatformBuildGraphRunner runner = CreateRunner(builder);

        EditorPlayCookResult firstResult = runner.ExecuteAssetsOnly(CreateQueueItem());
        Assert.True(firstResult.Succeeded, firstResult.Message);
        string builderWorkingRootPath = builder.Requests[0].WorkingRoot;
        string sentinelPath = Path.Combine(builderWorkingRootPath, "fingerprints.bin");
        File.WriteAllText(sentinelPath, "persistent");
        string cookRootPath = Path.Combine(Path.GetDirectoryName(builderWorkingRootPath), "cooked");
        string staleCookPath = Path.Combine(cookRootPath, "stale.bin");
        File.WriteAllText(staleCookPath, "stale");

        EditorPlayCookResult secondResult = runner.ExecuteAssetsOnly(CreateQueueItem());

        Assert.True(secondResult.Succeeded, secondResult.Message);
        Assert.Equal(2, builder.Requests.Count);
        Assert.Equal(builderWorkingRootPath, builder.Requests[1].WorkingRoot);
        Assert.Equal(firstResult.ContentRootPath, secondResult.ContentRootPath);
        Assert.True(File.Exists(sentinelPath));
        Assert.False(File.Exists(staleCookPath));
    }

    /// <summary>
    /// Creates a runner for the scratch project whose builder loader serves the supplied recording builder.
    /// </summary>
    /// <param name="builder">Recording builder returned for the Windows descriptor.</param>
    /// <returns>Runner configured for the scratch project.</returns>
    EditorPlatformBuildGraphRunner CreateRunner(IPlatformAssetBuilder builder) {
        return new EditorPlatformBuildGraphRunner(
            ProjectRootPath,
            "1.0.0",
            "project",
            "1.0.0",
            Array.Empty<IAssetImporterRegistration>(),
            new AvailablePlatformDescriptor("windows", "Windows", BuilderAssemblyPath, string.Empty, true, "generated-core"),
            PackagedFontAssetFactory.Create(),
            new TestEditorPlatformAssetBuilderLoader(new Dictionary<string, IPlatformAssetBuilder> {
                [BuilderAssemblyPath] = builder
            }),
            new EditorGeneratedCoreRegenerationService(),
            null,
            TestGeneratedAssetGraph.CreateShaderLibrary(),
            new FixedCodegenToolProvider());
    }

    /// <summary>
    /// Creates the Play queue item that cooks the single test scene with the debug profile.
    /// </summary>
    /// <returns>Queue item selecting the test scene.</returns>
    EditorBuildQueueItemDocument CreateQueueItem() {
        return new EditorBuildQueueItemDocument {
            QueueItemId = "play",
            PlatformId = "windows",
            OutputDirectoryPath = Path.Combine(RootPath, "output"),
            SelectedSceneIds = ["MainMenu"],
            SelectedBuildProfileId = "debug",
            SelectedBuildOptionValues = new Dictionary<string, string>(),
            SelectedGraphicsOptionValues = new Dictionary<string, string>(),
            SelectedCodegenOptionValues = new Dictionary<string, string>()
        };
    }

    /// <summary>
    /// Writes one material-free authored scene with a single root entity.
    /// </summary>
    /// <param name="projectRootPath">Authored project root.</param>
    /// <param name="sceneRelativePath">Scene path relative to the project's assets folder.</param>
    static void WriteSceneAsset(string projectRootPath, string sceneRelativePath) {
        string scenePath = Path.Combine(projectRootPath, "assets", sceneRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(scenePath));
        byte[] hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(sceneRelativePath));
        SceneAsset sceneAsset = new() {
            Id = sceneRelativePath,
            AuthoringAssetId = Convert.ToHexString(hash)[..32].ToLowerInvariant(),
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
}

using helengine.editor.tests.testing;
using Xunit.Sdk;

namespace helengine.editor.tests;

/// <summary>
/// Verifies the authored city render-only slope scene can pass through the shared Windows scene-component packaging path.
/// </summary>
public sealed class CityRenderOnlySlopeScenePackagingTests : IDisposable {
    /// <summary>
    /// Absolute city project root used by the shared packaging path.
    /// </summary>
    const string CityProjectRootPath = @"C:\dev\helprojs\city";

    /// <summary>
    /// Absolute authored scene path for the focused render-only slope validation scene.
    /// </summary>
    const string RenderOnlySlopeScenePath = @"C:\dev\helprojs\city\assets\Scenes\physics\test_scene_render_only_slope.helen";

    /// <summary>
    /// Temporary build root used while packaging the scene components under test.
    /// </summary>
    readonly string BuildRootPath;

    /// <summary>
    /// Initializes one isolated temporary build root for the focused packaging probe.
    /// </summary>
    public CityRenderOnlySlopeScenePackagingTests() {
        BuildRootPath = Path.Combine(Path.GetTempPath(), "helengine-city-render-only-slope-packaging", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(BuildRootPath);
    }

    /// <summary>
    /// Removes the temporary build root after the focused packaging probe completes.
    /// </summary>
    public void Dispose() {
        if (Directory.Exists(BuildRootPath)) {
            Directory.Delete(BuildRootPath, true);
        }
    }

    /// <summary>
    /// Ensures every component in the authored render-only slope scene can pass through the shared Windows packaging transform.
    /// </summary>
    [Fact]
    public void Render_only_slope_scene_components_package_for_windows() {
        SceneAsset sceneAsset = LoadSceneAsset(RenderOnlySlopeScenePath);
        using Core core = CreateInitializedCore();
        SceneComponentPackagingTransformService service = CreatePackagingService();

        VerifyEntityComponentsPackage(service, sceneAsset.RootEntities, string.Empty);
    }

    /// <summary>
    /// Creates one Windows packaging transform service rooted to the real city project content tree.
    /// </summary>
    /// <returns>Configured component packaging transform service.</returns>
    SceneComponentPackagingTransformService CreatePackagingService() {
        ContentManager contentManager = new ContentManager(CityProjectRootPath);
        AssetImportManager assetImportManager = new AssetImportManager(CityProjectRootPath, contentManager);
        assetImportManager.RegisterFontImporter(new FontImporterRegistration("test-font", new TestFontImporter(), [".ttf"]));
        assetImportManager.RegisterTextureImporter(new TextureImporterRegistration("test-texture", new TestTextureImporter(), [".png"]));
        EditorFileSystemModelResolver fileSystemModelResolver = new EditorFileSystemModelResolver(assetImportManager);

        return new SceneComponentPackagingTransformService(
            Path.Combine(CityProjectRootPath, "assets"),
            contentManager,
            assetImportManager,
            fileSystemModelResolver,
            new List<string>(),
            new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            "windows",
            null,
            string.Empty,
            string.Empty,
            null,
            null,
            null,
            new NoOpTextComponentSpriteBakeService(),
            null);
    }

    /// <summary>
    /// Creates one initialized core instance that mirrors the runtime prerequisites expected by camera-component construction during packaging.
    /// </summary>
    /// <returns>Initialized core instance.</returns>
    static Core CreateInitializedCore() {
        Core core = new Core(new CoreInitializationOptions {
            ContentRootPath = Path.Combine(CityProjectRootPath, "assets")
        });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), null, new PlatformInfo("test", "test-version"));
        return core;
    }

    /// <summary>
    /// Loads one authored scene asset from disk through the shared scene serializer.
    /// </summary>
    /// <param name="scenePath">Absolute path to the authored scene asset.</param>
    /// <returns>Deserialized scene asset.</returns>
    static SceneAsset LoadSceneAsset(string scenePath) {
        if (string.IsNullOrWhiteSpace(scenePath)) {
            throw new ArgumentException("A scene path must be provided.", nameof(scenePath));
        }

        using FileStream stream = File.OpenRead(scenePath);
        return Assert.IsType<SceneAsset>(AssetSerializer.Deserialize(stream));
    }

    /// <summary>
    /// Verifies every component beneath the supplied entity subtree packages successfully.
    /// </summary>
    /// <param name="service">Packaging service that rewrites component payloads.</param>
    /// <param name="entities">Entity subtree to inspect.</param>
    /// <param name="entityPath">Current entity path prefix used for failure diagnostics.</param>
    void VerifyEntityComponentsPackage(
        SceneComponentPackagingTransformService service,
        IReadOnlyList<SceneEntityAsset> entities,
        string entityPath) {
        if (service == null) {
            throw new ArgumentNullException(nameof(service));
        } else if (entities == null) {
            throw new ArgumentNullException(nameof(entities));
        }

        for (int entityIndex = 0; entityIndex < entities.Count; entityIndex++) {
            SceneEntityAsset entity = entities[entityIndex];
            string currentEntityPath = BuildEntityPath(entityPath, entity.Name, entityIndex);
            VerifyComponentRecordsPackage(service, entity.Components, currentEntityPath);
            VerifyEntityComponentsPackage(service, entity.Children, currentEntityPath);
        }
    }

    /// <summary>
    /// Verifies every component record on one entity packages successfully.
    /// </summary>
    /// <param name="service">Packaging service that rewrites component payloads.</param>
    /// <param name="components">Component records to inspect.</param>
    /// <param name="entityPath">Diagnostic entity path that owns the component records.</param>
    void VerifyComponentRecordsPackage(
        SceneComponentPackagingTransformService service,
        IReadOnlyList<SceneComponentAssetRecord> components,
        string entityPath) {
        if (service == null) {
            throw new ArgumentNullException(nameof(service));
        } else if (components == null) {
            throw new ArgumentNullException(nameof(components));
        } else if (string.IsNullOrWhiteSpace(entityPath)) {
            throw new ArgumentException("An entity path must be provided.", nameof(entityPath));
        }

        for (int componentIndex = 0; componentIndex < components.Count; componentIndex++) {
            SceneComponentAssetRecord record = components[componentIndex];
            try {
                service.TryTransform(record, BuildRootPath, out _);
            } catch (Exception exception) {
                throw new XunitException(
                    $"Packaging failed for entity '{entityPath}', component #{componentIndex}, type '{record.ComponentTypeId}', payload length '{record.Payload?.Length ?? 0}': {exception}");
            }
        }
    }

    /// <summary>
    /// Builds one stable entity path segment for failure diagnostics.
    /// </summary>
    /// <param name="parentPath">Parent entity path prefix when present.</param>
    /// <param name="entityName">Current entity display name.</param>
    /// <param name="entityIndex">Current entity index within the parent collection.</param>
    /// <returns>Diagnostic entity path.</returns>
    static string BuildEntityPath(string parentPath, string entityName, int entityIndex) {
        string currentSegment = string.IsNullOrWhiteSpace(entityName)
            ? $"[{entityIndex}]"
            : entityName + "[" + entityIndex + "]";
        if (string.IsNullOrWhiteSpace(parentPath)) {
            return currentSegment;
        }

        return parentPath + "/" + currentSegment;
    }

    /// <summary>
    /// Provides one deterministic no-op text-sprite bake result for the shared packaging service dependency graph.
    /// </summary>
    sealed class NoOpTextComponentSpriteBakeService : ITextComponentSpriteBakeService {
        /// <summary>
        /// Returns one deterministic generated texture bake result for the supplied request.
        /// </summary>
        /// <param name="request">Bake request issued by the transform service.</param>
        /// <returns>Generated bake result.</returns>
        public TextComponentSpriteBakeResult Bake(TextComponentSpriteBakeRequest request) {
            if (request == null) {
                throw new ArgumentNullException(nameof(request));
            }

            return new TextComponentSpriteBakeResult(
                new TextureAsset {
                    Id = "generated:text-sprite",
                    Width = 8,
                    Height = 8,
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8,
                    Colors = new byte[8 * 8 * 4]
                },
                new TextureAssetProcessorSettings {
                    ColorFormat = TextureAssetColorFormat.Rgba32,
                    AlphaPrecision = TextureAssetAlphaPrecision.A8
                },
                "city-render-only-slope");
        }
    }
}

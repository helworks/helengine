using helengine.directx11;
using helengine.vulkan;

namespace helengine.editor.tests.testing;

/// <summary>
/// Explicit test composition root for one generated-asset graph. A fixture owns
/// its graph and must dispose it before disposing the associated core.
/// </summary>
public sealed class TestGeneratedAssetGraph : IDisposable {
    readonly Core CoreValue;
    readonly EditorSceneEntityIdAllocator SceneEntityIdAllocatorValue;

    public GeneratedAssetProviderRegistry Registry { get; }
    public EngineGeneratedModelCache ModelCache { get; }
    public EngineGeneratedMaterialCache MaterialCache { get; }
    public EditorBuiltInShaderAssetLibrary ShaderLibrary { get; }
    public EditorSessionRendererResources RendererResources { get; }
    public ObjectManager ObjectManager => CoreValue.ObjectManager;

    public TestGeneratedAssetGraph(Core core) {
        CoreValue = core ?? throw new ArgumentNullException(nameof(core));
        if (core.RenderManager3D == null) {
            throw new InvalidOperationException("An initialized renderer is required for generated-asset tests.");
        }

        ShaderBackendRegistry backendRegistry = new ShaderBackendRegistry();
        backendRegistry.Register(new DirectX11ShaderBackend());
        backendRegistry.Register(new VulkanShaderBackend());
        ShaderLibrary = new EditorBuiltInShaderAssetLibrary(backendRegistry);
        ModelCache = new EngineGeneratedModelCache(core);
        MaterialCache = new EngineGeneratedMaterialCache(core, ShaderLibrary);
        SceneEntityIdAllocatorValue = (core as EditorCore)?.SceneEntityIdAllocator ?? new EditorSceneEntityIdAllocator();
        IEntityFactory entityFactory = core.EntityFactory ?? new EditorEntityFactory(core, SceneEntityIdAllocatorValue);
        RendererResources = new EditorSessionRendererResources(core.RenderManager3D, core.RenderManager2D, core.ObjectManager, entityFactory, SceneEntityIdAllocatorValue, core.Input, () => core.FrameDeltaSeconds, core is EditorCore editorCore ? editorCore.DefaultFontAssetForEditor : null);
        Registry = new GeneratedAssetProviderRegistry();
    }

    public RuntimeModel GetRuntimeModel(string assetId) {
        return ModelCache.GetRuntimeModel(assetId);
    }

    public RuntimeMaterial GetRuntimeMaterial(string assetId) {
        return MaterialCache.GetRuntimeMaterial(assetId);
    }

    public EngineGeneratedAssetProvider CreateProvider() {
        return new EngineGeneratedAssetProvider(ModelCache, MaterialCache);
    }

    public ShaderAsset LoadShaderAsset(ShaderCompileTarget target, string shaderFileName) {
        return ShaderLibrary.Load(target, shaderFileName);
    }

    public EditorSceneCreationService CreateSceneCreationService() {
        if (CoreValue.EntityFactory == null) {
            throw new InvalidOperationException("EntityFactory must be initialized for generated scene tests.");
        }
        return new EditorSceneCreationService(CoreValue.EntityFactory, CoreValue.ObjectManager, ModelCache, MaterialCache, RendererResources);
    }

    public void Dispose() {
        Registry.Dispose();
        RendererResources.Dispose();
        MaterialCache.Dispose();
        ModelCache.Dispose();
        ShaderLibrary.Dispose();
    }

    /// <summary>Creates a standalone shader library for renderer-only factory tests.</summary>
    public static EditorBuiltInShaderAssetLibrary CreateShaderLibrary() {
        ShaderBackendRegistry backendRegistry = new ShaderBackendRegistry();
        backendRegistry.Register(new DirectX11ShaderBackend());
        backendRegistry.Register(new VulkanShaderBackend());
        return new EditorBuiltInShaderAssetLibrary(backendRegistry);
    }
}

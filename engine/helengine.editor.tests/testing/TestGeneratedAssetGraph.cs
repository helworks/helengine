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
    readonly bool OwnsInteractionServices;

    public GeneratedAssetProviderRegistry Registry { get; }
    public Core OwnerCore => CoreValue;
    public EngineGeneratedModelCache ModelCache { get; }
    public EngineGeneratedMaterialCache MaterialCache { get; }
    public EditorBuiltInShaderAssetLibrary ShaderLibrary { get; }
    public EditorSessionRendererResources RendererResources { get; }
    public EditorSessionInteractionServices InteractionServices { get; }
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
        InteractionServices = core is EditorCore editorCore && editorCore.SessionInteractionServices != null
            ? editorCore.SessionInteractionServices
            : core.SessionInteractionGraph as EditorSessionInteractionServices ?? new EditorSessionInteractionServices();
        OwnsInteractionServices = core.SessionInteractionGraph == null
            && (core is not EditorCore editorCoreWithInteraction
                || editorCoreWithInteraction.SessionInteractionServices == null);
        if (OwnsInteractionServices) {
            core.SessionInteractionGraph = InteractionServices;
        }
        RendererResources = new EditorSessionRendererResources(core.RenderManager3D, core.RenderManager2D, core.ObjectManager, entityFactory, SceneEntityIdAllocatorValue, core.Input, () => core.FrameDeltaSeconds, core is EditorCore editorCoreWithFont ? editorCoreWithFont.DefaultFontAssetForEditor : null, InteractionServices);
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
        List<Exception> failures = new List<Exception>();
        DisposeOwned(Registry, failures);
        DisposeOwned(RendererResources, failures);
        DisposeOwned(MaterialCache, failures);
        DisposeOwned(ModelCache, failures);
        DisposeOwned(ShaderLibrary, failures);
        if (OwnsInteractionServices && ReferenceEquals(CoreValue.SessionInteractionGraph, InteractionServices)) {
            CoreValue.SessionInteractionGraph = null;
        }
        if (OwnsInteractionServices) {
            // Clear the borrowed core slot before releasing the graph. This
            // leaves a failed interaction disposal retryable without exposing
            // a disposed graph through the owner core.
            DisposeOwned(InteractionServices, failures);
        }

        if (failures.Count != 0) {
            throw failures.Count == 1
                ? failures[0]
                : new AggregateException("Generated asset graph disposal failed.", failures);
        }
    }

    static void DisposeOwned(IDisposable disposable, List<Exception> failures) {
        if (disposable == null) {
            return;
        }

        try {
            disposable.Dispose();
        } catch (Exception exception) {
            failures.Add(exception);
        }
    }

    /// <summary>Creates a standalone shader library for renderer-only factory tests.</summary>
    public static EditorBuiltInShaderAssetLibrary CreateShaderLibrary() {
        ShaderBackendRegistry backendRegistry = new ShaderBackendRegistry();
        backendRegistry.Register(new DirectX11ShaderBackend());
        backendRegistry.Register(new VulkanShaderBackend());
        return new EditorBuiltInShaderAssetLibrary(backendRegistry);
    }
}

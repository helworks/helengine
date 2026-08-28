using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies that generated asset graphs are isolated by owner and remain usable
/// when another graph is disposed.
/// </summary>
public sealed class GeneratedSessionIsolationBehaviorTests {
    [Fact]
    public void SeparateGraphs_DoNotShareGeneratedCachesOrProviderEntries() {
        Core coreA = CreateCore();
        Core coreB = CreateCore();
        using TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        using TestGeneratedAssetGraph graphB = new TestGeneratedAssetGraph(coreB);
        EngineGeneratedModelCache modelCacheA = graphA.ModelCache;
        EngineGeneratedModelCache modelCacheB = graphB.ModelCache;
        EngineGeneratedMaterialCache materialCacheA = graphA.MaterialCache;
        EngineGeneratedMaterialCache materialCacheB = graphB.MaterialCache;
        GeneratedAssetProviderRegistry registryA = graphA.Registry;
        GeneratedAssetProviderRegistry registryB = graphB.Registry;
        registryA.Register(new EngineGeneratedAssetProvider(modelCacheA, materialCacheA));
        registryB.Register(new EngineGeneratedAssetProvider(modelCacheB, materialCacheB));

        AssetBrowserEntry modelEntry = AssetBrowserEntry.CreateGeneratedAsset(
            "Cube",
            EngineGeneratedAssetProvider.CubeRelativePath,
            AssetEntryKind.Model,
            EngineGeneratedAssetProvider.ProviderIdValue,
            EngineGeneratedModelCache.CubeAssetId);
        RuntimeModel modelA = registryA.ResolveRuntimeModel(modelEntry);
        RuntimeModel modelB = registryB.ResolveRuntimeModel(modelEntry);

        Assert.NotSame(modelA, modelB);
        Assert.Same(modelA, modelCacheA.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId));
        Assert.Same(modelB, modelCacheB.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId));

        registryA.Dispose();
        materialCacheA.Dispose();
        modelCacheA.Dispose();
        graphA.ShaderLibrary.Dispose();

        RuntimeModel modelBAfterAClosed = modelCacheB.GetRuntimeModel(EngineGeneratedModelCache.CubeAssetId);
        Assert.Same(modelB, modelBAfterAClosed);
        List<AssetBrowserEntry> entriesB = new List<AssetBrowserEntry>();
        registryB.Register(new TestGeneratedAssetProvider(
            "project-b",
            new[] { AssetBrowserEntry.CreateGeneratedDirectory("B", "B", "project-b") },
            new TestRuntimeModel()));
        registryB.LoadEntries(string.Empty, entriesB);
        Assert.Contains(entriesB, entry => entry.ProviderId == "project-b");

        coreA.Dispose();
        coreB.Dispose();
    }

    static ShaderBackendRegistry CreateBackends() {
        ShaderBackendRegistry registry = new ShaderBackendRegistry();
        registry.Register(new DirectX11ShaderBackend());
        registry.Register(new VulkanShaderBackend());
        return registry;
    }

    static Core CreateCore() {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new FakeContentStreamSource() });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }
}

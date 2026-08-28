using System.Reflection;
using helengine.directx11;
using helengine.editor.tests.testing;
using helengine.projectfile;
using helengine.ui;
using helengine.vulkan;
using Xunit;

namespace helengine.editor.tests;

/// <summary>
/// Verifies that generated asset graphs are isolated by owner and remain usable
/// when another graph is disposed.
/// </summary>
public sealed class GeneratedSessionIsolationBehaviorTests {
    [Fact]
    public void ActualEditorSessions_KeepRendererGraphsIndependentAfterSessionADisposes() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        EditorSession sessionA = null;
        EditorSession sessionB = null;

        try {
            sessionA = CreateActualEditorSession(projectRootA);
            sessionB = CreateActualEditorSession(projectRootB);

            EditorSessionRendererResources rendererResourcesA = GetPrivateField<EditorSessionRendererResources>(sessionA, "rendererResources");
            EditorSessionRendererResources rendererResourcesB = GetPrivateField<EditorSessionRendererResources>(sessionB, "rendererResources");
            GeneratedAssetProviderRegistry registryA = GetPrivateField<GeneratedAssetProviderRegistry>(sessionA, "generatedAssetProviderRegistry");
            GeneratedAssetProviderRegistry registryB = GetPrivateField<GeneratedAssetProviderRegistry>(sessionB, "generatedAssetProviderRegistry");
            EngineGeneratedModelCache modelCacheA = GetPrivateField<EngineGeneratedModelCache>(sessionA, "generatedModelCache");
            EngineGeneratedModelCache modelCacheB = GetPrivateField<EngineGeneratedModelCache>(sessionB, "generatedModelCache");

            Assert.NotSame(rendererResourcesA, rendererResourcesB);
            Assert.NotSame(rendererResourcesA.RenderManager3D, rendererResourcesB.RenderManager3D);
            Assert.NotSame(registryA, registryB);
            Assert.NotSame(modelCacheA, modelCacheB);

            AssetBrowserEntry cubeEntry = AssetBrowserEntry.CreateGeneratedAsset(
                "Cube",
                EngineGeneratedAssetProvider.CubeRelativePath,
                AssetEntryKind.Model,
                EngineGeneratedAssetProvider.ProviderIdValue,
                EngineGeneratedModelCache.CubeAssetId);
            RuntimeModel modelA = registryA.ResolveRuntimeModel(cubeEntry);
            RuntimeModel modelB = registryB.ResolveRuntimeModel(cubeEntry);
            Assert.NotSame(modelA, modelB);

            SceneSaveService saveServiceB = GetPrivateField<SceneSaveService>(sessionB, "SceneSaveService");
            string savedScenePath = Path.Combine(projectRootB, "assets", "Scenes", "Isolation.helen");
            saveServiceB.Save(savedScenePath);

            sessionA.Dispose();
            sessionA = null;

            Assert.Same(modelB, registryB.ResolveRuntimeModel(cubeEntry));
            Assert.True(File.Exists(savedScenePath));
        } finally {
            sessionA?.Dispose();
            sessionB?.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

    [Fact]
    public void LiveAuthoringSessions_KeepResolverGraphsIndependentAfterSessionADisposes() {
        string projectRootA = CreateProjectRoot();
        string projectRootB = CreateProjectRoot();
        Core coreA = CreateCore(projectRootA);
        Core coreB = CreateCore(projectRootB);
        TestGeneratedAssetGraph graphA = new TestGeneratedAssetGraph(coreA);
        TestGeneratedAssetGraph graphB = new TestGeneratedAssetGraph(coreB);
        graphA.Registry.Register(graphA.CreateProvider());
        graphB.Registry.Register(graphB.CreateProvider());

        try {
            using EditorProjectAuthoringSession sessionA = CreateAuthoringSession(projectRootA, graphA);
            using EditorProjectAuthoringSession sessionB = CreateAuthoringSession(projectRootB, graphB);
            ISceneAssetReferenceResolver resolverA = sessionA.CreateSceneAssetReferenceResolver();
            ISceneAssetReferenceResolver resolverB = sessionB.CreateSceneAssetReferenceResolver();

            RuntimeModel modelA = resolverA.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());
            RuntimeModel modelB = resolverB.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());

            Assert.NotSame(resolverA, resolverB);
            Assert.NotSame(modelA, modelB);

            sessionA.Dispose();
            graphA.Dispose();
            coreA.Dispose();

            RuntimeModel modelBAfterAClosed = resolverB.ResolveModel(global::helengine.EngineSceneAssetReferenceFactory.CreateCubeModel());
            Assert.Same(modelB, modelBAfterAClosed);
        } finally {
            graphA.Dispose();
            graphB.Dispose();
            coreA.Dispose();
            coreB.Dispose();
            DeleteProjectRoot(projectRootA);
            DeleteProjectRoot(projectRootB);
        }
    }

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

    static Core CreateCore(string projectRootPath) {
        Core core = new Core(new CoreInitializationOptions { ContentStreamSource = new HostFileSystemContentStreamSource(projectRootPath) });
        core.Initialize(new TestRenderManager3D(), new TestRenderManager2D(), new TestInputBackend(), new PlatformInfo("test", "test-version"));
        return core;
    }

    static EditorSession CreateActualEditorSession(string projectRootPath) {
        EditorCore core = new EditorCore(new Project {
            Name = "Generated isolation",
            Path = projectRootPath
        });
        try {
            ShaderBackendRegistry shaderBackendRegistry = CreateBackends();
            EditorSession session = new EditorSession(
                core,
                Path.Combine(projectRootPath, "project.heproj"),
                new EditorPreferencesSettings(new EditorUiScaleSettings(EditorUiScaleMode.Override, 100), EditorThemeCatalog.DefaultThemeId),
                EditorUiMetrics.Default,
                CreateEditorFont(),
                CreateEditorFont(),
                TestDirectX11RenderManager3D.Create(),
                new TestRenderManager2D(),
                new TestInputBackend(),
                1280,
                720,
                CreateToolbarIcons(),
                CreateTexture(),
                Array.Empty<IAssetImporterRegistration>(),
                () => projectRootPath,
                shaderBackendRegistry);
            return session;
        } catch {
            core.Dispose();
            throw;
        }
    }

    static EditorViewportToolbarIconSet CreateToolbarIcons() {
        return new EditorViewportToolbarIconSet(
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture(),
            CreateTexture());
    }

    static RuntimeTexture CreateTexture() {
        return new TestRuntimeTexture {
            Width = 16,
            Height = 16
        };
    }

    static FontAsset CreateEditorFont() {
        Dictionary<char, FontChar> characters = new Dictionary<char, FontChar>();
        const string glyphs = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 .,:;!?+-_[]()/'\\\\=<>";
        for (int index = 0; index < glyphs.Length; index++) {
            char glyph = glyphs[index];
            if (!characters.ContainsKey(glyph)) {
                float width = glyph == ' ' ? 4f : 8f;
                characters.Add(glyph, new FontChar(new float4(0f, 0f, width, 12f), 0f, width, 0f, 0f));
            }
        }

        return new FontAsset(
            new FontInfo("Generated isolation", 16, 4f),
            CreateTexture(),
            characters,
            16f,
            64,
            64);
    }

    static T GetPrivateField<T>(EditorSession session, string fieldName) {
        FieldInfo field = typeof(EditorSession).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<T>(field.GetValue(session));
    }

    static EditorProjectAuthoringSession CreateAuthoringSession(string projectRootPath, TestGeneratedAssetGraph graph) {
        return new EditorProjectAuthoringSession(
            projectRootPath,
            Array.Empty<IAssetImporterRegistration>(),
            new ContentManager(new HostFileSystemContentStreamSource(Path.Combine(projectRootPath, "assets"))),
            graph.Registry,
            graph.ModelCache,
            graph.MaterialCache,
            graph.RendererResources);
    }

    static string CreateProjectRoot() {
        string projectRootPath = Path.Combine(Path.GetTempPath(), "helengine-generated-isolation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRootPath, "assets"));
        File.WriteAllText(
            Path.Combine(projectRootPath, "project.heproj"),
            "{\"projectFormatVersion\":1,\"name\":\"Generated isolation\",\"requiredEngineVersion\":\"0.4.0\",\"supportedPlatforms\":[\"windows\"],\"created\":\"2026-04-01T00:00:00Z\",\"lastOpened\":\"2026-04-20T00:00:00Z\",\"version\":\"1.0.0\"}");
        return projectRootPath;
    }

    static void DeleteProjectRoot(string projectRootPath) {
        if (Directory.Exists(projectRootPath)) {
            Directory.Delete(projectRootPath, true);
        }
    }
}

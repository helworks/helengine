using helengine.baseplatform.Builders;
using helengine.baseplatform.Manifest;
using helengine.editor.tests.testing;
using helengine.platforms;
using System.Text.Json;
using System.Reflection;
using helengine.baseplatform.Requests;

namespace helengine.editor.tests.managers.project;

/// <summary>Protects immutable native SDK snapshots and the cache identities derived from their actual toolchains.</summary>
public sealed class PlatformCookContextTests {
    /// <summary>Every managed graph clone and native request must preserve the exact snapshot that identifies cooked shader artifacts.</summary>
    [Fact]
    public void GraphManifestCopiesAndNativeRequestPreserveCookProvenance() {
        string root = Path.GetFullPath(OperatingSystem.IsWindows() ? "C:/temp/helengine/macos/cook-context-graph-test" : "/tmp/helengine/macos/cook-context-graph-test");
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(root);
        try {
            using EditorBuiltInShaderAssetLibrary borrowed = new(new ShaderBackendRegistry());
            EditorPlatformBuildGraphRunner graph = new(root, "1", "project", "1", [], new AvailablePlatformDescriptor("ios", "iOS", "builder.dll"), null,
                new EditorPlatformAssetBuilderLoader(), new EditorGeneratedCoreRegenerationService(), null, borrowed, new EngineCodegenToolProvider());
            PlatformCookContext context = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"));
            PlatformBuildManifest manifest = new(2, "project", "1", "1", "ios", "1", "", [], [], [], [], [], new("", []), [], PlatformBuildRuntimeFeatureManifest.Empty) { CookContext = context };
            manifest = (PlatformBuildManifest)InvokeGraph(graph, "ApplyRuntimeFeatureManifest", manifest);
            Assert.Same(context, manifest.CookContext);
            manifest = (PlatformBuildManifest)InvokeGraph(graph, "RunResolveVariants", manifest, null);
            Assert.Same(context, manifest.CookContext);
            manifest = new EditorPlatformLayoutPlanService().Plan(manifest, null, null);
            Assert.Same(context, manifest.CookContext);
            manifest = (PlatformBuildManifest)InvokeGraph(null, "ReplaceCodeModules", manifest, Array.Empty<PlatformBuildCodeModule>());
            Assert.Same(context, manifest.CookContext);
            EditorBuildQueueItemDocument queue = new() { PlatformId = "ios", OutputDirectoryPath = root, NumericPolicy = NumericPrecisionPolicy.Default };
            PlatformBuildRequest request = (PlatformBuildRequest)InvokeGraph(graph, "BuildScopedRequest", queue, manifest, root, root,
                "debug", "metal", "debug", new Dictionary<string, string>(), "", "", "", "", PlatformBuildExecutionScope.Full, context);
            Assert.Same(context, request.CookContext);
            Assert.Same(context, request.Manifest.CookContext);
        } finally { Directory.Delete(root, true); }
    }

    /// <summary>Invokes actual graph phases without running unrelated external code generation or native build tools.</summary>
    static object InvokeGraph(EditorPlatformBuildGraphRunner graph, string name, params object[] arguments) {
        MethodInfo method = typeof(EditorPlatformBuildGraphRunner).GetMethod(name, BindingFlags.NonPublic | (graph == null ? BindingFlags.Static : BindingFlags.Instance));
        Assert.NotNull(method);
        return method.Invoke(graph, arguments);
    }

    /// <summary>Native object reuse must distinguish the same SDK, installed version and compiler inputs as scene packaging.</summary>
    [Fact]
    public void NativeFingerprintIncludesSdkSnapshot() {
        TestContextualPlatformAssetBuilder builder = new();
        EditorBuildQueueItemDocument queue = new() { NumericPolicy = NumericPrecisionPolicy.Default };
        PlatformCookContext device = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"));
        PlatformCookContext simulator = new("ios", new("apple-metal", "iphonesimulator", "26.2", "compiler-a"));
        string original = EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("ios", queue, "debug", "metal", "debug", "release", builder.Descriptor, null, device);
        Assert.NotEqual(original, EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("ios", queue, "debug", "metal", "debug", "release", builder.Descriptor, null, simulator));
        Assert.NotEqual(original, EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("ios", queue, "debug", "metal", "debug", "release", builder.Descriptor, null, new("ios", new("apple-metal", "iphoneos", "26.3", "compiler-a"))));
        Assert.NotEqual(original, EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("ios", queue, "debug", "metal", "debug", "release", builder.Descriptor, null, new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-b"))));
        Assert.Equal(original, EditorPlatformBuildGraphRunner.ComputeNativeCacheFingerprint("ios", queue, "debug", "metal", "debug", "release", builder.Descriptor, null, device));
    }
    /// <summary>Installing a contextual plugin must not probe its SDK, while legacy compiler contributions remain available.</summary>
    [Fact]
    public void GlobalRegistrySkipsContextualPluginAndKeepsLegacyContributor() {
        TestContextualPlatformAssetBuilder contextual = new();
        TestShaderBackendContributorPlatformAssetBuilder legacy = new("linux", ShaderCompileTarget.Vulkan);
        EditorPlatformCatalogService catalog = new([
            new AvailablePlatformDescriptor("ios", "iOS", "ios.dll", isInstalled: true),
            new AvailablePlatformDescriptor("linux", "Linux", "linux.dll", isInstalled: true)
        ], new TestCookContextBuilderLoader(new Dictionary<string, IPlatformAssetBuilder> { ["ios.dll"] = contextual, ["linux.dll"] = legacy }));
        ShaderBackendRegistry registry = new();
        catalog.RegisterShaderBackends(registry);
        Assert.True(registry.ContainsTarget(ShaderCompileTarget.Vulkan));
        Assert.False(registry.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.Equal(0, contextual.CaptureCount);
        Assert.Equal(0, contextual.LegacyRegistrationCount);
    }

    /// <summary>The builder loaded by the graph must reuse the CLI snapshot and own only its isolated compiler library.</summary>
    [Fact]
    public void SessionReusesSnapshotAcrossBuilderInstancesAndDisposesOnlyOwnedLibrary() {
        TestContextualPlatformAssetBuilder cliBuilder = new();
        PlatformCookContext captured = EditorPlatformCookSession.CaptureContext(cliBuilder);
        cliBuilder.SelectedSdk = "iphonesimulator";
        TestContextualPlatformAssetBuilder graphBuilder = new() { SelectedSdk = "iphonesimulator" };
        using EditorBuiltInShaderAssetLibrary borrowed = new(new ShaderBackendRegistry());
        EditorPlatformCookSession session = EditorPlatformCookSession.Create(graphBuilder, borrowed, captured);
        EditorBuiltInShaderAssetLibrary owned = session.ShaderLibrary;
        using (session) {
            Assert.Same(captured, session.Context);
            Assert.Same(captured, graphBuilder.RegisteredContext);
            Assert.Equal("iphoneos", graphBuilder.RegisteredContext.NativeSdk.Name);
            Assert.Equal(1, cliBuilder.CaptureCount);
            Assert.Equal(0, graphBuilder.CaptureCount);
            Assert.NotSame(borrowed, owned);
        }
        Assert.Throws<ObjectDisposedException>(() => owned.TryLoadById(ShaderCompileTarget.Metal, "", out _));
        Assert.False(borrowed.TryLoadById(ShaderCompileTarget.Vulkan, "", out _));
        using (EditorPlatformCookSession legacy = EditorPlatformCookSession.Create(new TestPlatformMaterialAssetBuilder("linux"), borrowed)) {
            Assert.Same(borrowed, legacy.ShaderLibrary);
            Assert.Null(legacy.Context.NativeSdk);
        }
        Assert.False(borrowed.TryLoadById(ShaderCompileTarget.Vulkan, "", out _));
    }

    /// <summary>Compiler assets remain scoped to the build when a later cook or native phase fails.</summary>
    [Fact]
    public void SessionDisposesOwnedLibraryWhenBuildPhaseThrows() {
        using EditorBuiltInShaderAssetLibrary borrowed = new(new ShaderBackendRegistry());
        EditorPlatformCookSession session = EditorPlatformCookSession.Create(new TestContextualPlatformAssetBuilder(), borrowed);
        EditorBuiltInShaderAssetLibrary owned = session.ShaderLibrary;
        Assert.Throws<InvalidOperationException>((Action)(() => {
            using (session) { throw new InvalidOperationException("cook phase failed"); }
        }));
        Assert.Throws<ObjectDisposedException>(() => owned.TryLoadById(ShaderCompileTarget.Metal, "", out _));
        Assert.False(borrowed.TryLoadById(ShaderCompileTarget.Vulkan, "", out _));
    }

    /// <summary>Managed manifest provenance survives JSON while old manifests remain valid without changing constructor signatures.</summary>
    [Fact]
    public void ManifestRoundTripPreservesContextAndAcceptsLegacyJson() {
        PlatformBuildManifest manifest = new(2, "project", "1", "1", "ios", "1", "", [], [], [], [], [], new("", []), [], PlatformBuildRuntimeFeatureManifest.Empty) {
            CookContext = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"))
        };
        string json = JsonSerializer.Serialize(manifest);
        Assert.Equal(manifest.CookContext.CacheIdentity, JsonSerializer.Deserialize<PlatformBuildManifest>(json).CookContext.CacheIdentity);
        manifest.CookContext = null;
        string legacy = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull });
        Assert.DoesNotContain("CookContext", legacy);
        Assert.Null(JsonSerializer.Deserialize<PlatformBuildManifest>(legacy).CookContext);
    }
    /// <summary>The same loaded builder and unchanged project inputs must produce SDK-specific keys and recover the original key on return.</summary>
    [Fact]
    public void SameBuilderDllSdkFlipChangesScenePackageKeyWithoutClearingCache() {
        string testRoot = OperatingSystem.IsWindows() ? "C:/temp/helengine/macos/cook-context-cache-test" : "/tmp/helengine/macos/cook-context-cache-test";
        string root = Path.GetFullPath(testRoot);
        Assert.False(Directory.Exists(root));
        Directory.CreateDirectory(Path.Combine(root, "assets"));
        File.WriteAllText(Path.Combine(root, "assets", "scene.helen"), "unchanged scene");
        try {
            testing.TestPlatformMaterialAssetBuilder builder = new("ios");
            Guid module = builder.GetType().Assembly.ManifestModule.ModuleVersionId;
            EditorScenePackageCache cache = new(root);
            PlatformCookContext device = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"));
            PlatformCookContext simulator = new("ios", new("apple-metal", "iphonesimulator", "26.2", "compiler-a"));
            string original = ComputeKey(cache, builder, device);
            Assert.NotEqual(original, ComputeKey(cache, builder, simulator));
            Assert.NotEqual(original, ComputeKey(cache, builder, new("ios", new("apple-metal", "iphoneos", "26.3", "compiler-a"))));
            Assert.NotEqual(original, ComputeKey(cache, builder, new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-b"))));
            Assert.Equal(original, ComputeKey(cache, builder, device));
            Assert.Equal(module, builder.GetType().Assembly.ManifestModule.ModuleVersionId);
        } finally {
            Assert.Equal(Path.GetFullPath(testRoot), root);
            Directory.Delete(root, true);
        }
    }

    /// <summary>Runs the actual scene cache against one unchanged builder implementation and captured SDK selection.</summary>
    static string ComputeKey(EditorScenePackageCache cache, testing.TestPlatformMaterialAssetBuilder builder, PlatformCookContext context) {
        return cache.ComputeKey(builder.Definition, builder, "debug", "metal", "debug", ["scene"], ["scene.helen"], [], null, null, null, cookContext: context).Key;
    }
    /// <summary>Changing SDK, installed version or compiler implementation must distinguish otherwise identical builds.</summary>
    [Fact]
    public void NativeSdkIdentitySeparatesSdkVersionAndCompiler() {
        PlatformCookContext device = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"));
        PlatformCookContext identical = new("ios", new("apple-metal", "iphoneos", "26.2", "compiler-a"));
        Assert.Equal(device.CacheIdentity, identical.CacheIdentity);
        Assert.NotEqual(device.CacheIdentity, new PlatformCookContext("ios", new("apple-metal", "iphonesimulator", "26.2", "compiler-a")).CacheIdentity);
        Assert.NotEqual(device.CacheIdentity, new PlatformCookContext("ios", new("apple-metal", "iphoneos", "26.3", "compiler-a")).CacheIdentity);
        Assert.NotEqual(device.CacheIdentity, new PlatformCookContext("ios", new("apple-metal", "iphoneos", "26.2", "compiler-b")).CacheIdentity);
    }

    /// <summary>Captured selections cannot change when the process environment changes after capture.</summary>
    [Fact]
    public void CapturedContextDoesNotReadLaterEnvironment() {
        const string key = "HELENGINE_COOK_CONTEXT_TEST_SDK";
        string previous = Environment.GetEnvironmentVariable(key);
        try {
            Environment.SetEnvironmentVariable(key, "iphoneos");
            PlatformCookContext context = new("ios", new("apple-metal", Environment.GetEnvironmentVariable(key), "26.2", "compiler-a"));
            string identity = context.CacheIdentity;
            Environment.SetEnvironmentVariable(key, "iphonesimulator");
            Assert.Equal("iphoneos", context.NativeSdk.Name);
            Assert.Equal(identity, context.CacheIdentity);
        } finally { Environment.SetEnvironmentVariable(key, previous); }
    }

    /// <summary>Platforms without an SDK-specific provider retain a stable context without inventing a native SDK.</summary>
    [Fact]
    public void PlatformWithoutSdkHasStableExplicitIdentity() {
        PlatformCookContext linux = new("linux");
        Assert.Null(linux.NativeSdk);
        Assert.Equal(linux.CacheIdentity, new PlatformCookContext("linux").CacheIdentity);
        Assert.NotEqual(linux.CacheIdentity, new PlatformCookContext("windows").CacheIdentity);
    }
}

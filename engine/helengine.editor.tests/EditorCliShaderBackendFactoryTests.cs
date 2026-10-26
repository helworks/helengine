using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;
using helengine.editor.tests.testing;
using helengine.platforms;

namespace helengine.editor.tests;

/// <summary>Separates host authoring compilers from console cook compilers without global SDK probing.</summary>
public sealed class EditorCliShaderBackendFactoryTests {
    [Fact]
    public void UnusedAuthoringLibraryDoesNotCaptureHostEvenDuringIosCook() {
        HostBuilder host = new() { CaptureFailure = new InvalidOperationException("incompatible host SDK environment") };
        var (catalog, loader) = Catalog(host);
        PlatformCookContext ios = new("ios", new("apple-metal", "iphoneos", "26.2", "test"));
        EditorCliHostShaderBackend deferred = new(catalog, "macos", ios);
        using EditorBuiltInShaderAssetLibrary library = new(deferred.Registry, deferred.EnsureRegistered);
        Assert.Equal(ShaderCompileTarget.Metal, deferred.Target);
        Assert.Empty(loader.LoadedAssemblyPaths);
        Assert.Equal(0, host.CaptureCount);
        Assert.False(deferred.Registry.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.Equal("iphoneos", ios.NativeSdk.Name);
    }

    [Fact]
    public void FirstAuthoringCompileRegistersHostAndPreservesSdkFailure() {
        HostBuilder host = new() { CaptureFailure = new InvalidOperationException("incompatible host SDK environment") };
        var (catalog, _) = Catalog(host);
        EditorCliHostShaderBackend deferred = new(catalog, "macos");
        using EditorBuiltInShaderAssetLibrary library = new(deferred.Registry, deferred.EnsureRegistered);
        Assert.Same(host.CaptureFailure, Assert.Throws<InvalidOperationException>(() => library.Load(ShaderCompileTarget.Metal, "EditorTransformGizmo.hlsl")));
        Assert.Equal(1, host.CaptureCount);
        Assert.Same(host.CaptureFailure, Assert.Throws<InvalidOperationException>(() => library.Load(ShaderCompileTarget.Metal, "EditorTransformGizmo.hlsl")));
        Assert.Equal(1, host.CaptureCount);
    }

    [Fact]
    public void MacAuthoringUsesCapturedMetalWhile3DsCookKeepsItsOwnTarget() {
        HostBuilder host = new();
        TestShaderBackendContributorPlatformAssetBuilder console = new("3ds", ShaderCompileTarget.DirectX11);
        var (catalog, loader) = Catalog(host, console);
        var authoring = EditorCliShaderBackendFactory.CreateHost(catalog, "macos");
        PlatformCookContext cookContext = EditorPlatformCookSession.CaptureContext(console);
        ShaderBackendRegistry cook = EditorCliShaderBackendFactory.CreateCookRegistry(catalog, "3ds", cookContext);
        Assert.Equal(ShaderCompileTarget.Metal, authoring.Target);
        Assert.True(authoring.Registry.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.False(authoring.Registry.ContainsTarget(ShaderCompileTarget.DirectX11));
        Assert.True(cook.ContainsTarget(ShaderCompileTarget.DirectX11));
        Assert.False(cook.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.Equal(ShaderCompileTarget.DirectX11, EditorCliBuildRunner.ResolveShaderCompileTarget("3ds"));
        Assert.Same(authoring.Context, host.RegisteredContext);
        Assert.Equal("macosx", authoring.Context.NativeSdk.Name);
        Assert.Equal(1, host.CaptureCount);
        Assert.Equal(0, host.LegacyCount);
        Assert.Equal(new[] { "macos.dll", "3ds.dll" }, loader.LoadedAssemblyPaths);
    }

    [Fact]
    public void StandaloneAuthoringDoesNotLoadOptionalConsoleBuilders() {
        HostBuilder host = new();
        var (catalog, loader) = Catalog(host);
        var authoring = EditorCliShaderBackendFactory.CreateHost(catalog, "macos");
        Assert.True(authoring.Registry.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.Equal(new[] { "macos.dll" }, loader.LoadedAssemblyPaths);
    }

    [Fact]
    public void MacCookReusesExactCapturedSdkForHostInsteadOfRecapturing() {
        HostBuilder host = new();
        var (catalog, _) = Catalog(host);
        PlatformCookContext captured = EditorPlatformCookSession.CaptureContext(host);
        host.Sdk = "changed-after-capture";
        var authoring = EditorCliShaderBackendFactory.CreateHost(catalog, "macos", captured);
        ShaderBackendRegistry cook = EditorCliShaderBackendFactory.CreateCookRegistry(catalog, "macos", captured);
        Assert.Same(captured, authoring.Context);
        Assert.Same(captured, host.RegisteredContext);
        Assert.Equal(1, host.CaptureCount);
        Assert.True(cook.ContainsTarget(ShaderCompileTarget.Metal));
        Assert.Equal("macosx", captured.NativeSdk.Name);
    }

    [Fact]
    public void IosCookContextCannotReplaceMacAuthoringContext() {
        HostBuilder host = new();
        var (catalog, _) = Catalog(host);
        PlatformCookContext ios = new("ios", new("apple-metal", "iphoneos", "26.2", "test"));
        var authoring = EditorCliShaderBackendFactory.CreateHost(catalog, "macos", ios);
        Assert.Equal("macos", authoring.Context.PlatformId);
        Assert.NotSame(ios, authoring.Context);
        Assert.Equal("iphoneos", ios.NativeSdk.Name);
        Assert.Equal(1, host.CaptureCount);
    }

    [Fact]
    public void MissingMacHostReportsRegistrationInsteadOfDesktopDllFallback() {
        EditorPlatformCatalogService catalog = new([]);
        var error = Assert.Throws<InvalidOperationException>(() => EditorCliShaderBackendFactory.CreateHost(catalog, "macos"));
        Assert.Contains("macos", error.Message);
        Assert.Contains("platforms.json", error.Message);
    }

    [Fact]
    public void RegisteredHostMustActuallyProvideItsShaderTarget() {
        HostBuilder host = new() { RegisteredTarget = ShaderCompileTarget.DirectX11 };
        var (catalog, _) = Catalog(host);
        var error = Assert.Throws<InvalidOperationException>(() => EditorCliShaderBackendFactory.CreateHost(catalog, "macos"));
        Assert.Contains("Metal", error.Message);
    }

    [Fact]
    public void InvalidCapturedHostSdkRemainsAnExplicitError() {
        var (catalog, _) = Catalog(new HostBuilder());
        Assert.Throws<InvalidOperationException>(() => EditorCliShaderBackendFactory.CreateHost(catalog, "macos", new PlatformCookContext("macos")));
    }

    [Fact]
    public void SdkProbeFailureIsPreservedWithoutTryingAnotherCompiler() {
        HostBuilder host = new() { CaptureFailure = new InvalidOperationException("installed SDK missing") };
        var (catalog, _) = Catalog(host);
        Assert.Same(host.CaptureFailure, Assert.Throws<InvalidOperationException>(() => EditorCliShaderBackendFactory.CreateHost(catalog, "macos")));
        Assert.Null(host.RegisteredContext);
    }

    [Fact]
    public void WindowsHostUsesExistingBuiltInBackendWithoutOptionalBuilders() {
        PlatformCookContext captured = new("windows");
        var authoring = EditorCliShaderBackendFactory.CreateHost(new EditorPlatformCatalogService([]), "windows", captured);
        Assert.Equal(ShaderCompileTarget.DirectX11, authoring.Target);
        Assert.True(authoring.Registry.ContainsTarget(ShaderCompileTarget.DirectX11));
        Assert.Null(authoring.Context.NativeSdk);
        Assert.Same(captured, authoring.Context);
    }

    [Fact]
    public void LinuxHostUsesItsExistingLegacyVulkanContributor() {
        TestShaderBackendContributorPlatformAssetBuilder host = new("linux", ShaderCompileTarget.Vulkan);
        TestEditorPlatformAssetBuilderLoader loader = new(new Dictionary<string, IPlatformAssetBuilder> { ["linux.dll"] = host });
        EditorPlatformCatalogService catalog = new([new AvailablePlatformDescriptor("linux", "Linux", "linux.dll")], loader);
        var authoring = EditorCliShaderBackendFactory.CreateHost(catalog, "linux");
        Assert.Equal(ShaderCompileTarget.Vulkan, authoring.Target);
        Assert.True(authoring.Registry.ContainsTarget(ShaderCompileTarget.Vulkan));
        Assert.False(authoring.Registry.ContainsTarget(ShaderCompileTarget.DirectX11));
        Assert.Null(authoring.Context.NativeSdk);
        Assert.Equal(new[] { "linux.dll" }, loader.LoadedAssemblyPaths);
    }

    static (EditorPlatformCatalogService, TestEditorPlatformAssetBuilderLoader) Catalog(HostBuilder host, IPlatformAssetBuilder console = null) {
        var descriptors = new[] { new AvailablePlatformDescriptor("macos", "macOS", "macos.dll"), new AvailablePlatformDescriptor("3ds", "3DS", "3ds.dll") };
        Dictionary<string, IPlatformAssetBuilder> builders = new() { ["macos.dll"] = host };
        if (console != null) builders["3ds.dll"] = console;
        TestEditorPlatformAssetBuilderLoader loader = new(builders);
        return (new EditorPlatformCatalogService(descriptors, loader), loader);
    }

    sealed class HostBuilder : IPlatformAssetBuilder, IPlatformCookContextProvider, IContextualShaderBackendRegistryContributor, IShaderBackendRegistryContributor {
        readonly TestPlatformMaterialAssetBuilder Inner = new("macos");
        public int CaptureCount { get; private set; }
        public int LegacyCount { get; private set; }
        public string Sdk { get; set; } = "macosx";
        public ShaderCompileTarget RegisteredTarget { get; set; } = ShaderCompileTarget.Metal;
        public InvalidOperationException CaptureFailure { get; set; }
        public PlatformCookContext RegisteredContext { get; private set; }
        public PlatformBuilderDescriptor Descriptor => Inner.Descriptor;
        public PlatformDefinition Definition => Inner.Definition;
        public PlatformCookContext CaptureCookContext() {
            CaptureCount++;
            if (CaptureFailure != null) throw CaptureFailure;
            return new("macos", new("apple-metal", Sdk, "26.2", "test"));
        }
        public void RegisterShaderBackends(ShaderBackendRegistry registry) { LegacyCount++; throw new InvalidOperationException("Context required"); }
        public void RegisterShaderBackends(ShaderBackendRegistry registry, PlatformCookContext context) { RegisteredContext = context; registry.Register(new TestShaderBackend(RegisteredTarget)); }
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) => Inner.CookMaterial(request);
        public Task<PlatformBuildReport> BuildAsync(PlatformBuildRequest request, IPlatformBuildProgressReporter progress, IPlatformBuildDiagnosticReporter diagnostics, CancellationToken token) => Inner.BuildAsync(request, progress, diagnostics, token);
    }
}

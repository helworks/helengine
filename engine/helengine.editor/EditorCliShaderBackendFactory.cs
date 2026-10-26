using helengine.baseplatform.Builders;
using helengine.directx11;

namespace helengine.editor;

/// <summary>Keeps host authoring compilers separate from the selected player's cook compilers.</summary>
internal static class EditorCliShaderBackendFactory {
    /// <summary>Resolves the current authoring host independently of the selected player's platform.</summary>
    /// <returns>The canonical Windows, Linux, or macOS platform identifier.</returns>
    internal static string ResolveHostPlatformId() => OperatingSystem.IsWindows() ? "windows"
        : OperatingSystem.IsLinux() ? "linux"
        : OperatingSystem.IsMacOS() ? "macos"
        : throw new PlatformNotSupportedException("Headless authoring requires Windows, Linux, or macOS.");

    /// <summary>Creates host compiler state without loading optional builders or probing their SDKs.</summary>
    /// <param name="catalog">Catalog containing explicitly registered host builders.</param>
    /// <param name="capturedCookContext">Existing player snapshot reused when its platform matches the host.</param>
    /// <returns>Host state whose registration is deferred until authoring requires it.</returns>
    internal static EditorCliHostShaderBackend CreateDeferredHost(EditorPlatformCatalogService catalog, PlatformCookContext capturedCookContext = null) =>
        new(catalog, ResolveHostPlatformId(), capturedCookContext);

    /// <summary>Registers the requested host compiler against one validated immutable SDK snapshot.</summary>
    /// <param name="catalog">Catalog used to resolve the explicitly installed host builder.</param>
    /// <param name="hostPlatformId">Canonical authoring host identifier.</param>
    /// <param name="capturedCookContext">Existing snapshot reused only when its platform matches the host.</param>
    /// <param name="registry">Registry to populate, or null to create an isolated host registry.</param>
    /// <returns>The host target, its populated registry, and the captured SDK context.</returns>
    internal static (ShaderCompileTarget Target, ShaderBackendRegistry Registry, PlatformCookContext Context) CreateHost(
        EditorPlatformCatalogService catalog, string hostPlatformId, PlatformCookContext capturedCookContext = null, ShaderBackendRegistry registry = null) {
        ArgumentNullException.ThrowIfNull(catalog);
        if (hostPlatformId is not ("windows" or "linux" or "macos")) throw new PlatformNotSupportedException("Unsupported authoring host: " + hostPlatformId);
        ShaderCompileTarget target = EditorPlatformShaderTargetResolver.Resolve(hostPlatformId);
        registry ??= new ShaderBackendRegistry();
        if (hostPlatformId == "windows") {
            registry.Register(new DirectX11ShaderBackend());
            return (target, registry, capturedCookContext?.PlatformId == "windows" ? capturedCookContext : new PlatformCookContext("windows"));
        }
        if (catalog.ResolveSelectionModel(hostPlatformId) == null) {
            throw new InvalidOperationException($"Host authoring requires the '{hostPlatformId}' shader backend ({target}). Register its prepared builder in the engine platforms.json; no SDK installation or compiler fallback was attempted.");
        }
        EditorLoadedPlatformBuilder host = catalog.Resolve(hostPlatformId);
        PlatformCookContext context = capturedCookContext?.PlatformId == hostPlatformId
            ? capturedCookContext : EditorPlatformCookSession.CaptureContext(host.Builder);
        EditorPlatformCookSession.ValidateContext(host.Builder, context);
        host.RegisterShaderBackends(registry, context);
        if (!registry.ContainsTarget(target)) throw new InvalidOperationException($"Host builder '{hostPlatformId}' did not register the required {target} shader backend.");
        return (target, registry, context);
    }

    /// <summary>Creates the selected player's compiler registry without importing host authoring backends.</summary>
    /// <param name="catalog">Catalog containing the selected player builder.</param>
    /// <param name="platformId">Canonical platform identifier of the player being cooked.</param>
    /// <param name="context">Previously captured SDK snapshot belonging to that player.</param>
    /// <returns>An isolated registry preserving the player's existing compiler selection.</returns>
    internal static ShaderBackendRegistry CreateCookRegistry(EditorPlatformCatalogService catalog, string platformId, PlatformCookContext context) {
        ArgumentNullException.ThrowIfNull(catalog);
        EditorLoadedPlatformBuilder player = catalog.Resolve(platformId);
        EditorPlatformCookSession.ValidateContext(player.Builder, context);
        ShaderBackendRegistry registry = new();
        registry.Register(new DirectX11ShaderBackend());
        player.RegisterShaderBackends(registry, context);
        return registry;
    }
}

/// <summary>Defers optional host SDK capture until an authoring compile or a real prebuild command needs it.</summary>
internal sealed class EditorCliHostShaderBackend {
    /// <summary>Owns the single deferred SDK capture and preserves its result or failure across authoring requests.</summary>
    readonly Lazy<PlatformCookContext> CapturedContext;
    /// <summary>Creates compiler state without resolving a host builder or probing its SDK.</summary>
    /// <param name="catalog">Catalog used when host compiler registration becomes necessary.</param>
    /// <param name="hostPlatformId">Canonical authoring host identifier.</param>
    /// <param name="capturedCookContext">Existing player snapshot reused when its platform matches the host.</param>
    internal EditorCliHostShaderBackend(EditorPlatformCatalogService catalog, string hostPlatformId, PlatformCookContext capturedCookContext = null) {
        Target = EditorPlatformShaderTargetResolver.Resolve(hostPlatformId);
        Registry = new ShaderBackendRegistry();
        CapturedContext = new Lazy<PlatformCookContext>(() => EditorCliShaderBackendFactory.CreateHost(catalog, hostPlatformId, capturedCookContext, Registry).Context);
    }
    /// <summary>Gets the host shader target without triggering SDK capture.</summary>
    internal ShaderCompileTarget Target { get; }
    /// <summary>Gets the isolated registry populated when host authoring first requires a compiler.</summary>
    internal ShaderBackendRegistry Registry { get; }
    /// <summary>Registers the host compiler once, propagating capability or SDK failures without fallback.</summary>
    internal void EnsureRegistered() { _ = CapturedContext.Value; }
}

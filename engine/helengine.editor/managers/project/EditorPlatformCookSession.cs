using helengine.baseplatform.Builders;

namespace helengine.editor {
    /// <summary>Owns one build's SDK-bound shader library while preserving borrowed libraries for legacy platforms.</summary>
    internal sealed class EditorPlatformCookSession : IDisposable {
        /// <summary>Records whether this build created the shader library and must release its compiled assets.</summary>
        readonly bool OwnsShaderLibrary;

        /// <summary>Retains immutable build inputs and the library selected for exactly this cook.</summary>
        EditorPlatformCookSession(PlatformCookContext context, EditorBuiltInShaderAssetLibrary shaderLibrary, bool ownsShaderLibrary) {
            Context = context;
            ShaderLibrary = shaderLibrary;
            OwnsShaderLibrary = ownsShaderLibrary;
        }
        /// <summary>Gets the exact captured context passed to cache lookup and native packaging.</summary>
        public PlatformCookContext Context { get; }
        /// <summary>Gets the isolated SDK-bound library, or the unmodified borrowed legacy library.</summary>
        public EditorBuiltInShaderAssetLibrary ShaderLibrary { get; }

        /// <summary>Captures a provider's SDK once, or explicitly represents a platform without an SDK-dependent provider.</summary>
        public static PlatformCookContext CaptureContext(IPlatformAssetBuilder builder) {
            ArgumentNullException.ThrowIfNull(builder);
            PlatformCookContext context = builder is IPlatformCookContextProvider provider
                ? provider.CaptureCookContext()
                : new PlatformCookContext(builder.Definition.PlatformId);
            ValidateContext(builder, context);
            return context;
        }

        /// <summary>Creates per-build compiler ownership without recapturing a context supplied by the CLI.</summary>
        public static EditorPlatformCookSession Create(IPlatformAssetBuilder builder, EditorBuiltInShaderAssetLibrary borrowedLibrary, PlatformCookContext capturedContext = null) {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentNullException.ThrowIfNull(borrowedLibrary);
            PlatformCookContext context = capturedContext ?? CaptureContext(builder);
            ValidateContext(builder, context);
            if (builder is not IContextualShaderBackendRegistryContributor contributor) {
                return new EditorPlatformCookSession(context, borrowedLibrary, false);
            }
            ShaderBackendRegistry registry = new();
            contributor.RegisterShaderBackends(registry, context);
            return new EditorPlatformCookSession(context, new EditorBuiltInShaderAssetLibrary(registry), true);
        }

        /// <summary>Rejects missing SDK snapshots and accidental reuse across different canonical target identities.</summary>
        static void ValidateContext(IPlatformAssetBuilder builder, PlatformCookContext context) {
            if (context == null) { throw new InvalidOperationException("Platform cook context providers must return an immutable context."); }
            if (!string.Equals(context.PlatformId, builder.Definition.PlatformId, StringComparison.Ordinal)) {
                throw new InvalidOperationException($"Cook context for '{context.PlatformId}' cannot cook target '{builder.Definition.PlatformId}'.");
            }
            if (builder is not IPlatformCookContextProvider && context.NativeSdk != null) {
                throw new InvalidOperationException($"Platform '{context.PlatformId}' does not provide contextual native SDK cooking.");
            }
            if (builder is IPlatformCookContextProvider && context.NativeSdk == null) {
                throw new InvalidOperationException($"Platform '{context.PlatformId}' requires its captured native SDK selection.");
            }
        }

        /// <summary>Disposes only the library created by this build, including when cooking or native packaging throws.</summary>
        public void Dispose() {
            if (OwnsShaderLibrary) { ShaderLibrary.Dispose(); }
        }
    }
}

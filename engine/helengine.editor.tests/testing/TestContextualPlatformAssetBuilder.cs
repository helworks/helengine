using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor.tests.testing {
    /// <summary>Exposes mutable capture inputs and rejects legacy registration so tests detect accidental SDK recapture or global compiler creation.</summary>
    public sealed class TestContextualPlatformAssetBuilder : IPlatformAssetBuilder, IPlatformCookContextProvider, IContextualShaderBackendRegistryContributor, IShaderBackendRegistryContributor {
        /// <summary>Delegates unrelated material and metadata behavior to the existing test builder.</summary>
        readonly TestPlatformMaterialAssetBuilder Inner = new("ios");
        /// <summary>Gets the number of SDK captures performed on this builder instance.</summary>
        public int CaptureCount { get; private set; }
        /// <summary>Gets the number of forbidden context-free registrations attempted.</summary>
        public int LegacyRegistrationCount { get; private set; }
        /// <summary>Gets the exact context bound to this instance's compiler.</summary>
        public PlatformCookContext RegisteredContext { get; private set; }
        /// <summary>Gets or sets the SDK selected if a new capture is requested.</summary>
        public string SelectedSdk { get; set; } = "iphoneos";
        /// <summary>Gets stable builder identity independent of the selected SDK.</summary>
        public PlatformBuilderDescriptor Descriptor => Inner.Descriptor;
        /// <summary>Gets platform metadata independent of mutable capture inputs.</summary>
        public PlatformDefinition Definition => Inner.Definition;
        /// <summary>Captures the current SDK selection, recording every invocation.</summary>
        public PlatformCookContext CaptureCookContext() {
            CaptureCount++;
            return new("ios", new("apple-metal", SelectedSdk, "26.2", "test-compiler"));
        }
        /// <summary>Requires explicit build context instead of silently consulting process state during global registration.</summary>
        public void RegisterShaderBackends(ShaderBackendRegistry registry) {
            LegacyRegistrationCount++;
            throw new InvalidOperationException("This compiler requires a captured build context.");
        }
        /// <summary>Records and binds the exact supplied build context without recapturing.</summary>
        public void RegisterShaderBackends(ShaderBackendRegistry registry, PlatformCookContext context) {
            RegisteredContext = context;
            registry.Register(new TestShaderBackend(ShaderCompileTarget.Metal));
        }
        /// <summary>Delegates material translation unrelated to context ownership.</summary>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) => Inner.CookMaterial(request);
        /// <summary>Delegates packaging unrelated to capture and registration tests.</summary>
        public Task<PlatformBuildReport> BuildAsync(PlatformBuildRequest request, IPlatformBuildProgressReporter progressReporter, IPlatformBuildDiagnosticReporter diagnosticReporter, CancellationToken cancellationToken) => Inner.BuildAsync(request, progressReporter, diagnosticReporter, cancellationToken);
    }
}

using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides one minimal platform builder that contributes a single shader backend for catalog-loading tests.
    /// </summary>
    public sealed class TestShaderBackendContributorPlatformAssetBuilder : IPlatformAssetBuilder, IShaderBackendRegistryContributor {
        /// <summary>
        /// Initializes one minimal platform builder for the supplied platform id and shader target.
        /// </summary>
        /// <param name="platformId">Stable platform identifier exposed by the builder.</param>
        /// <param name="shaderCompileTarget">Compile target contributed by the builder.</param>
        public TestShaderBackendContributorPlatformAssetBuilder(string platformId, ShaderCompileTarget shaderCompileTarget) {
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }

            ShaderCompileTarget = shaderCompileTarget;
            Descriptor = new PlatformBuilderDescriptor(
                "helengine.editor.tests." + platformId + ".builder",
                "1.0.0",
                platformId,
                new EngineCompatibilityRange("1.0.0", "999.0.0"),
                new ManifestCompatibilityRange(1, 1),
                [platformId],
                ["default"]);
            Definition = new PlatformDefinition(
                platformId,
                platformId.ToUpperInvariant(),
                [
                    new PlatformBuildProfileDefinition(
                        "default",
                        "Default",
                        "Default test build profile.",
                        "graphics",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "graphics",
                        "Graphics",
                        "Default test graphics profile.",
                        [])
                ],
                [],
                [],
                [],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default test codegen profile.",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [])
                ]);
        }

        /// <summary>
        /// Gets the shader compile target contributed by the builder.
        /// </summary>
        ShaderCompileTarget ShaderCompileTarget { get; }

        /// <summary>
        /// Gets the builder descriptor exposed to the editor.
        /// </summary>
        public PlatformBuilderDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the minimal platform definition exposed to the editor.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Throws because the test builder is only used for catalog loading and shader-backend contribution.
        /// </summary>
        /// <param name="request">Material cook request.</param>
        /// <returns>Never returns because material cooking is outside the scope of the test builder.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
            throw new NotSupportedException("The test builder does not cook materials.");
        }

        /// <summary>
        /// Returns a successful build report because catalog tests do not execute platform packaging logic.
        /// </summary>
        /// <param name="request">Build request supplied by the editor.</param>
        /// <param name="progressReporter">Progress reporter supplied by the editor.</param>
        /// <param name="diagnosticReporter">Diagnostic reporter supplied by the editor.</param>
        /// <param name="cancellationToken">Cancellation token supplied by the editor.</param>
        /// <returns>A completed successful build report.</returns>
        public Task<PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            IPlatformBuildProgressReporter progressReporter,
            IPlatformBuildDiagnosticReporter diagnosticReporter,
            CancellationToken cancellationToken) {
            return Task.FromResult(new PlatformBuildReport(true, [], [], []));
        }

        /// <summary>
        /// Registers the builder's single test shader backend into the supplied registry.
        /// </summary>
        /// <param name="shaderBackendRegistry">Registry that should receive the test shader backend.</param>
        public void RegisterShaderBackends(ShaderBackendRegistry shaderBackendRegistry) {
            if (shaderBackendRegistry == null) {
                throw new ArgumentNullException(nameof(shaderBackendRegistry));
            }

            shaderBackendRegistry.Register(new TestShaderBackend(ShaderCompileTarget));
        }
    }
}

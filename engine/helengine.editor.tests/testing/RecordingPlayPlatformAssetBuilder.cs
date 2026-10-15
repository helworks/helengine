using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.editor.tests.testing {
    /// <summary>
    /// Minimal Windows-shaped platform builder for editor Play tests. It publishes a configurable Play capability,
    /// records every package request it receives and reports success without producing native output.
    /// </summary>
    public sealed class RecordingPlayPlatformAssetBuilder : IPlatformAssetBuilder {
        /// <summary>
        /// Package requests received through <see cref="BuildAsync"/>, in call order.
        /// </summary>
        readonly List<PlatformBuildRequest> RecordedRequests;

        /// <summary>
        /// Initializes the builder with one debug build profile, one DirectX graphics profile, loose-file storage,
        /// an install-tree media profile and the supplied Play capability.
        /// </summary>
        /// <param name="playerCapability">Play capability published through the platform definition.</param>
        public RecordingPlayPlatformAssetBuilder(PlatformPlayerCapability playerCapability)
            : this(playerCapability, [], null) {
        }

        /// <summary>
        /// Initializes the builder like the single-argument constructor, but with the given settings on its graphics profile
        /// and the given video modes, so a test can build a platform whose Play frame rate comes from a video mode.
        /// </summary>
        /// <param name="playerCapability">Play capability published through the platform definition.</param>
        /// <param name="graphicsSettings">Settings of the single graphics profile (a video-mode platform declares its mode setting here).</param>
        /// <param name="videoModeCapability">Video modes published through the platform definition, or null for none.</param>
        public RecordingPlayPlatformAssetBuilder(PlatformPlayerCapability playerCapability, PlatformSettingDefinition[] graphicsSettings, PlatformVideoModeCapability videoModeCapability) {
            if (playerCapability == null) {
                throw new ArgumentNullException(nameof(playerCapability));
            } else if (graphicsSettings == null) {
                throw new ArgumentNullException(nameof(graphicsSettings));
            }

            RecordedRequests = [];
            Descriptor = new PlatformBuilderDescriptor(
                "helengine.editor.tests.play-builder",
                "1.0.0",
                "windows",
                new EngineCompatibilityRange("1.0.0", "999.0.0"),
                new ManifestCompatibilityRange(1, 1),
                ["windows"],
                ["debug"]);
            Definition = new PlatformDefinition(
                "windows",
                "Windows Play Test",
                [
                    new PlatformBuildProfileDefinition(
                        "debug",
                        "Debug",
                        "Debug player build",
                        "directx11",
                        [])
                ],
                [
                    new PlatformGraphicsProfileDefinition(
                        "directx11",
                        "DirectX 11",
                        "Default Windows renderer",
                        graphicsSettings)
                ],
                [],
                [],
                [],
                [
                    new PlatformCodegenProfileDefinition(
                        "default",
                        "Default",
                        "Default codegen profile",
                        PlatformCodegenLanguage.Cpp,
                        PlatformSerializationEndianness.LittleEndian,
                        [])
                ],
                [
                    new PlatformStorageProfileDefinition(
                        "default",
                        "Default",
                        PlatformStorageProfileKind.LooseFiles,
                        "storage",
                        true)
                ],
                [
                    new PlatformMediaProfileDefinition(
                        "install",
                        "Install",
                        PlatformMediaLayoutKind.InstallTree,
                        true,
                        true)
                ],
                playerCapability: playerCapability,
                videoModeCapability: videoModeCapability);
        }

        /// <summary>
        /// Gets the builder descriptor exposed to the editor build graph.
        /// </summary>
        public PlatformBuilderDescriptor Descriptor { get; }

        /// <summary>
        /// Gets the platform definition carrying the configured Play capability.
        /// </summary>
        public PlatformDefinition Definition { get; }

        /// <summary>
        /// Gets the package requests received so far, in call order.
        /// </summary>
        public IReadOnlyList<PlatformBuildRequest> Requests => RecordedRequests;

        /// <summary>
        /// Material cooking is never reached by the Play tests, which cook material-free scenes.
        /// </summary>
        /// <param name="request">Unused material request.</param>
        /// <returns>Never returns; always throws.</returns>
        public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
            throw new NotSupportedException("Material cooking is not used by the Play test builder.");
        }

        /// <summary>
        /// Records the package request and reports a successful build.
        /// </summary>
        /// <param name="request">Package request built by the editor build graph.</param>
        /// <param name="progressReporter">Unused progress sink.</param>
        /// <param name="diagnosticReporter">Unused diagnostic sink.</param>
        /// <param name="cancellationToken">Unused cancellation token.</param>
        /// <returns>A successful build report.</returns>
        public Task<PlatformBuildReport> BuildAsync(
            PlatformBuildRequest request,
            IPlatformBuildProgressReporter progressReporter,
            IPlatformBuildDiagnosticReporter diagnosticReporter,
            CancellationToken cancellationToken) {
            RecordedRequests.Add(request);
            return Task.FromResult(new PlatformBuildReport(true, [], [], []));
        }
    }
}

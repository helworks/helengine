using helengine.baseplatform.Manifest;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Targets;

namespace helengine.baseplatform.tests.Requests {
    /// <summary>
    /// Builds minimal valid platform build requests so request-level tests can focus on one property at a time.
    /// </summary>
    public static class PlatformBuildRequestTestFactory {
        /// <summary>
        /// Filesystem root used for both the output and working roots; requests never create it on disk.
        /// </summary>
        const string RequestRoot = @"C:\dev\helworks\builds\helengine\play-tests\request";

        /// <summary>
        /// Creates one minimal request through either the short or the long constructor.
        /// </summary>
        /// <param name="scope">Execution scope passed to the long constructor; ignored by the short constructor, which always builds fully.</param>
        /// <param name="useShortCtor">True to use the five-argument constructor, false to use the long constructor with every selection argument.</param>
        /// <returns>A valid request with one target variant and its matching cook profile.</returns>
        public static PlatformBuildRequest Create(PlatformBuildExecutionScope scope, bool useShortCtor) {
            PlatformBuildManifest manifest = CreateManifest();
            PlatformBuildTargetVariant[] targetVariants = [
                new PlatformBuildTargetVariant("ps2-target", "ps2", "gskit", "shared")
            ];
            PlatformCookProfile[] cookProfiles = [
                new PlatformCookProfile(
                    "shared",
                    "Shared",
                    new PlatformCookProfileCapabilities(
                        "shared-runtime",
                        "bc",
                        "ogg",
                        "scene",
                        PlatformSerializationEndianness.LittleEndian))
            ];

            if (useShortCtor) {
                return new PlatformBuildRequest(manifest, targetVariants, cookProfiles, RequestRoot, RequestRoot);
            }

            return new PlatformBuildRequest(
                manifest,
                targetVariants,
                cookProfiles,
                RequestRoot,
                RequestRoot,
                "",
                "",
                "",
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                new Dictionary<string, string>(),
                "",
                "",
                "",
                "release",
                "",
                scope);
        }

        /// <summary>
        /// Creates the smallest manifest the request constructor accepts: one scene, one asset and one cooked artifact.
        /// </summary>
        /// <returns>A fully populated manifest.</returns>
        static PlatformBuildManifest CreateManifest() {
            return new PlatformBuildManifest(
                2,
                "project",
                "1.0.0",
                "1.0.0",
                "ps2",
                "1.0.0",
                "scene",
                [
                    new PlatformBuildScene(
                        "scene",
                        "Scene",
                        "scene.source",
                        [
                            new PlatformBuildPayloadReference("payload", "payload.source")
                        ],
                        [])
                ],
                [
                    new PlatformBuildAsset(
                        "asset",
                        "Asset",
                        "asset.source",
                        new PlatformBuildPayloadReference("asset-payload", "asset-payload.source"),
                        [])
                ],
                [
                    new PlatformBuildArtifact("scene.hasset", "scene:scene", "sha256:scene", "scene", "shared")
                ],
                [],
                [],
                new PlatformContainerWritePlan(string.Empty, []),
                Array.Empty<PlatformCookWorkItem>(),
                PlatformBuildRuntimeFeatureManifest.Empty);
        }
    }
}

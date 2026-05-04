using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;

namespace helengine.baseplatform.tests.Builders;

/// <summary>
/// Provides one minimal builder instance for contract verification.
/// </summary>
public sealed class TestPlatformAssetBuilder : IPlatformAssetBuilder {
    /// <summary>
    /// Initializes one minimal test builder.
    /// </summary>
    public TestPlatformAssetBuilder() {
        Descriptor = new PlatformBuilderDescriptor(
            "test.builder",
            "1.0.0",
            "windows",
            new EngineCompatibilityRange("1.0.0", "999.0.0"),
            new ManifestCompatibilityRange(1, 1),
            ["windows"],
            ["debug"]);
        Definition = new PlatformDefinition(
            "windows",
            "Windows DirectX",
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
                    [])
            ],
            [],
            [
                new PlatformComponentCompatibilityDefinition(
                    "helengine.FPSComponent",
                    PlatformComponentCompatibilityKind.PassThrough,
                    "FPS overlay is canonical on this platform.",
                    string.Empty)
            ],
            [
                new PlatformCodegenProfileDefinition(
                    "default",
                    "Default",
                    "Default codegen profile",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [])
            ]);
    }

    /// <summary>
    /// Gets the builder descriptor used by the test instance.
    /// </summary>
    public PlatformBuilderDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the typed platform definition used by the test instance.
    /// </summary>
    public PlatformDefinition Definition { get; }

    /// <summary>
    /// Returns a minimal success report without mutating the request.
    /// </summary>
    /// <param name="request">The request to process.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    /// <param name="diagnosticReporter">The diagnostic reporter.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A completed success report.</returns>
    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        return Task.FromResult(new PlatformBuildReport(true, [], [], []));
    }
}

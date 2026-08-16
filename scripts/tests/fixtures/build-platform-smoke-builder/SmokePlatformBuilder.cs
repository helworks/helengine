using helengine.baseplatform.Builders;
using helengine.baseplatform.Definitions;
using helengine.baseplatform.Descriptors;
using helengine.baseplatform.Profiles;
using helengine.baseplatform.Reporting;
using helengine.baseplatform.Requests;
using helengine.baseplatform.Results;

namespace helengine.buildplatform.smokebuilder;

/// <summary>
/// Provides the smallest real platform builder needed by the direct-source wrapper smoke test.
/// </summary>
public sealed class SmokePlatformBuilder : IPlatformAssetBuilder {
    /// <summary>
    /// Initializes one smoke platform builder.
    /// </summary>
    public SmokePlatformBuilder() {
        Descriptor = new PlatformBuilderDescriptor(
            "helengine.buildplatform.smoke",
            "1.0.0",
            "smoke",
            new EngineCompatibilityRange("1.0.0", "1.0.0"),
            new ManifestCompatibilityRange(1, 1),
            ["smoke"],
            ["release"]);
        Definition = new PlatformDefinition(
            "smoke",
            "Build Platform Smoke",
            [
                new PlatformBuildProfileDefinition(
                    "release",
                    "Release",
                    "Minimal release smoke profile",
                    "smoke",
                    "smoke",
                    [])
            ],
            [
                new PlatformGraphicsProfileDefinition(
                    "smoke",
                    "Smoke",
                    "Minimal smoke graphics profile",
                    [])
            ],
            [],
            [],
            [],
            [
                new PlatformCodegenProfileDefinition(
                    "smoke",
                    "Smoke",
                    "Minimal smoke codegen profile",
                    PlatformCodegenLanguage.Cpp,
                    PlatformSerializationEndianness.LittleEndian,
                    [])
            ],
            [
                new PlatformStorageProfileDefinition(
                    "smoke",
                    "Smoke",
                    PlatformStorageProfileKind.LooseFiles,
                    "smoke-runtime",
                    false)
            ],
            [
                new PlatformMediaProfileDefinition(
                    "smoke",
                    "Smoke",
                    PlatformMediaLayoutKind.InstallTree,
                    false,
                    false)
            ]);
    }

    /// <summary>
    /// Gets the smoke builder descriptor.
    /// </summary>
    public PlatformBuilderDescriptor Descriptor { get; }

    /// <summary>
    /// Gets the smoke platform definition.
    /// </summary>
    public PlatformDefinition Definition { get; }

    /// <summary>
    /// Rejects material cooking because the smoke fixture contains no materials.
    /// </summary>
    /// <param name="request">Unused material cook request.</param>
    /// <returns>This method does not return.</returns>
    public PlatformMaterialCookResult CookMaterial(PlatformMaterialCookRequest request) {
        throw new NotSupportedException("The smoke platform fixture does not contain materials.");
    }

    /// <summary>
    /// Writes one direct-output marker and returns a successful platform report.
    /// </summary>
    /// <param name="request">Build request containing the direct output and stable native roots.</param>
    /// <param name="progressReporter">Progress reporter supplied by the editor.</param>
    /// <param name="diagnosticReporter">Diagnostic reporter supplied by the editor.</param>
    /// <param name="cancellationToken">Cancellation token supplied by the editor.</param>
    /// <returns>A completed successful platform report.</returns>
    public Task<PlatformBuildReport> BuildAsync(
        PlatformBuildRequest request,
        IPlatformBuildProgressReporter progressReporter,
        IPlatformBuildDiagnosticReporter diagnosticReporter,
        CancellationToken cancellationToken) {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(request.OutputRoot);
        File.WriteAllText(
            Path.Combine(request.OutputRoot, "smoke-build.txt"),
            Path.GetFullPath(request.WorkingRoot));
        return Task.FromResult(new PlatformBuildReport(true, [], [], []));
    }
}

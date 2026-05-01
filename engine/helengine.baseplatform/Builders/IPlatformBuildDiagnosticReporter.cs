using helengine.baseplatform.Reporting;

namespace helengine.baseplatform.Builders;

/// <summary>
/// Receives streamed diagnostics while a platform builder cooks scenes and assets.
/// </summary>
public interface IPlatformBuildDiagnosticReporter {
    /// <summary>
    /// Reports one diagnostic emitted during the build.
    /// </summary>
    /// <param name="diagnostic">The diagnostic emitted by the builder.</param>
    void Report(PlatformBuildDiagnostic diagnostic);
}

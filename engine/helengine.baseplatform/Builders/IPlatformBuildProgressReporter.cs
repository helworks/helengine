using helengine.baseplatform.Reporting;

namespace helengine.baseplatform.Builders;

/// <summary>
/// Receives streamed progress updates while a platform builder cooks scenes and assets.
/// </summary>
public interface IPlatformBuildProgressReporter {
    /// <summary>
    /// Reports one progress update produced during the build.
    /// </summary>
    /// <param name="update">The progress update emitted by the builder.</param>
    void Report(PlatformBuildProgressUpdate update);
}

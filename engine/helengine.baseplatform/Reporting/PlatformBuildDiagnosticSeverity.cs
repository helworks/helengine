namespace helengine.baseplatform.Reporting;

/// <summary>
/// Defines the severity level for a build diagnostic emitted during platform cooking.
/// </summary>
public enum PlatformBuildDiagnosticSeverity {
    /// <summary>
    /// Indicates an informational diagnostic that does not require user action.
    /// </summary>
    Information,

    /// <summary>
    /// Indicates a warning diagnostic that should be surfaced but does not stop the build by itself.
    /// </summary>
    Warning,

    /// <summary>
    /// Indicates an error diagnostic that represents a build failure.
    /// </summary>
    Error,
}

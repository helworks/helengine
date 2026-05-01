namespace helengine.baseplatform.Reporting;

/// <summary>
/// Carries a structured diagnostic emitted while cooking platform content.
/// </summary>
public class PlatformBuildDiagnostic {
    /// <summary>
    /// Initializes a new diagnostic with explicit severity, identity, and source context.
    /// </summary>
    /// <param name="severity">The severity level for the diagnostic.</param>
    /// <param name="code">The stable diagnostic code.</param>
    /// <param name="message">The human-readable diagnostic message.</param>
    /// <param name="sceneId">The scene identifier associated with the diagnostic, when applicable.</param>
    /// <param name="assetId">The loose asset identifier associated with the diagnostic, when applicable.</param>
    /// <param name="sourceIdentity">The source identity associated with the diagnostic.</param>
    public PlatformBuildDiagnostic(
        PlatformBuildDiagnosticSeverity severity,
        string code,
        string message,
        string sceneId,
        string assetId,
        string sourceIdentity) {
        if (string.IsNullOrWhiteSpace(code)) {
            throw new ArgumentException("Diagnostic code is required.", nameof(code));
        } else if (string.IsNullOrWhiteSpace(message)) {
            throw new ArgumentException("Diagnostic message is required.", nameof(message));
        } else if (string.IsNullOrWhiteSpace(sourceIdentity)) {
            throw new ArgumentException("Source identity is required.", nameof(sourceIdentity));
        }

        Severity = severity;
        Code = code;
        Message = message;
        SceneId = sceneId ?? string.Empty;
        AssetId = assetId ?? string.Empty;
        SourceIdentity = sourceIdentity;
    }

    /// <summary>
    /// Gets the severity level for the diagnostic.
    /// </summary>
    public PlatformBuildDiagnosticSeverity Severity { get; }

    /// <summary>
    /// Gets the stable diagnostic code.
    /// </summary>
    public string Code { get; }

    /// <summary>
    /// Gets the human-readable diagnostic message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    /// Gets the scene identifier associated with the diagnostic when the issue belongs to a scene.
    /// </summary>
    public string SceneId { get; }

    /// <summary>
    /// Gets the loose asset identifier associated with the diagnostic when the issue belongs to a loose asset.
    /// </summary>
    public string AssetId { get; }

    /// <summary>
    /// Gets the source identity that produced the diagnostic.
    /// </summary>
    public string SourceIdentity { get; }
}

namespace helengine.baseplatform.Reporting;

/// <summary>
/// Captures the final result of one platform asset build, including diagnostics and per-item outcomes.
/// </summary>
public class PlatformBuildReport {
    /// <summary>
    /// Initializes a new build report with final outcomes for scenes and loose assets.
    /// </summary>
    /// <param name="succeeded">Whether the overall build completed successfully.</param>
    /// <param name="diagnostics">The diagnostics emitted during the build.</param>
    /// <param name="sceneOutcomes">The final outcomes for all scenes in the request.</param>
    /// <param name="looseAssetOutcomes">The final outcomes for all loose assets in the request.</param>
    public PlatformBuildReport(
        bool succeeded,
        PlatformBuildDiagnostic[] diagnostics,
        PlatformBuildItemOutcome[] sceneOutcomes,
        PlatformBuildItemOutcome[] looseAssetOutcomes) {
        if (diagnostics == null) {
            throw new ArgumentNullException(nameof(diagnostics));
        } else if (sceneOutcomes == null) {
            throw new ArgumentNullException(nameof(sceneOutcomes));
        } else if (looseAssetOutcomes == null) {
            throw new ArgumentNullException(nameof(looseAssetOutcomes));
        } else if (Array.Exists(diagnostics, diagnostic => diagnostic == null)) {
            throw new ArgumentException("Diagnostics cannot contain null entries.", nameof(diagnostics));
        } else if (Array.Exists(sceneOutcomes, outcome => outcome == null)) {
            throw new ArgumentException("Scene outcomes cannot contain null entries.", nameof(sceneOutcomes));
        } else if (Array.Exists(looseAssetOutcomes, outcome => outcome == null)) {
            throw new ArgumentException("Loose asset outcomes cannot contain null entries.", nameof(looseAssetOutcomes));
        }

        Succeeded = succeeded;
        Diagnostics = [.. diagnostics];
        SceneOutcomes = [.. sceneOutcomes];
        LooseAssetOutcomes = [.. looseAssetOutcomes];
    }

    /// <summary>
    /// Gets whether the overall build completed successfully.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets the diagnostics emitted during the build.
    /// </summary>
    public PlatformBuildDiagnostic[] Diagnostics { get; }

    /// <summary>
    /// Gets the final outcomes for all scenes included in the build.
    /// </summary>
    public PlatformBuildItemOutcome[] SceneOutcomes { get; }

    /// <summary>
    /// Gets the final outcomes for all loose assets included in the build.
    /// </summary>
    public PlatformBuildItemOutcome[] LooseAssetOutcomes { get; }
}

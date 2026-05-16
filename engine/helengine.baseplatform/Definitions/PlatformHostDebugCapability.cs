namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes whether one platform supports host-debug execution and how the runner behaves.
/// </summary>
public sealed class PlatformHostDebugCapability {
    /// <summary>
    /// Initializes one platform host-debug capability description.
    /// </summary>
    /// <param name="supportsHostDebug">Whether the platform supports host-debug execution.</param>
    /// <param name="runnerKind">How the host-debug runner is launched.</param>
    /// <param name="requiresPackagedExportArtifacts">Whether the runner consumes the normal packaged export outputs.</param>
    /// <param name="supportsSingleStepSceneLoad">Whether the runner supports a load-only startup-scene mode.</param>
    /// <param name="supportsSingleStepDraw">Whether the runner supports a one-shot draw mode.</param>
    /// <param name="runnerId">Stable platform-defined host-debug runner identifier.</param>
    public PlatformHostDebugCapability(
        bool supportsHostDebug,
        PlatformHostDebugRunnerKind runnerKind,
        bool requiresPackagedExportArtifacts,
        bool supportsSingleStepSceneLoad,
        bool supportsSingleStepDraw,
        string runnerId) {
        if (string.IsNullOrWhiteSpace(runnerId)) {
            throw new ArgumentException("Host-debug runner id must be provided.", nameof(runnerId));
        }

        SupportsHostDebug = supportsHostDebug;
        RunnerKind = runnerKind;
        RequiresPackagedExportArtifacts = requiresPackagedExportArtifacts;
        SupportsSingleStepSceneLoad = supportsSingleStepSceneLoad;
        SupportsSingleStepDraw = supportsSingleStepDraw;
        RunnerId = runnerId;
    }

    /// <summary>
    /// Gets whether the platform supports host-debug execution.
    /// </summary>
    public bool SupportsHostDebug { get; }

    /// <summary>
    /// Gets how the host-debug runner is launched.
    /// </summary>
    public PlatformHostDebugRunnerKind RunnerKind { get; }

    /// <summary>
    /// Gets whether the runner requires the packaged export artifacts from the normal build output.
    /// </summary>
    public bool RequiresPackagedExportArtifacts { get; }

    /// <summary>
    /// Gets whether the runner supports load-only startup-scene execution.
    /// </summary>
    public bool SupportsSingleStepSceneLoad { get; }

    /// <summary>
    /// Gets whether the runner supports one-shot draw execution.
    /// </summary>
    public bool SupportsSingleStepDraw { get; }

    /// <summary>
    /// Gets the stable platform-defined host-debug runner identifier.
    /// </summary>
    public string RunnerId { get; }

    /// <summary>
    /// Creates the default disabled host-debug capability for platforms that do not opt in.
    /// </summary>
    /// <returns>Disabled host-debug capability.</returns>
    public static PlatformHostDebugCapability CreateDefault() {
        return new PlatformHostDebugCapability(
            false,
            PlatformHostDebugRunnerKind.None,
            false,
            false,
            false,
            "none");
    }
}

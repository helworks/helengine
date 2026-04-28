namespace helengine.editor.launcher.Models;

/// <summary>
/// Binds one installed engine-platform pair to the exact shared artifacts it reuses.
/// </summary>
public sealed class InstalledEnginePlatformBinding {
    /// <summary>
    /// Initializes one installed engine-platform binding.
    /// </summary>
    /// <param name="engineVersion">Exact installed engine version.</param>
    /// <param name="platformId">Stable platform identifier.</param>
    /// <param name="sdkIdentity">Exact shared SDK identity used by the engine platform.</param>
    /// <param name="platformBuilderIdentity">Exact shared platform-builder identity used by the engine platform.</param>
    /// <param name="platformFilesIdentity">Exact shared platform-files identity used by the engine platform.</param>
    public InstalledEnginePlatformBinding(
        string engineVersion,
        string platformId,
        ArtifactIdentity sdkIdentity,
        ArtifactIdentity platformBuilderIdentity,
        ArtifactIdentity platformFilesIdentity) {
        EngineVersion = engineVersion;
        PlatformId = platformId;
        SdkIdentity = sdkIdentity;
        PlatformBuilderIdentity = platformBuilderIdentity;
        PlatformFilesIdentity = platformFilesIdentity;
    }

    /// <summary>
    /// Gets the exact installed engine version.
    /// </summary>
    public string EngineVersion { get; }

    /// <summary>
    /// Gets the stable platform identifier for the binding.
    /// </summary>
    public string PlatformId { get; }

    /// <summary>
    /// Gets the exact shared SDK identity used by the engine platform.
    /// </summary>
    public ArtifactIdentity SdkIdentity { get; }

    /// <summary>
    /// Gets the exact shared platform-builder identity used by the engine platform.
    /// </summary>
    public ArtifactIdentity PlatformBuilderIdentity { get; }

    /// <summary>
    /// Gets the exact shared platform-files identity used by the engine platform.
    /// </summary>
    public ArtifactIdentity PlatformFilesIdentity { get; }
}

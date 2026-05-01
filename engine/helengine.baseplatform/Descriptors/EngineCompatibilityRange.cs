namespace helengine.baseplatform.Descriptors;

/// <summary>
/// Defines the inclusive engine-version bounds a platform builder supports.
/// </summary>
public class EngineCompatibilityRange {
    /// <summary>
    /// Initializes a new compatibility range with inclusive minimum and maximum versions.
    /// </summary>
    /// <param name="minimumVersion">The inclusive minimum engine version supported by the builder.</param>
    /// <param name="maximumVersion">The inclusive maximum engine version supported by the builder.</param>
    /// <exception cref="ArgumentException">Thrown when either bound is missing.</exception>
    public EngineCompatibilityRange(string minimumVersion, string maximumVersion) {
        if (string.IsNullOrWhiteSpace(minimumVersion)) {
            throw new ArgumentException("Minimum engine version is required.", nameof(minimumVersion));
        } else if (string.IsNullOrWhiteSpace(maximumVersion)) {
            throw new ArgumentException("Maximum engine version is required.", nameof(maximumVersion));
        } else if (CompareVersions(minimumVersion, maximumVersion) > 0) {
            throw new ArgumentException("Maximum engine version must be greater than or equal to the minimum engine version.", nameof(maximumVersion));
        }

        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
    }

    /// <summary>
    /// Gets the inclusive minimum engine version supported by the builder.
    /// </summary>
    public string MinimumVersion { get; }

    /// <summary>
    /// Gets the inclusive maximum engine version supported by the builder.
    /// </summary>
    public string MaximumVersion { get; }

    /// <summary>
    /// Compares two engine versions after removing build metadata that does not affect ordering.
    /// </summary>
    /// <param name="leftVersion">The left version operand.</param>
    /// <param name="rightVersion">The right version operand.</param>
    /// <returns>A signed value describing the relative ordering of the two versions.</returns>
    /// <exception cref="ArgumentException">Thrown when either version string cannot be parsed.</exception>
    static int CompareVersions(string leftVersion, string rightVersion) {
        var parsedLeftVersion = ParseVersion(leftVersion);
        var parsedRightVersion = ParseVersion(rightVersion);

        return parsedLeftVersion.CompareTo(parsedRightVersion);
    }

    /// <summary>
    /// Parses an engine version into a comparable semantic version by removing build metadata.
    /// </summary>
    /// <param name="version">The version text to parse.</param>
    /// <returns>A comparable <see cref="Version"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when the version cannot be parsed as a .NET version.</exception>
    static Version ParseVersion(string version) {
        var versionWithoutBuildMetadata = version.Split('+', 2)[0];

        if (!Version.TryParse(versionWithoutBuildMetadata, out var parsedVersion)) {
            throw new ArgumentException($"Engine version '{version}' is not a valid comparable version.", nameof(version));
        }

        return parsedVersion;
    }
}

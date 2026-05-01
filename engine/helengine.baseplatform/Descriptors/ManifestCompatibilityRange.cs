namespace helengine.baseplatform.Descriptors;

/// <summary>
/// Defines the inclusive manifest-version bounds a platform builder supports.
/// </summary>
public class ManifestCompatibilityRange {
    /// <summary>
    /// Initializes a new manifest compatibility range with inclusive minimum and maximum versions.
    /// </summary>
    /// <param name="minimumVersion">The inclusive minimum manifest version supported by the builder.</param>
    /// <param name="maximumVersion">The inclusive maximum manifest version supported by the builder.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when either version is less than one.</exception>
    /// <exception cref="ArgumentException">Thrown when the maximum version is lower than the minimum version.</exception>
    public ManifestCompatibilityRange(int minimumVersion, int maximumVersion) {
        if (minimumVersion < 1) {
            throw new ArgumentOutOfRangeException(nameof(minimumVersion), "Minimum manifest version must be at least 1.");
        } else if (maximumVersion < 1) {
            throw new ArgumentOutOfRangeException(nameof(maximumVersion), "Maximum manifest version must be at least 1.");
        } else if (maximumVersion < minimumVersion) {
            throw new ArgumentException("Maximum manifest version must be greater than or equal to the minimum manifest version.", nameof(maximumVersion));
        }

        MinimumVersion = minimumVersion;
        MaximumVersion = maximumVersion;
    }

    /// <summary>
    /// Gets the inclusive minimum manifest version supported by the builder.
    /// </summary>
    public int MinimumVersion { get; }

    /// <summary>
    /// Gets the inclusive maximum manifest version supported by the builder.
    /// </summary>
    public int MaximumVersion { get; }
}

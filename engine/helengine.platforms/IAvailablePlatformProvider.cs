namespace helengine.platforms;

/// <summary>
/// Defines the contract for one platform source that may or may not have platform state available on the current machine.
/// </summary>
public interface IAvailablePlatformProvider {
    /// <summary>
    /// Attempts to load the available platforms for one exact engine version.
    /// </summary>
    /// <param name="engineVersion">Exact engine version whose available platforms should be loaded.</param>
    /// <param name="platforms">Resolved platforms when the provider has state available.</param>
    /// <returns><c>true</c> when the provider had state to evaluate; otherwise <c>false</c>.</returns>
    bool TryLoadPlatforms(string engineVersion, out IReadOnlyList<AvailablePlatformDescriptor> platforms);
}

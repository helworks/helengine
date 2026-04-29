namespace helengine.platforms;

/// <summary>
/// Describes one platform that can be selected in editor build settings.
/// </summary>
public sealed class AvailablePlatformDescriptor {
    /// <summary>
    /// Initializes one available-platform descriptor.
    /// </summary>
    /// <param name="id">Stable platform identifier written into project files.</param>
    /// <param name="displayName">Readable platform name shown in editor UI.</param>
    public AvailablePlatformDescriptor(string id, string displayName) {
        Id = id;
        DisplayName = displayName;
    }

    /// <summary>
    /// Gets the stable platform identifier written into project files.
    /// </summary>
    public string Id { get; }

    /// <summary>
    /// Gets the readable platform name shown in editor UI.
    /// </summary>
    public string DisplayName { get; }
}

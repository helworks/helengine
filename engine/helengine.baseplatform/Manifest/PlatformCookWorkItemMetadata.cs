namespace helengine.baseplatform.Manifest;

/// <summary>
/// Stores one generic metadata entry attached to a builder-owned platform cook work item.
/// </summary>
public sealed class PlatformCookWorkItemMetadata {
    /// <summary>
    /// Creates one metadata entry for a platform cook work item.
    /// </summary>
    /// <param name="key">Stable metadata key.</param>
    /// <param name="value">Metadata value associated with the key.</param>
    public PlatformCookWorkItemMetadata(string key, string value) {
        if (string.IsNullOrWhiteSpace(key)) {
            throw new ArgumentException("Metadata key is required.", nameof(key));
        } else if (value == null) {
            throw new ArgumentNullException(nameof(value), "Metadata value is required.");
        }

        Key = key;
        Value = value;
    }

    /// <summary>
    /// Gets the stable metadata key.
    /// </summary>
    public string Key { get; }

    /// <summary>
    /// Gets the metadata value associated with the key.
    /// </summary>
    public string Value { get; }
}

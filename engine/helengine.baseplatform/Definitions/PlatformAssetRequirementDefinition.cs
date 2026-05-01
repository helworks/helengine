namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one asset class a platform builder expects the editor to provide.
/// </summary>
public class PlatformAssetRequirementDefinition {
    /// <summary>
    /// Initializes one asset requirement definition.
    /// </summary>
    /// <param name="requirementId">Stable asset requirement identifier.</param>
    /// <param name="displayName">Human-readable label shown in the editor.</param>
    /// <param name="required">Whether the asset requirement must be satisfied.</param>
    /// <param name="supportedFileExtensions">The file extensions accepted for this asset requirement.</param>
    public PlatformAssetRequirementDefinition(
        string requirementId,
        string displayName,
        bool required,
        string[] supportedFileExtensions) {
        if (string.IsNullOrWhiteSpace(requirementId)) {
            throw new ArgumentException("Asset requirement id is required.", nameof(requirementId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Asset requirement display name is required.", nameof(displayName));
        } else if (supportedFileExtensions == null) {
            throw new ArgumentNullException(nameof(supportedFileExtensions), "Supported file extensions are required.");
        } else if (Array.Exists(supportedFileExtensions, extension => string.IsNullOrWhiteSpace(extension))) {
            throw new ArgumentException("Supported file extensions cannot contain blank entries.", nameof(supportedFileExtensions));
        }

        RequirementId = requirementId;
        DisplayName = displayName;
        Required = required;
        SupportedFileExtensions = [.. supportedFileExtensions];
    }

    /// <summary>
    /// Gets the stable asset requirement identifier.
    /// </summary>
    public string RequirementId { get; }

    /// <summary>
    /// Gets the human-readable label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets whether the asset requirement must be satisfied.
    /// </summary>
    public bool Required { get; }

    /// <summary>
    /// Gets the file extensions accepted for this asset requirement.
    /// </summary>
    public string[] SupportedFileExtensions { get; }
}

namespace helengine.baseplatform.Requests;

/// <summary>
/// Describes one builder-owned material translation request for a single target platform and schema payload.
/// </summary>
public sealed class PlatformMaterialCookRequest {
    /// <summary>
    /// Initializes one material cook request.
    /// </summary>
    /// <param name="materialAssetId">Asset identifier of the source material being translated.</param>
    /// <param name="materialRelativePath">Project-relative material path being cooked.</param>
    /// <param name="targetPlatformId">Target platform identifier that owns the schema semantics.</param>
    /// <param name="selectedBuildProfileId">Selected build profile identifier for the current build.</param>
    /// <param name="selectedGraphicsProfileId">Selected graphics profile identifier for the current build.</param>
    /// <param name="schemaId">Selected builder-defined schema identifier.</param>
    /// <param name="fieldValues">Serialized field values keyed by builder-defined field identifier.</param>
    public PlatformMaterialCookRequest(
        string materialAssetId,
        string materialRelativePath,
        string targetPlatformId,
        string selectedBuildProfileId,
        string selectedGraphicsProfileId,
        string schemaId,
        IReadOnlyDictionary<string, string> fieldValues) {
        if (string.IsNullOrWhiteSpace(materialAssetId)) {
            throw new ArgumentException("Material asset id is required.", nameof(materialAssetId));
        } else if (string.IsNullOrWhiteSpace(materialRelativePath)) {
            throw new ArgumentException("Material relative path is required.", nameof(materialRelativePath));
        } else if (string.IsNullOrWhiteSpace(targetPlatformId)) {
            throw new ArgumentException("Target platform id is required.", nameof(targetPlatformId));
        } else if (schemaId == null) {
            throw new ArgumentNullException(nameof(schemaId), "Material schema id is required.");
        } else if (fieldValues == null) {
            throw new ArgumentNullException(nameof(fieldValues), "Material field values are required.");
        }

        MaterialAssetId = materialAssetId;
        MaterialRelativePath = materialRelativePath;
        TargetPlatformId = targetPlatformId;
        SelectedBuildProfileId = selectedBuildProfileId ?? string.Empty;
        SelectedGraphicsProfileId = selectedGraphicsProfileId ?? string.Empty;
        SchemaId = schemaId;
        FieldValues = new Dictionary<string, string>(fieldValues, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Gets the asset identifier of the source material being translated.
    /// </summary>
    public string MaterialAssetId { get; }

    /// <summary>
    /// Gets the project-relative material path being cooked.
    /// </summary>
    public string MaterialRelativePath { get; }

    /// <summary>
    /// Gets the target platform identifier that owns the schema semantics.
    /// </summary>
    public string TargetPlatformId { get; }

    /// <summary>
    /// Gets the selected build profile identifier for the current build.
    /// </summary>
    public string SelectedBuildProfileId { get; }

    /// <summary>
    /// Gets the selected graphics profile identifier for the current build.
    /// </summary>
    public string SelectedGraphicsProfileId { get; }

    /// <summary>
    /// Gets the selected builder-defined schema identifier.
    /// </summary>
    public string SchemaId { get; }

    /// <summary>
    /// Gets the serialized field values keyed by builder-defined field identifier.
    /// </summary>
    public IReadOnlyDictionary<string, string> FieldValues { get; }
}

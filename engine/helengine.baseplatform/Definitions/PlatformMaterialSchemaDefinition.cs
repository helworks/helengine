namespace helengine.baseplatform.Definitions;

/// <summary>
/// Describes one builder-defined material authoring schema exposed to the editor.
/// </summary>
public class PlatformMaterialSchemaDefinition {
    /// <summary>
    /// Initializes one material schema definition.
    /// </summary>
    /// <param name="schemaId">Stable material schema identifier.</param>
    /// <param name="displayName">Human-readable schema label shown in the editor.</param>
    /// <param name="graphicsProfileIds">Graphics profile identifiers that can use this schema, or an empty set when the schema applies to every graphics profile.</param>
    /// <param name="fields">Fields the editor should render for the schema.</param>
    public PlatformMaterialSchemaDefinition(
        string schemaId,
        string displayName,
        string[] graphicsProfileIds,
        PlatformMaterialFieldDefinition[] fields) {
        if (string.IsNullOrWhiteSpace(schemaId)) {
            throw new ArgumentException("Material schema id is required.", nameof(schemaId));
        } else if (string.IsNullOrWhiteSpace(displayName)) {
            throw new ArgumentException("Material schema display name is required.", nameof(displayName));
        } else if (graphicsProfileIds == null) {
            throw new ArgumentNullException(nameof(graphicsProfileIds), "Material schema graphics profile ids are required.");
        } else if (Array.Exists(graphicsProfileIds, graphicsProfileId => string.IsNullOrWhiteSpace(graphicsProfileId))) {
            throw new ArgumentException("Material schema graphics profile ids cannot contain blank entries.", nameof(graphicsProfileIds));
        } else if (fields == null) {
            throw new ArgumentNullException(nameof(fields), "Material schema fields are required.");
        } else if (Array.Exists(fields, field => field == null)) {
            throw new ArgumentException("Material schema fields cannot contain null entries.", nameof(fields));
        }

        SchemaId = schemaId;
        DisplayName = displayName;
        GraphicsProfileIds = [.. graphicsProfileIds];
        Fields = [.. fields];
    }

    /// <summary>
    /// Gets the stable material schema identifier.
    /// </summary>
    public string SchemaId { get; }

    /// <summary>
    /// Gets the human-readable schema label shown in the editor.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the graphics profile identifiers that can use this schema, or an empty set when the schema applies to every graphics profile.
    /// </summary>
    public string[] GraphicsProfileIds { get; }

    /// <summary>
    /// Gets the fields the editor should render for the schema.
    /// </summary>
    public PlatformMaterialFieldDefinition[] Fields { get; }
}

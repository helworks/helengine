using System.Text.Json.Serialization;

namespace helengine.editor {
    /// <summary>
    /// Represents one platform's persisted build and graphics profile values.
    /// </summary>
    public sealed class EditorPlatformProfileSettingsDocument {
        /// <summary>
        /// Gets or sets the platform identifier this profile record belongs to.
        /// </summary>
        public string PlatformId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the build-profile values used when cooking assets for this platform.
        /// </summary>
        public EditorBuildProfileSettingsDocument Build { get; set; } = new EditorBuildProfileSettingsDocument();

        /// <summary>
        /// Gets or sets the graphics-profile values used when configuring the runtime player for this platform.
        /// </summary>
        public EditorGraphicsProfileSettingsDocument Graphics { get; set; } = new EditorGraphicsProfileSettingsDocument();

        /// <summary>
        /// Gets or sets the cached builder metadata for the selected platform.
        /// </summary>
        [JsonIgnore]
        public EditorPlatformBuildSelectionModel SelectionModel { get; set; }
    }
}

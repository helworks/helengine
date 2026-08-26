namespace helengine.editor {
    /// <summary>
    /// Defines one registered platform settings section.
    /// </summary>
    public interface IAssetPlatformSettingsSectionDefinition {
        /// <summary>
        /// Gets the registered section identifier.
        /// </summary>
        string SectionId { get; }

        /// <summary>
        /// Gets the payload type owned by the section.
        /// </summary>
        Type SettingsType { get; }

        /// <summary>
        /// Creates one default payload for the section.
        /// </summary>
        /// <returns>Default payload instance.</returns>
        object CreateDefaultSettings();

        /// <summary>
        /// Creates one deep clone of the supplied section payload.
        /// </summary>
        /// <param name="settings">Payload instance to clone.</param>
        /// <returns>Cloned payload instance.</returns>
        object CloneSettings(object settings);

        /// <summary>
        /// Returns whether two payload instances carry the same values.
        /// </summary>
        /// <param name="left">First payload instance.</param>
        /// <param name="right">Second payload instance.</param>
        /// <returns>True when both payloads match.</returns>
        bool SettingsEqual(object left, object right);

        /// <summary>
        /// Serializes one payload into the asset import settings stream.
        /// </summary>
        /// <param name="writer">Writer that owns the destination stream.</param>
        /// <param name="settings">Payload instance to serialize.</param>
        void Serialize(EngineBinaryWriter writer, object settings);

        /// <summary>
        /// Deserializes one payload from the asset import settings stream.
        /// </summary>
        /// <param name="reader">Reader positioned at the payload body.</param>
        /// <returns>Deserialized payload instance.</returns>
        object Deserialize(EngineBinaryReader reader);
    }
}

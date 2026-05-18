namespace helengine {
    /// <summary>
    /// Stores authored scene-id remapping entries that optional runtime callers can consult before loading scenes.
    /// </summary>
    public sealed class SceneMapComponent : Component {
        /// <summary>
        /// Current cooked payload version used by runtime scene persistence.
        /// </summary>
        public const byte CurrentVersion = 1;

        /// <summary>
        /// Stable serialized component type id used by scene persistence.
        /// </summary>
        public const string SerializedComponentTypeId = "helengine.SceneMapComponent";

        /// <summary>
        /// Initializes one empty scene-map component.
        /// </summary>
        public SceneMapComponent() {
            Mappings = new Dictionary<string, string>(StringComparer.Ordinal);
        }

        /// <summary>
        /// Gets the authored mapping entries keyed by logical source scene id.
        /// </summary>
        [EditorPropertyDisplayName("Scene Mappings")]
        [EditorPropertyOrder(0)]
        public Dictionary<string, string> Mappings { get; }
    }
}

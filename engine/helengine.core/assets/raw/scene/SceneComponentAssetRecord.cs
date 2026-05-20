namespace helengine {
    /// <summary>
    /// Stores one persisted component payload inside a scene entity record.
    /// </summary>
    public class SceneComponentAssetRecord {
        /// <summary>
        /// Tracks the number of transient component records that have been constructed and not explicitly released by the runtime scene loader.
        /// </summary>
        static int LiveInstanceCountValue;

        /// <summary>
        /// Initializes a serialized component record and records the transient diagnostic lifetime.
        /// </summary>
        public SceneComponentAssetRecord() {
            LiveInstanceCountValue++;
        }

        /// <summary>
        /// Gets the number of serialized component records currently considered live by transient release diagnostics.
        /// </summary>
        public static int LiveInstanceCount => LiveInstanceCountValue;

        /// <summary>
        /// Marks this serialized component record as released by the runtime transient-scene cleanup path.
        /// </summary>
        public void MarkReleasedForDiagnostics() {
            LiveInstanceCountValue--;
        }

        /// <summary>
        /// Gets or sets the stable editor component key used to match authored components across platform overrides.
        /// </summary>
        public string ComponentKey { get; set; }

        /// <summary>
        /// Gets or sets the stable serialized type identifier for the component.
        /// </summary>
        public string ComponentTypeId { get; set; }

        /// <summary>
        /// Gets or sets the entity-local component index used to preserve component ordering.
        /// </summary>
        public int ComponentIndex { get; set; }

        /// <summary>
        /// Gets or sets the opaque component payload bytes.
        /// </summary>
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }
}

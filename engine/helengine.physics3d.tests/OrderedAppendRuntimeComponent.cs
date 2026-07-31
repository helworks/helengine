namespace helengine.physics3d.tests {
    /// <summary>
    /// Provides deliberately non-alphabetical append members for runtime ordinal deserialization tests.
    /// </summary>
    public sealed class OrderedAppendRuntimeComponent : Component {
        /// <summary>
        /// Gets or sets the required value that begins every payload.
        /// </summary>
        public int RequiredValue { get; set; }

        /// <summary>
        /// Gets or sets the first append-only value despite its alphabetically later name.
        /// </summary>
        [ScenePersistenceAppend(0)]
        public int ZuluExtension { get; set; }

        /// <summary>
        /// Gets or sets the second append-only value despite its alphabetically earlier name.
        /// </summary>
        [ScenePersistenceAppend(1)]
        public int AlphaExtension { get; set; }
    }
}

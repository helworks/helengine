namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides required and deliberately non-alphabetical appended members for ordinal schema ordering tests.
    /// </summary>
    public sealed class TestOrderedAppendComponent : Component {
        /// <summary>
        /// Gets or sets the required value that must precede every optional extension.
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

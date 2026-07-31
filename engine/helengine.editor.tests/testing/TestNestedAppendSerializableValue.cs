namespace helengine.editor.tests.testing {
    /// <summary>
    /// Provides a nested authored value with an append marker that is invalid without nested payload framing.
    /// </summary>
    public sealed class TestNestedAppendSerializableValue {
        /// <summary>
        /// Gets or sets the required nested value written before the invalid extension member.
        /// </summary>
        public int RequiredValue { get; set; }

        /// <summary>
        /// Gets or sets the member that must not participate in nested append ordering.
        /// </summary>
        [ScenePersistenceAppend(0)]
        public int AppendedValue { get; set; }
    }
}

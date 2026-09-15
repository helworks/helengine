namespace helengine {
    /// <summary>
    /// Represents a single labeled debug value contributed by a debug info provider.
    /// </summary>
    public class DebugInfoEntry {
        /// <summary>
        /// Initializes a new debug info entry.
        /// </summary>
        /// <param name="category">Category label under which the entry is grouped.</param>
        /// <param name="key">Display name of the entry.</param>
        /// <param name="value">Display value of the entry.</param>
        public DebugInfoEntry(string category, string key, string value) {
            Category = category;
            Key = key;
            Value = value;
        }

        /// <summary>
        /// Gets the category label under which this entry is grouped.
        /// </summary>
        public string Category { get; }

        /// <summary>
        /// Gets the display name of this entry.
        /// </summary>
        public string Key { get; }

        /// <summary>
        /// Gets the display value of this entry.
        /// </summary>
        public string Value { get; }
    }
}

namespace helengine.timeline {
    /// <summary>
    /// Builds the deduplicated string table of a cooked timeline. Index 0 is always the empty string, used by markers
    /// without a value.
    /// </summary>
    sealed class CookedStringTable {
        /// <summary>
        /// Strings in index order.
        /// </summary>
        readonly List<string> Values = new List<string> { string.Empty };

        /// <summary>
        /// Index of each string already added.
        /// </summary>
        readonly Dictionary<string, int> Indices = new Dictionary<string, int>(StringComparer.Ordinal) { { string.Empty, 0 } };

        /// <summary>
        /// Returns the index of a string, adding it on first use.
        /// </summary>
        /// <param name="value">String to index (null is treated as empty).</param>
        /// <returns>The index.</returns>
        public int IndexOf(string value) {
            string key = value ?? string.Empty;
            int index;
            if (!Indices.TryGetValue(key, out index)) {
                index = Values.Count;
                Values.Add(key);
                Indices.Add(key, index);
            }
            return index;
        }

        /// <summary>
        /// Returns the table.
        /// </summary>
        /// <returns>The strings in index order.</returns>
        public string[] ToArray() {
            return Values.ToArray();
        }
    }
}

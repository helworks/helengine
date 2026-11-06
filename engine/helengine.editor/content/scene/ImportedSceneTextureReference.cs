namespace helengine.editor {
    /// <summary>
    /// Formats and parses the texture slots of <see cref="ImportedSceneMaterial"/>: an empty string means no texture, and "*K" refers to entry K of <see cref="ImportedSceneData.Textures"/>.
    /// </summary>
    public static class ImportedSceneTextureReference {
        /// <summary>
        /// Prefix of a texture slot that refers to an entry of the scene texture table.
        /// </summary>
        const string IndexPrefix = "*";

        /// <summary>
        /// Builds the slot value that refers to a texture table entry.
        /// </summary>
        /// <param name="textureIndex">Index into the scene texture table.</param>
        /// <returns>Slot value such as "*0".</returns>
        public static string Format(int textureIndex) {
            if (textureIndex < 0) {
                throw new ArgumentOutOfRangeException(nameof(textureIndex), textureIndex, "Texture index must not be negative.");
            }

            return IndexPrefix + textureIndex.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads the texture table index out of a material texture slot.
        /// </summary>
        /// <param name="slot">Slot value of a material.</param>
        /// <returns>The table index, or -1 when the slot is empty.</returns>
        public static int Parse(string slot) {
            if (slot == null) {
                throw new ArgumentNullException(nameof(slot));
            } else if (slot.Length == 0) {
                return -1;
            }

            int index;
            if (!slot.StartsWith(IndexPrefix, StringComparison.Ordinal)
                || !int.TryParse(slot.Substring(IndexPrefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out index)) {
                throw new InvalidOperationException($"Material texture slot '{slot}' is neither empty nor a texture table index like '*0'.");
            }

            return index;
        }
    }
}

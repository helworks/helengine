namespace helengine.editor {
    /// <summary>
    /// Name helpers that keep imported node names usable as file names and unique inside one scene.
    /// </summary>
    public static class ImportedSceneNameUtils {
        /// <summary>
        /// Name given to nodes whose source name is empty.
        /// </summary>
        const string EmptyNodeName = "Node";

        /// <summary>
        /// Replaces path separators, colons and every other invalid file-name character with underscores.
        /// </summary>
        /// <param name="name">Source name, possibly empty.</param>
        /// <returns>Safe name, or "Node" when the source name is empty.</returns>
        public static string MakeSafeName(string name) {
            if (name == null) {
                throw new ArgumentNullException(nameof(name));
            }
            if (name.Length == 0) {
                return EmptyNodeName;
            }

            char[] invalid = Path.GetInvalidFileNameChars();
            char[] characters = name.ToCharArray();
            for (int index = 0; index < characters.Length; index++) {
                char character = characters[index];
                if (character == '/' || character == '\\' || character == ':' || Array.IndexOf(invalid, character) >= 0) {
                    characters[index] = '_';
                }
            }

            return new string(characters);
        }

        /// <summary>
        /// Returns a name not yet in the used set, appending _2, _3 and so on to repeated names, and records the result.
        /// </summary>
        /// <param name="name">Already safe name.</param>
        /// <param name="usedNames">Names handed out so far; the returned name is added to it.</param>
        /// <returns>Unique name.</returns>
        public static string Disambiguate(string name, HashSet<string> usedNames) {
            if (name == null) {
                throw new ArgumentNullException(nameof(name));
            } else if (usedNames == null) {
                throw new ArgumentNullException(nameof(usedNames));
            }

            string candidate = name;
            int suffix = 2;
            while (!usedNames.Add(candidate)) {
                candidate = name + "_" + suffix;
                suffix++;
            }

            return candidate;
        }
    }
}

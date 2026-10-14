namespace helengine.editor {
    /// <summary>
    /// The effective Play settings for one platform: each declared setting's stored value, or its declared default when
    /// nothing is stored, plus every stored value the declarations reject.
    /// </summary>
    public sealed class EditorPlaySettingsEvaluation {
        /// <summary>
        /// Initializes one evaluation.
        /// </summary>
        /// <param name="filePath">Play settings file the values came from.</param>
        /// <param name="platformId">Platform the values were evaluated for.</param>
        /// <param name="commonValues">Effective common values keyed by id.</param>
        /// <param name="platformValues">Effective platform values keyed by id.</param>
        /// <param name="issues">Rejected stored values.</param>
        public EditorPlaySettingsEvaluation(
            string filePath,
            string platformId,
            IReadOnlyDictionary<string, string> commonValues,
            IReadOnlyDictionary<string, string> platformValues,
            IReadOnlyList<EditorPlaySettingIssue> issues) {
            FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
            PlatformId = platformId ?? throw new ArgumentNullException(nameof(platformId));
            CommonValues = commonValues ?? throw new ArgumentNullException(nameof(commonValues));
            PlatformValues = platformValues ?? throw new ArgumentNullException(nameof(platformValues));
            Issues = issues ?? throw new ArgumentNullException(nameof(issues));
        }

        /// <summary>Gets the Play settings file the values came from.</summary>
        public string FilePath { get; }

        /// <summary>Gets the platform the values were evaluated for.</summary>
        public string PlatformId { get; }

        /// <summary>Gets the effective common values keyed by id.</summary>
        public IReadOnlyDictionary<string, string> CommonValues { get; }

        /// <summary>Gets the effective platform values keyed by id.</summary>
        public IReadOnlyDictionary<string, string> PlatformValues { get; }

        /// <summary>Gets the stored values the declarations reject.</summary>
        public IReadOnlyList<EditorPlaySettingIssue> Issues { get; }

        /// <summary>Gets whether every effective value is accepted.</summary>
        public bool IsValid => Issues.Count == 0;

        /// <summary>
        /// Formats the message Play fails with while issues exist, naming the file, the platform and every rejected id.
        /// </summary>
        /// <returns>One-line failure message.</returns>
        public string FormatFailure() {
            List<string> descriptions = new List<string>();
            for (int index = 0; index < Issues.Count; index++) {
                descriptions.Add(Issues[index].Describe());
            }

            return $"Play settings in '{FilePath}' are invalid for platform '{PlatformId}': {string.Join("; ", descriptions)}. Fix them under Play Settings... in the Play platform selector.";
        }
    }
}

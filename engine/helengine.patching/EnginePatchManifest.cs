namespace helengine.patching {
    /// <summary>
    /// Defines the metadata and build rules for an engine patch.
    /// </summary>
    public sealed class EnginePatchManifest {
        /// <summary>
        /// Initializes a new manifest with safe defaults.
        /// </summary>
        public EnginePatchManifest() {
            Id = string.Empty;
            Version = string.Empty;
            Description = string.Empty;
            Dependencies = new List<string>();
            Conflicts = new List<string>();
            Defines = new List<string>();
            IncludeFiles = new List<string>();
            ExcludeFiles = new List<string>();
            Features = new List<EnginePatchFeature>();
        }

        /// <summary>
        /// Gets or sets the patch identifier.
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Gets or sets the patch version.
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// Gets or sets the patch description.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Gets or sets required patch identifiers.
        /// </summary>
        public List<string> Dependencies { get; set; }

        /// <summary>
        /// Gets or sets conflicting patch identifiers.
        /// </summary>
        public List<string> Conflicts { get; set; }

        /// <summary>
        /// Gets or sets conditional compilation symbols introduced by this patch.
        /// </summary>
        public List<string> Defines { get; set; }

        /// <summary>
        /// Gets or sets relative file paths to include in the build.
        /// </summary>
        public List<string> IncludeFiles { get; set; }

        /// <summary>
        /// Gets or sets relative file paths to exclude from the build.
        /// </summary>
        public List<string> ExcludeFiles { get; set; }

        /// <summary>
        /// Gets or sets features that this patch contributes to serialization.
        /// </summary>
        public List<EnginePatchFeature> Features { get; set; }

        /// <summary>
        /// Normalizes list values and removes empty entries.
        /// </summary>
        public void Normalize() {
            if (Id == null) {
                Id = string.Empty;
            }

            if (Version == null) {
                Version = string.Empty;
            }

            if (Description == null) {
                Description = string.Empty;
            }

            if (Dependencies == null) {
                Dependencies = new List<string>();
            }

            if (Conflicts == null) {
                Conflicts = new List<string>();
            }

            if (Defines == null) {
                Defines = new List<string>();
            }

            if (IncludeFiles == null) {
                IncludeFiles = new List<string>();
            }

            if (ExcludeFiles == null) {
                ExcludeFiles = new List<string>();
            }

            if (Features == null) {
                Features = new List<EnginePatchFeature>();
            }

            NormalizeList(Dependencies);
            NormalizeList(Conflicts);
            NormalizeList(Defines);
            NormalizeList(IncludeFiles);
            NormalizeList(ExcludeFiles);
            NormalizeFeatures();
        }

        /// <summary>
        /// Normalizes the entries in a list of strings.
        /// </summary>
        /// <param name="values">List to normalize.</param>
        void NormalizeList(List<string> values) {
            for (int i = values.Count - 1; i >= 0; i--) {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value)) {
                    values.RemoveAt(i);
                    continue;
                }

                values[i] = value.Trim();
            }
        }

        /// <summary>
        /// Normalizes feature entries and removes empty features.
        /// </summary>
        void NormalizeFeatures() {
            for (int i = Features.Count - 1; i >= 0; i--) {
                EnginePatchFeature feature = Features[i];
                if (feature == null) {
                    Features.RemoveAt(i);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(feature.Id)) {
                    Features.RemoveAt(i);
                    continue;
                }

                if (feature.Version == null) {
                    feature.Version = string.Empty;
                }

                if (feature.Description == null) {
                    feature.Description = string.Empty;
                }

                feature.Id = feature.Id.Trim();
            }
        }
    }
}

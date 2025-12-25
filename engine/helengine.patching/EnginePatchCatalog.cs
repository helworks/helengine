namespace helengine.patching {
    /// <summary>
    /// Loads and stores patch definitions discovered in a directory tree.
    /// </summary>
    public sealed class EnginePatchCatalog {
        readonly Dictionary<string, EnginePatchDefinition> patches;
        readonly List<string> errors;
        readonly EnginePatchManifestLoader loader;

        /// <summary>
        /// Initializes a new patch catalog.
        /// </summary>
        public EnginePatchCatalog() {
            patches = new Dictionary<string, EnginePatchDefinition>(StringComparer.OrdinalIgnoreCase);
            errors = new List<string>();
            loader = new EnginePatchManifestLoader();
        }

        /// <summary>
        /// Gets the loaded patch definitions.
        /// </summary>
        public IReadOnlyCollection<EnginePatchDefinition> Patches => patches.Values;

        /// <summary>
        /// Gets any errors encountered during loading.
        /// </summary>
        public IReadOnlyList<string> Errors => errors;

        /// <summary>
        /// Loads patch manifests from the provided root folder.
        /// </summary>
        /// <param name="rootPath">Root folder to scan for patch manifests.</param>
        public void LoadFromRoot(string rootPath) {
            patches.Clear();
            errors.Clear();

            if (string.IsNullOrWhiteSpace(rootPath)) {
                errors.Add("Patch root path is required.");
                return;
            }

            if (!Directory.Exists(rootPath)) {
                errors.Add("Patch root path does not exist.");
                return;
            }

            foreach (string manifestPath in Directory.EnumerateFiles(rootPath, "patch.json", SearchOption.AllDirectories)) {
                TryLoadManifest(manifestPath);
            }
        }

        /// <summary>
        /// Attempts to retrieve a patch definition by identifier.
        /// </summary>
        /// <param name="id">Patch identifier.</param>
        /// <param name="definition">Resolved patch definition.</param>
        /// <returns>True when found.</returns>
        public bool TryGetPatch(string id, out EnginePatchDefinition definition) {
            if (string.IsNullOrWhiteSpace(id)) {
                definition = new EnginePatchDefinition(new EnginePatchManifest(), string.Empty, string.Empty);
                return false;
            }

            return patches.TryGetValue(id, out definition);
        }

        /// <summary>
        /// Attempts to load and store a manifest at the provided path.
        /// </summary>
        /// <param name="manifestPath">Path to the patch manifest.</param>
        void TryLoadManifest(string manifestPath) {
            try {
                EnginePatchManifest manifest = loader.Load(manifestPath);
                if (string.IsNullOrWhiteSpace(manifest.Id)) {
                    errors.Add($"Patch manifest at '{manifestPath}' is missing an id.");
                    return;
                }

                string rootPath = Path.GetDirectoryName(manifestPath) ?? string.Empty;
                var definition = new EnginePatchDefinition(manifest, manifestPath, rootPath);
                AddDefinition(definition);
            } catch (Exception ex) {
                errors.Add($"Failed to load patch manifest '{manifestPath}': {ex.Message}");
            }
        }

        /// <summary>
        /// Adds a patch definition, handling duplicates.
        /// </summary>
        /// <param name="definition">Definition to add.</param>
        void AddDefinition(EnginePatchDefinition definition) {
            if (definition == null) {
                return;
            }

            if (patches.ContainsKey(definition.Id)) {
                errors.Add($"Duplicate patch id '{definition.Id}' found at '{definition.ManifestPath}'.");
                return;
            }

            patches[definition.Id] = definition;
        }
    }
}

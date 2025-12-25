namespace helengine.patching {
    /// <summary>
    /// Resolves patch dependencies and validates conflicts.
    /// </summary>
    public sealed class EnginePatchResolver {
        readonly EnginePatchCatalog catalog;

        /// <summary>
        /// Initializes a resolver with the provided catalog.
        /// </summary>
        /// <param name="catalog">Catalog of available patches.</param>
        public EnginePatchResolver(EnginePatchCatalog catalog) {
            this.catalog = catalog ?? new EnginePatchCatalog();
        }

        /// <summary>
        /// Resolves patches by id, including dependencies.
        /// </summary>
        /// <param name="patchIds">Patch identifiers to resolve.</param>
        /// <returns>Resolution result.</returns>
        public EnginePatchResolution Resolve(IReadOnlyList<string> patchIds) {
            var resolution = new EnginePatchResolution();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (patchIds == null || patchIds.Count == 0) {
                return resolution;
            }

            for (int i = 0; i < patchIds.Count; i++) {
                string id = patchIds[i];
                if (string.IsNullOrWhiteSpace(id)) {
                    continue;
                }

                ResolvePatch(id.Trim(), resolution, visited, visiting);
            }

            ValidateConflicts(resolution);
            return resolution;
        }

        /// <summary>
        /// Resolves a single patch and its dependencies.
        /// </summary>
        /// <param name="id">Patch identifier.</param>
        /// <param name="resolution">Resolution container.</param>
        /// <param name="visited">Already resolved ids.</param>
        /// <param name="visiting">Ids currently being resolved.</param>
        void ResolvePatch(
            string id,
            EnginePatchResolution resolution,
            HashSet<string> visited,
            HashSet<string> visiting) {
            if (visited.Contains(id)) {
                return;
            }

            if (visiting.Contains(id)) {
                resolution.AddError($"Circular dependency detected for patch '{id}'.");
                return;
            }

            if (!catalog.TryGetPatch(id, out EnginePatchDefinition patch)) {
                resolution.AddError($"Missing patch '{id}'.");
                return;
            }

            visiting.Add(id);
            ResolveDependencies(patch, resolution, visited, visiting);
            visiting.Remove(id);
            visited.Add(id);
            resolution.AddPatch(patch);
        }

        /// <summary>
        /// Resolves dependencies for a patch.
        /// </summary>
        /// <param name="patch">Patch definition.</param>
        /// <param name="resolution">Resolution container.</param>
        /// <param name="visited">Already resolved ids.</param>
        /// <param name="visiting">Ids currently being resolved.</param>
        void ResolveDependencies(
            EnginePatchDefinition patch,
            EnginePatchResolution resolution,
            HashSet<string> visited,
            HashSet<string> visiting) {
            List<string> deps = patch.Manifest.Dependencies;
            if (deps == null || deps.Count == 0) {
                return;
            }

            for (int i = 0; i < deps.Count; i++) {
                string depId = deps[i];
                if (string.IsNullOrWhiteSpace(depId)) {
                    continue;
                }

                ResolvePatch(depId.Trim(), resolution, visited, visiting);
            }
        }

        /// <summary>
        /// Validates conflict declarations across resolved patches.
        /// </summary>
        /// <param name="resolution">Resolution container.</param>
        void ValidateConflicts(EnginePatchResolution resolution) {
            if (resolution == null || !resolution.Success) {
                return;
            }

            var resolvedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<EnginePatchDefinition> patches = resolution.Patches;
            for (int i = 0; i < patches.Count; i++) {
                resolvedIds.Add(patches[i].Id);
            }

            for (int i = 0; i < patches.Count; i++) {
                EnginePatchDefinition patch = patches[i];
                List<string> conflicts = patch.Manifest.Conflicts;
                if (conflicts == null || conflicts.Count == 0) {
                    continue;
                }

                for (int j = 0; j < conflicts.Count; j++) {
                    string conflictId = conflicts[j];
                    if (string.IsNullOrWhiteSpace(conflictId)) {
                        continue;
                    }

                    if (resolvedIds.Contains(conflictId.Trim())) {
                        resolution.AddError($"Patch '{patch.Id}' conflicts with '{conflictId}'.");
                    }
                }
            }
        }
    }
}

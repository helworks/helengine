namespace helengine {
    /// <summary>
    /// Describes one runtime code module declared by the packaged build graph.
    /// </summary>
    public sealed class RuntimeCodeModuleManifestEntry {
        /// <summary>
        /// Initializes one runtime code module manifest entry.
        /// </summary>
        /// <param name="moduleId">Stable module identifier.</param>
        /// <param name="runtimeSpecializationId">Runtime specialization id used to build the module payload.</param>
        /// <param name="loadState">Runtime residency state for the module.</param>
        /// <param name="dependencyModuleIds">Dependent module identifiers required before this module can load.</param>
        public RuntimeCodeModuleManifestEntry(
            string moduleId,
            string runtimeSpecializationId,
            RuntimeCodeModuleLoadState loadState,
            string[] dependencyModuleIds) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Runtime code module id is required.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(runtimeSpecializationId)) {
                throw new ArgumentException("Runtime code module specialization id is required.", nameof(runtimeSpecializationId));
            }
            if (dependencyModuleIds == null) {
                throw new ArgumentNullException(nameof(dependencyModuleIds));
            }
            for (int index = 0; index < dependencyModuleIds.Length; index++) {
                string dependencyModuleId = dependencyModuleIds[index];
                if (string.IsNullOrWhiteSpace(dependencyModuleId)) {
                    throw new ArgumentException("Runtime code module dependencies cannot contain blank entries.", nameof(dependencyModuleIds));
                }
            }

            ModuleId = moduleId;
            RuntimeSpecializationId = runtimeSpecializationId;
            LoadState = loadState;
            DependencyModuleIds = dependencyModuleIds;
        }

        /// <summary>
        /// Gets the stable module identifier.
        /// </summary>
        public string ModuleId { get; }

        /// <summary>
        /// Gets the runtime specialization id used to build the module payload.
        /// </summary>
        public string RuntimeSpecializationId { get; }

        /// <summary>
        /// Gets the runtime residency state for the module.
        /// </summary>
        public RuntimeCodeModuleLoadState LoadState { get; }

        /// <summary>
        /// Gets the dependent module identifiers required before this module can load.
        /// </summary>
        public string[] DependencyModuleIds { get; }
    }
}

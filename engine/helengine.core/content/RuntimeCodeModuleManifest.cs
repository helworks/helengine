namespace helengine {
    /// <summary>
    /// Describes the runtime code-module residency plan emitted by the editor build graph.
    /// </summary>
    public sealed class RuntimeCodeModuleManifest {
        /// <summary>
        /// Initializes one runtime code-module manifest.
        /// </summary>
        /// <param name="entries">Runtime code-module residency entries.</param>
        public RuntimeCodeModuleManifest(RuntimeCodeModuleManifestEntry[] entries) {
            if (entries == null) {
                throw new ArgumentNullException(nameof(entries));
            }

            Entries = entries;
        }

        /// <summary>
        /// Gets the packaged runtime code-module residency entries.
        /// </summary>
        public RuntimeCodeModuleManifestEntry[] Entries { get; }

        /// <summary>
        /// Reads one runtime code-module manifest from a JSON file written by the editor build graph.
        /// </summary>
        /// <param name="manifestPath">Path to the runtime-code-modules.json file.</param>
        /// <returns>The loaded runtime code-module manifest.</returns>
        public static RuntimeCodeModuleManifest ReadFromFile(string manifestPath) {
            if (string.IsNullOrWhiteSpace(manifestPath)) {
                throw new ArgumentException("Runtime code-module manifest path is required.", nameof(manifestPath));
            }
            if (!File.Exists(manifestPath)) {
                throw new FileNotFoundException($"Runtime code-module manifest '{manifestPath}' was not found.", manifestPath);
            }

            FileStream fileStream = File.OpenRead(manifestPath);
            StreamReader reader = new StreamReader(fileStream, System.Text.Encoding.UTF8, false, 1024, true);
            string json = reader.ReadToEnd();
            reader.Dispose();
            fileStream.Dispose();
            return RuntimeManifestJsonReader.ReadRuntimeCodeModuleManifest(json);
        }

        /// <summary>
        /// Gets the module identifiers that must remain loaded for the full runtime session.
        /// </summary>
        /// <returns>Resident-at-startup module identifiers.</returns>
        public string[] GetResidentModuleIds() {
            string[] residentModuleIds = new string[Entries.Length];
            int residentModuleCount = 0;
            for (int index = 0; index < Entries.Length; index++) {
                RuntimeCodeModuleManifestEntry entry = Entries[index];
                if (entry.LoadState != RuntimeCodeModuleLoadState.Unloadable) {
                    residentModuleIds[residentModuleCount] = entry.ModuleId;
                    residentModuleCount++;
                }
            }

            if (residentModuleCount == residentModuleIds.Length) {
                return residentModuleIds;
            }

            string[] exactResidentModuleIds = new string[residentModuleCount];
            for (int index = 0; index < residentModuleCount; index++) {
                exactResidentModuleIds[index] = residentModuleIds[index];
            }

            return exactResidentModuleIds;
        }

        /// <summary>
        /// Returns true when one module can be unloaded after it has been loaded.
        /// </summary>
        /// <param name="moduleId">Stable module identifier to evaluate.</param>
        /// <returns>True when the module is not permanently resident.</returns>
        public bool CanUnloadModule(string moduleId) {
            return GetLoadState(moduleId) != RuntimeCodeModuleLoadState.ResidentAtStartup;
        }

        /// <summary>
        /// Gets the module identifiers that may be released after their active scope ends.
        /// </summary>
        /// <returns>Unloadable module identifiers.</returns>
        public string[] GetUnloadableModuleIds() {
            string[] unloadableModuleIds = new string[Entries.Length];
            int unloadableModuleCount = 0;
            for (int index = 0; index < Entries.Length; index++) {
                RuntimeCodeModuleManifestEntry entry = Entries[index];
                if (entry.LoadState == RuntimeCodeModuleLoadState.Unloadable) {
                    unloadableModuleIds[unloadableModuleCount] = entry.ModuleId;
                    unloadableModuleCount++;
                }
            }

            if (unloadableModuleCount == unloadableModuleIds.Length) {
                return unloadableModuleIds;
            }

            string[] exactUnloadableModuleIds = new string[unloadableModuleCount];
            for (int index = 0; index < unloadableModuleCount; index++) {
                exactUnloadableModuleIds[index] = unloadableModuleIds[index];
            }

            return exactUnloadableModuleIds;
        }

        /// <summary>
        /// Gets one module's runtime load state.
        /// </summary>
        /// <param name="moduleId">Stable module identifier to locate.</param>
        /// <returns>Load state for the requested module.</returns>
        RuntimeCodeModuleLoadState GetLoadState(string moduleId) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id is required.", nameof(moduleId));
            }

            for (int index = 0; index < Entries.Length; index++) {
                RuntimeCodeModuleManifestEntry entry = Entries[index];
                if (string.Equals(entry.ModuleId, moduleId, StringComparison.OrdinalIgnoreCase)) {
                    return entry.LoadState;
                }
            }

            throw new InvalidOperationException($"Runtime code module '{moduleId}' was not found in the residency manifest.");
        }
    }
}

namespace helengine.editor {
    /// <summary>
    /// The single definition of how Play script modules are laid out beneath a runtime modules root:
    /// <c>&lt;modulesRootPath&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>. The Play build verifies its generated projects
    /// against it, the Play fingerprint check probes it, and the player loads from it, so the three cannot drift apart.
    /// </summary>
    public static class EditorPlayModuleLayout {
        /// <summary>
        /// Resolves the folder that holds one module's assembly, symbols and copied dependencies.
        /// </summary>
        /// <param name="modulesRootPath">Runtime modules root handed to the player.</param>
        /// <param name="moduleId">Stable module identifier, which is also the assembly name.</param>
        /// <returns>Module folder path beneath the modules root.</returns>
        /// <exception cref="ArgumentException">Thrown when the root or module id is blank.</exception>
        public static string ResolveModuleDirectoryPath(string modulesRootPath, string moduleId) {
            if (string.IsNullOrWhiteSpace(modulesRootPath)) {
                throw new ArgumentException("Script modules root path must be provided.", nameof(modulesRootPath));
            } else if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }

            return Path.Combine(modulesRootPath, moduleId);
        }

        /// <summary>
        /// Resolves the assembly the player loads for one module.
        /// </summary>
        /// <param name="modulesRootPath">Runtime modules root handed to the player.</param>
        /// <param name="moduleId">Stable module identifier, which is also the assembly name.</param>
        /// <returns>Module assembly path: <c>&lt;modulesRootPath&gt;/&lt;moduleId&gt;/&lt;moduleId&gt;.dll</c>.</returns>
        /// <exception cref="ArgumentException">Thrown when the root or module id is blank.</exception>
        public static string ResolveModuleAssemblyPath(string modulesRootPath, string moduleId) {
            return Path.Combine(ResolveModuleDirectoryPath(modulesRootPath, moduleId), moduleId + ".dll");
        }
    }
}

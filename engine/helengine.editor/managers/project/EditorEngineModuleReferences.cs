namespace helengine.editor {
    /// <summary>
    /// Resolves the optional engine plugins (for example <c>helengine.animation.runtime</c>) a project code module declares
    /// through <c>engineModules</c>: where their compile assemblies live and whether a platform transpiles them.
    /// </summary>
    internal static class EditorEngineModuleReferences {
        /// <summary>
        /// Resolves the compile assembly of one engine plugin. Plugins ship next to the editor assemblies, because the
        /// build host references every optional engine plugin it can cook for.
        /// </summary>
        /// <param name="moduleId">Code module that declares the plugin, named in errors.</param>
        /// <param name="engineModuleId">Engine plugin project name, which is also its assembly name.</param>
        /// <returns>Absolute path of the plugin assembly.</returns>
        internal static string ResolveAssemblyPath(string moduleId, string engineModuleId) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (string.IsNullOrWhiteSpace(engineModuleId)) {
                throw new ArgumentException("Engine module id must be provided.", nameof(engineModuleId));
            }
            if (engineModuleId.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) {
                throw new InvalidOperationException(
                    $"Code module '{moduleId}' declares engine module '{engineModuleId}', which is not an engine plugin project name.");
            }

            string engineAssemblyDirectoryPath = Path.GetDirectoryName(typeof(EditorEngineModuleReferences).Assembly.Location);
            if (string.IsNullOrWhiteSpace(engineAssemblyDirectoryPath)) {
                throw new InvalidOperationException("The deployed HelEngine editor assembly directory could not be resolved.");
            }

            string assemblyPath = Path.Combine(engineAssemblyDirectoryPath, engineModuleId + ".dll");
            if (!File.Exists(assemblyPath)) {
                throw new FileNotFoundException(
                    $"Code module '{moduleId}' declares engine module '{engineModuleId}', but its assembly was not found next to the editor.",
                    assemblyPath);
            }

            return assemblyPath;
        }

        /// <summary>
        /// Throws unless the platform transpiles every engine plugin the module declares, since a plugin that is not part of
        /// the platform's generated-core has no native code for the module to call.
        /// </summary>
        /// <param name="moduleEntry">Code module whose declared engine plugins are checked.</param>
        /// <param name="platformId">Target platform, named in errors.</param>
        /// <param name="generatedCoreProjectPaths">Engine plugin projects the platform transpiles; a plugin matches by project file name.</param>
        internal static void ValidateTranspiledByPlatform(
            EditorCodeModuleManifestEntry moduleEntry,
            string platformId,
            IReadOnlyList<string> generatedCoreProjectPaths) {
            if (moduleEntry == null) {
                throw new ArgumentNullException(nameof(moduleEntry));
            }
            if (string.IsNullOrWhiteSpace(platformId)) {
                throw new ArgumentException("Platform id must be provided.", nameof(platformId));
            }
            if (generatedCoreProjectPaths == null) {
                throw new ArgumentNullException(nameof(generatedCoreProjectPaths));
            }

            for (int index = 0; index < moduleEntry.EngineModules.Length; index++) {
                string engineModuleId = moduleEntry.EngineModules[index];
                if (!IsTranspiledByPlatform(engineModuleId, generatedCoreProjectPaths)) {
                    throw new InvalidOperationException(
                        $"Code module '{moduleEntry.ModuleId}' references engine module '{engineModuleId}', but platform '{platformId}' does not transpile it: the platform's generatedCoreProjectPaths must list '{engineModuleId}.csproj'.");
                }
            }
        }

        /// <summary>
        /// Returns whether one engine plugin is among the projects a platform transpiles.
        /// </summary>
        /// <param name="engineModuleId">Engine plugin project name.</param>
        /// <param name="generatedCoreProjectPaths">Engine plugin projects the platform transpiles.</param>
        /// <returns>True when a project file named after the plugin is listed.</returns>
        static bool IsTranspiledByPlatform(string engineModuleId, IReadOnlyList<string> generatedCoreProjectPaths) {
            for (int index = 0; index < generatedCoreProjectPaths.Count; index++) {
                string projectPath = generatedCoreProjectPaths[index];
                if (string.IsNullOrWhiteSpace(projectPath)) {
                    continue;
                }

                if (string.Equals(Path.GetFileNameWithoutExtension(projectPath), engineModuleId, StringComparison.OrdinalIgnoreCase)) {
                    return true;
                }
            }

            return false;
        }
    }
}

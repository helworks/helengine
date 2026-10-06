namespace helengine.editor {
    /// <summary>
    /// Routes persisted editor components to their owning assembly and project components to the reloadable script host.
    /// </summary>
    internal static class EditorPersistedComponentTypeResolver {
        /// <summary>
        /// Already-loaded editor assembly that owns authoring components such as blueprint instance markers.
        /// </summary>
        static readonly System.Reflection.Assembly EditorAssembly = typeof(ComponentPersistenceRegistry).Assembly;

        /// <summary>
        /// Resolves editor-owned types independently of project script registration while preserving script-host failures.
        /// </summary>
        /// <param name="componentTypeId">Current persisted component type identifier.</param>
        /// <param name="scriptTypeResolver">Project script resolver, when the host loads reloadable modules.</param>
        /// <returns>The resolved type, or null when a native component identifier has no matching type.</returns>
        internal static Type Resolve(string componentTypeId, IScriptTypeResolver scriptTypeResolver) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                throw new ArgumentException("Component type id must be provided.", nameof(componentTypeId));
            }

            if (!componentTypeId.Contains(',', StringComparison.Ordinal)) {
                return PersistedComponentTypeResolver.TryResolve(componentTypeId);
            }

            string[] parts = componentTypeId.Split(',', 2, StringSplitOptions.TrimEntries);
            if (string.Equals(parts[1], EditorAssembly.GetName().Name, StringComparison.OrdinalIgnoreCase)) {
                return EditorAssembly.GetType(parts[0], false, false);
            }
            if (scriptTypeResolver != null) {
                return scriptTypeResolver.Resolve(componentTypeId);
            }

            return PersistedComponentTypeResolver.TryResolve(componentTypeId);
        }
    }
}

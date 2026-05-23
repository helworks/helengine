namespace helengine {
    /// <summary>
    /// Resolves persisted component type identifiers back to runtime component types for both assembly-qualified script ids and legacy engine short ids.
    /// </summary>
    public static class PersistedComponentTypeResolver {
        /// <summary>
        /// Resolves one persisted component type identifier back to its runtime type when available.
        /// </summary>
        /// <param name="componentTypeId">Persisted component type identifier to resolve.</param>
        /// <returns>Resolved runtime type when found; otherwise null.</returns>
        public static Type TryResolve(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return null;
            }

            Type componentType = Type.GetType(componentTypeId, false);
            if (componentType != null) {
                return componentType;
            }

            System.Reflection.Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++) {
                componentType = assemblies[index].GetType(componentTypeId, false, false);
                if (componentType != null) {
                    return componentType;
                }
            }

            return null;
        }
    }
}

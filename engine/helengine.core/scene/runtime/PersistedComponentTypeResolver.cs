#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;

namespace helengine {
    /// <summary>
    /// Resolves current persisted component type identifiers back to runtime component types.
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
            if (componentTypeId.Contains(',', StringComparison.Ordinal)) {
                try {
                    Type assemblyQualifiedType = Type.GetType(componentTypeId, false);
                    string assemblyName = assemblyQualifiedType?.Assembly.GetName().Name ?? string.Empty;
                    if (assemblyQualifiedType == null
                        || (string.Equals(assemblyQualifiedType.Namespace, "helengine", StringComparison.Ordinal)
                            && assemblyName.StartsWith("helengine", StringComparison.Ordinal))) {
                        return null;
                    }

                    return assemblyQualifiedType;
                } catch (Exception) {
                    return null;
                }
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++) {
                Type componentType = assemblies[index].GetType(componentTypeId, false, false);
                if (componentType != null) {
                    return componentType;
                }
            }

            return null;
        }
    }
}
#else
namespace helengine {
    /// <summary>
    /// Provides a native-safe persisted component type resolver stub for player builds where runtime reflection is disabled.
    /// </summary>
    public static class PersistedComponentTypeResolver {
        /// <summary>
        /// Returns null because player builds with runtime reflection disabled do not resolve component types dynamically.
        /// </summary>
        /// <param name="componentTypeId">Persisted component type identifier that would otherwise be resolved.</param>
        /// <returns>Always null in native player builds.</returns>
        public static Type TryResolve(string componentTypeId) {
            return null;
        }
    }
}
#endif

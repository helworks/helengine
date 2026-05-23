#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;

namespace helengine {
    /// <summary>
    /// Resolves persisted component type identifiers back to runtime component types for both assembly-qualified script ids and legacy engine short ids.
    /// </summary>
    public static class PersistedComponentTypeResolver {
        /// <summary>
        /// Assembly names that may contain engine-owned component types persisted with legacy short identifiers.
        /// </summary>
        static readonly string[] CandidateAssemblyNames = [
            "helengine.core",
            "helengine.physics3d"
        ];

        /// <summary>
        /// Resolves one persisted component type identifier back to its runtime type when available.
        /// </summary>
        /// <param name="componentTypeId">Persisted component type identifier to resolve.</param>
        /// <returns>Resolved runtime type when found; otherwise null.</returns>
        public static Type TryResolve(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return null;
            }

            Type componentType = TryResolveAssemblyQualifiedType(componentTypeId);
            if (componentType != null) {
                return componentType;
            }
            if (componentTypeId.Contains(',', StringComparison.Ordinal)) {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < assemblies.Length; index++) {
                componentType = assemblies[index].GetType(componentTypeId, false, false);
                if (componentType != null) {
                    return componentType;
                }
            }

            for (int index = 0; index < CandidateAssemblyNames.Length; index++) {
                componentType = Type.GetType(componentTypeId + ", " + CandidateAssemblyNames[index], false);
                if (componentType != null) {
                    return componentType;
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve one persisted component identifier directly when it is already assembly-qualified.
        /// </summary>
        /// <param name="componentTypeId">Persisted component identifier to evaluate.</param>
        /// <returns>Resolved runtime type when the identifier is valid and loadable; otherwise null.</returns>
        static Type TryResolveAssemblyQualifiedType(string componentTypeId) {
            if (string.IsNullOrWhiteSpace(componentTypeId)) {
                return null;
            }
            if (!componentTypeId.Contains(',', StringComparison.Ordinal)) {
                return null;
            }

            try {
                return Type.GetType(componentTypeId, false);
            } catch (Exception) {
                return null;
            }
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

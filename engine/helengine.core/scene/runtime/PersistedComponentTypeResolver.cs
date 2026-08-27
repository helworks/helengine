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
                if (!TryParseCurrentAssemblyQualifiedId(componentTypeId, out string typeName, out string assemblyName)
                    || string.Equals(typeName.Substring(0, typeName.LastIndexOf('.')), "helengine", StringComparison.Ordinal)) {
                    return null;
                }

                try {
                    Assembly assembly = Assembly.Load(new AssemblyName(assemblyName));
                    Type componentType = assembly.GetType(typeName, false, false);
                    return componentType != null && typeof(Component).IsAssignableFrom(componentType)
                        ? componentType
                        : null;
                } catch (Exception) {
                    return null;
                }
            }

            Type coreComponentType = typeof(Component).Assembly.GetType(componentTypeId, false, false);
            if (coreComponentType != null && typeof(Component).IsAssignableFrom(coreComponentType)) {
                return coreComponentType;
            }

            if (componentTypeId.StartsWith("helengine.", StringComparison.Ordinal)) {
                try {
                    Assembly physicsAssembly = Assembly.Load(new AssemblyName("helengine.physics"));
                    Type physicsComponentType = physicsAssembly.GetType(componentTypeId, false, false);
                    if (physicsComponentType != null && typeof(Component).IsAssignableFrom(physicsComponentType)) {
                        return physicsComponentType;
                    }
                } catch (Exception) {
                    // The optional physics assembly is not present in every core-only host.
                }
            }

            return null;
        }

        /// <summary>
        /// Parses the current simple assembly-qualified component identifier shape without accepting runtime qualification metadata.
        /// </summary>
        /// <param name="componentTypeId">Stable component identifier to parse.</param>
        /// <param name="typeName">Parsed component type name.</param>
        /// <param name="assemblyName">Parsed simple assembly name.</param>
        /// <returns>True when the identifier has exactly one simple assembly separator.</returns>
        static bool TryParseCurrentAssemblyQualifiedId(string componentTypeId, out string typeName, out string assemblyName) {
            typeName = null;
            assemblyName = null;
            int separatorIndex = componentTypeId.IndexOf(", ", StringComparison.Ordinal);
            if (separatorIndex <= 0 || componentTypeId.IndexOf(',', separatorIndex + 1) >= 0) {
                return false;
            }

            string parsedTypeName = componentTypeId.Substring(0, separatorIndex);
            string parsedAssemblyName = componentTypeId.Substring(separatorIndex + 2);
            if (string.IsNullOrWhiteSpace(parsedTypeName)
                || string.IsNullOrWhiteSpace(parsedAssemblyName)
                || parsedAssemblyName.IndexOf(' ') >= 0) {
                return false;
            }

            int namespaceSeparatorIndex = parsedTypeName.LastIndexOf('.');
            if (namespaceSeparatorIndex <= 0 || namespaceSeparatorIndex == parsedTypeName.Length - 1) {
                return false;
            }

            typeName = parsedTypeName;
            assemblyName = parsedAssemblyName;
            return true;
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

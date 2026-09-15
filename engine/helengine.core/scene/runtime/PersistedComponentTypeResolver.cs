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
                    || IsSpoofedEngineComponentId(typeName, assemblyName)) {
                    return null;
                }

                Assembly assembly = TryLoadAssembly(assemblyName);
                if (assembly == null) {
                    return null;
                }

                try {
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
                // The optional physics assembly is not present in every core-only host.
                Assembly physicsAssembly = TryLoadAssembly(PhysicsAssemblyName);
                if (physicsAssembly != null) {
                    Type physicsComponentType = physicsAssembly.GetType(componentTypeId, false, false);
                    if (physicsComponentType != null && typeof(Component).IsAssignableFrom(physicsComponentType)) {
                        return physicsComponentType;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Simple name of the optional physics assembly.
        /// </summary>
        const string PhysicsAssemblyName = "helengine.physics";

        /// <summary>
        /// Simple names of assemblies that failed to load, remembered until any new assembly loads into the domain.
        /// </summary>
        static readonly HashSet<string> UnloadableAssemblyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Guards the unloadable assembly name set.
        /// </summary>
        static readonly object UnloadableAssemblyNamesLock = new object();

        /// <summary>
        /// Forgets failed assembly loads whenever a new assembly enters the domain, so a later load is retried once.
        /// </summary>
        static PersistedComponentTypeResolver() {
            AppDomain.CurrentDomain.AssemblyLoad += (sender, args) => {
                lock (UnloadableAssemblyNamesLock) {
                    UnloadableAssemblyNames.Clear();
                }
            };
        }

        /// <summary>
        /// Returns an already-loaded non-collectible assembly by simple name, or loads it once; a failed load is remembered
        /// so scenes that reference an assembly that is not present do not throw on every component lookup.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name to resolve.</param>
        /// <returns>Resolved assembly, or null when it cannot be loaded.</returns>
        static Assembly TryLoadAssembly(string assemblyName) {
            Assembly[] loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int index = 0; index < loadedAssemblies.Length; index++) {
                Assembly loadedAssembly = loadedAssemblies[index];
                if (loadedAssembly.IsCollectible) {
                    continue;
                }
                if (string.Equals(loadedAssembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase)) {
                    return loadedAssembly;
                }
            }

            lock (UnloadableAssemblyNamesLock) {
                if (UnloadableAssemblyNames.Contains(assemblyName)) {
                    return null;
                }
            }

            try {
                return Assembly.Load(new AssemblyName(assemblyName));
            } catch (Exception) {
                lock (UnloadableAssemblyNamesLock) {
                    UnloadableAssemblyNames.Add(assemblyName);
                }
                return null;
            }
        }

        /// <summary>
        /// Determines whether an assembly-qualified id attempts to repackage an engine-owned component under a non-current qualification.
        /// </summary>
        /// <param name="typeName">Fully qualified component type name from the persisted id.</param>
        /// <param name="assemblyName">Simple assembly name from the persisted id.</param>
        /// <returns>True when the id uses an engine-owned component name in an invalid assembly-qualified form.</returns>
        static bool IsSpoofedEngineComponentId(string typeName, string assemblyName) {
            if (string.IsNullOrWhiteSpace(typeName) || string.IsNullOrWhiteSpace(assemblyName)) {
                return true;
            }

            int namespaceSeparatorIndex = typeName.LastIndexOf('.');
            if (namespaceSeparatorIndex <= 0
                || !string.Equals(typeName.Substring(0, namespaceSeparatorIndex), "helengine", StringComparison.Ordinal)) {
                return false;
            }
            if (IsEngineComponentAssemblyName(assemblyName)) {
                return true;
            }

            Type coreComponentType = typeof(Component).Assembly.GetType(typeName, false, false);
            if (coreComponentType != null && typeof(Component).IsAssignableFrom(coreComponentType)) {
                return true;
            }

            Assembly physicsAssembly = TryLoadAssembly(PhysicsAssemblyName);
            if (physicsAssembly == null) {
                return false;
            }

            try {
                Type physicsComponentType = physicsAssembly.GetType(typeName, false, false);
                return physicsComponentType != null && typeof(Component).IsAssignableFrom(physicsComponentType);
            } catch (Exception) {
                return false;
            }
        }

        /// <summary>
        /// Determines whether a simple assembly name identifies one engine-owned component assembly.
        /// </summary>
        /// <param name="assemblyName">Simple assembly name under evaluation.</param>
        /// <returns>True for exact engine component assembly identities; otherwise false.</returns>
        static bool IsEngineComponentAssemblyName(string assemblyName) {
            return string.Equals(assemblyName, "helengine.core", StringComparison.Ordinal)
                || string.Equals(assemblyName, "helengine.physics", StringComparison.Ordinal);
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
                || string.IsNullOrWhiteSpace(parsedAssemblyName)) {
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

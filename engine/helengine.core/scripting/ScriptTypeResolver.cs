#if !HELENGINE_CODEGEN_DISABLE_RUNTIME_SCRIPT_REFLECTION
using System.Reflection;

namespace helengine {
    /// <summary>
    /// Resolves persisted script type names against assemblies registered by code-module id.
    /// </summary>
    public sealed class ScriptTypeResolver : IScriptTypeResolver {
        /// <summary>
        /// Assemblies keyed by module id.
        /// </summary>
        readonly Dictionary<string, Assembly> AssembliesByModuleId;

        /// <summary>
        /// Initializes an empty script type resolver.
        /// </summary>
        public ScriptTypeResolver() {
            AssembliesByModuleId = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Registers one loaded module assembly.
        /// </summary>
        /// <param name="moduleId">Stable module id that owns the assembly.</param>
        /// <param name="assembly">Loaded assembly for the module.</param>
        public void Register(string moduleId, Assembly assembly) {
            if (string.IsNullOrWhiteSpace(moduleId)) {
                throw new ArgumentException("Module id must be provided.", nameof(moduleId));
            }
            if (assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }

            AssembliesByModuleId[moduleId] = assembly;
        }

        /// <summary>
        /// Resolves one assembly-qualified script type name against the loaded module assemblies.
        /// </summary>
        /// <param name="assemblyQualifiedTypeName">Assembly-qualified script type name.</param>
        /// <returns>Resolved script type.</returns>
        public Type Resolve(string assemblyQualifiedTypeName) {
            if (string.IsNullOrWhiteSpace(assemblyQualifiedTypeName)) {
                throw new ArgumentException("Assembly-qualified type name must be provided.", nameof(assemblyQualifiedTypeName));
            }

            string[] parts = assemblyQualifiedTypeName.Split(',', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2) {
                throw new InvalidOperationException($"Type '{assemblyQualifiedTypeName}' is not assembly-qualified.");
            }

            string typeName = parts[0];
            string moduleId = parts[1];
            if (!AssembliesByModuleId.TryGetValue(moduleId, out Assembly assembly)) {
                throw new InvalidOperationException($"Script assembly '{moduleId}' is not loaded for type '{assemblyQualifiedTypeName}'.");
            }

            Type resolvedType = assembly.GetType(typeName, false, false);
            if (resolvedType == null) {
                throw new InvalidOperationException($"Type '{typeName}' was not found in loaded script assembly '{moduleId}'.");
            }

            return resolvedType;
        }
    }
}
#endif

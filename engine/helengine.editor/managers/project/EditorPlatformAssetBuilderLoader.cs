using System.Reflection;
using helengine.baseplatform.Builders;

namespace helengine.editor {
    /// <summary>
    /// Loads a platform asset builder from one dynamically provided assembly path.
    /// </summary>
    public class EditorPlatformAssetBuilderLoader {
        /// <summary>
        /// Loads one builder implementation from the supplied assembly path.
        /// </summary>
        /// <param name="assemblyPath">Absolute builder assembly path resolved from the platform manifest.</param>
        /// <returns>Loaded platform asset builder instance.</returns>
        public virtual IPlatformAssetBuilder Load(string assemblyPath) {
            if (string.IsNullOrWhiteSpace(assemblyPath)) {
                throw new ArgumentException("Builder assembly path must be provided.", nameof(assemblyPath));
            }

            string fullAssemblyPath = Path.GetFullPath(assemblyPath);
            if (!File.Exists(fullAssemblyPath)) {
                throw new FileNotFoundException($"Builder assembly '{fullAssemblyPath}' was not found.", fullAssemblyPath);
            }

            Assembly assembly = Assembly.LoadFrom(fullAssemblyPath);
            Type builderType = FindBuilderType(assembly);
            object builderInstance = Activator.CreateInstance(builderType);
            if (builderInstance is not IPlatformAssetBuilder builder) {
                throw new InvalidOperationException($"Type '{builderType.FullName}' from assembly '{assembly.FullName}' did not create a platform asset builder instance.");
            }

            return builder;
        }

        /// <summary>
        /// Finds the first concrete builder implementation exported by one assembly.
        /// </summary>
        /// <param name="assembly">Assembly to inspect.</param>
        /// <returns>Concrete builder type.</returns>
        static Type FindBuilderType(Assembly assembly) {
            if (assembly == null) {
                throw new ArgumentNullException(nameof(assembly));
            }

            Type builderInterfaceType = typeof(IPlatformAssetBuilder);
            Type[] assemblyTypes = assembly.GetTypes();
            for (int index = 0; index < assemblyTypes.Length; index++) {
                Type candidate = assemblyTypes[index];
                if (!candidate.IsClass || candidate.IsAbstract) {
                    continue;
                }
                if (!builderInterfaceType.IsAssignableFrom(candidate)) {
                    continue;
                }

                return candidate;
            }

            throw new InvalidOperationException($"Assembly '{assembly.FullName}' did not expose a platform asset builder implementation.");
        }
    }
}
